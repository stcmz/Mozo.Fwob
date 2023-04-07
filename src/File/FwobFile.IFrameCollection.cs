using Mozo.Fwob.Exceptions;
using Mozo.Fwob.Extensions;
using System;
using System.Collections.Generic;
using System.IO;

namespace Mozo.Fwob;

// The implementation of interface IFrameCollection<TFrame, TKey>.
public partial class FwobFile<TFrame, TKey>
{
    private void BlockCopy(long readerPos, long writerPos, long totalBytes)
    {
        BlockCopy(_br!, _bw!, readerPos, writerPos, totalBytes);
    }

    private static void BlockCopy(BinaryReader br, BinaryWriter bw, long readerPos, long writerPos, long totalBytes)
    {
        while (totalBytes > 0)
        {
            // br and bw share the same stream, i.e., the BaseStream.Position, so we must set it every read and write.
            br.BaseStream.Seek(readerPos, SeekOrigin.Begin);
            byte[] buf = br.ReadBytes((int)Math.Min(totalBytes, BlockCopyBufSize));

            bw.BaseStream.Seek(writerPos, SeekOrigin.Begin);
            bw.Write(buf);

            readerPos += buf.Length;
            writerPos += buf.Length;
            totalBytes -= buf.Length;
        }
    }

    private void ResizeFile(long frameCount)
    {
        _bw!.BaseStream.SetLength(Header.FirstFramePosition + frameCount * Header.FrameLength);
        Header.FrameCount = frameCount;
        _bw.UpdateFrameCount(Header);
    }

    #region Append Frames

    public override long AppendFrames(IEnumerable<TFrame> frames)
    {
        if (frames == null)
            throw new ArgumentNullException(nameof(frames));

        ValidateAccess(FileAccess.Write);

        IEnumerator<TFrame> it = frames.GetEnumerator();
        if (!it.MoveNext())
            return 0;

        _bw!.BaseStream.Seek(Header.LastFramePosition, SeekOrigin.Begin);

        TFrame? last = _lastFrame;
        long count = 0;
        do
        {
            if (last != null && GetKey(it.Current).CompareTo(GetKey(last)) < 0)
            {
                _lastFrame = last;
                Header.FrameCount += count;
                _bw.UpdateFrameCount(Header);

                throw new KeyOrderViolationException(FilePath!);
            }

            WriteFrame(_bw, it.Current);
            last = it.Current;
            _firstFrame ??= last;
            count++;
        }
        while (it.MoveNext());

        _lastFrame = last;
        Header.FrameCount += count;
        _bw.UpdateFrameCount(Header);

        return count;
    }

    public override long AppendFramesTx(IEnumerable<TFrame> frames)
    {
        if (frames == null)
            throw new ArgumentNullException(nameof(frames));

        ValidateAccess(FileAccess.Write);

        TFrame? last = _lastFrame;
        List<TFrame> list = new();

        foreach (TFrame frame in frames)
        {
            if (last != null && GetKey(frame).CompareTo(GetKey(last)) < 0)
                throw new KeyOrderViolationException(FilePath!);

            last = frame;
            list.Add(frame);
        }

        if (list.Count == 0)
            return 0;

        _bw!.BaseStream.Seek(Header.LastFramePosition, SeekOrigin.Begin);

        foreach (TFrame frame in list)
            WriteFrame(_bw, frame);

        _firstFrame ??= list[0];
        _lastFrame = last;

        Header.FrameCount += list.Count;
        _bw.UpdateFrameCount(Header);

        return list.Count;
    }

    #endregion

    #region Delete Frames

    public override long DeleteFrames(IEnumerable<TKey> keys)
    {
        ValidateKeys(keys, nameof(keys));
        ValidateAccess(FileAccess.ReadWrite);

        long frameCount = Header.FrameCount;
        if (frameCount == 0)
            return 0;

        IEnumerator<TKey> it = keys.GetEnumerator();

        long readerIdx = 0, writerIdx = 0;

        // Skip non-existing query keys
        while (readerIdx == writerIdx && it.MoveNext())
        {
            (writerIdx, readerIdx) = GetEqualRange(it.Current, readerIdx, frameCount);
        }

        if (readerIdx == writerIdx)
            return 0;

        // Copy data to fill the gaps of the deleted frames
        while (it.MoveNext())
        {
            (long lb, long ub) = GetEqualRange(it.Current, readerIdx, frameCount);

            if (lb == ub)
                continue;

            long fromPos = Header.FirstFramePosition + Header.FrameLength * readerIdx;
            long toPos = Header.FirstFramePosition + Header.FrameLength * writerIdx;
            long totalBytes = Header.FrameLength * (lb - readerIdx);

            BlockCopy(fromPos, toPos, totalBytes);

            writerIdx += lb - readerIdx;
            readerIdx = ub;
        }

        // Copy the last block
        if (readerIdx != frameCount)
        {
            long fromPos = Header.FirstFramePosition + Header.FrameLength * readerIdx;
            long toPos = Header.FirstFramePosition + Header.FrameLength * writerIdx;
            long totalBytes = Header.FrameLength * (frameCount - readerIdx);

            BlockCopy(fromPos, toPos, totalBytes);

            writerIdx += frameCount - readerIdx;
        }

        // Update the cache
        if (writerIdx == 0)
        {
            _firstFrame = _lastFrame = null;
        }
        else
        {
            _firstFrame = InternalGetFrameAt(0);
            _lastFrame = InternalGetFrameAt(writerIdx - 1);
        }

        // Set the new length
        ResizeFile(writerIdx);

        return frameCount - writerIdx;
    }

    public override long DeleteFramesBetween(TKey firstKey, TKey lastKey)
    {
        if (firstKey.CompareTo(lastKey) > 0)
            throw new ArgumentException($"{nameof(lastKey)} must be greater than or equal to {nameof(firstKey)}", nameof(lastKey));

        ValidateAccess(FileAccess.ReadWrite);

        long frameCount = Header.FrameCount;
        if (frameCount == 0)
            return 0;

        // Find the span to delete
        long writerIdx = GetLowerBound(firstKey, 0, frameCount);
        long readerIdx = GetUpperBound(lastKey, writerIdx, frameCount);
        long removedFrameCount = readerIdx - writerIdx;

        if (removedFrameCount == 0)
            return 0;

        if (removedFrameCount == frameCount)
        {
            _firstFrame = _lastFrame = null;
        }
        else
        {
            long readerPos = Header.FirstFramePosition + readerIdx * Header.FrameLength;
            long writerPos = Header.FirstFramePosition + writerIdx * Header.FrameLength;
            long totalBytes = (frameCount - readerIdx) * Header.FrameLength;

            BlockCopy(readerPos, writerPos, totalBytes);

            if (writerIdx == 0)
                _firstFrame = InternalGetFrameAt(0);

            writerIdx += frameCount - readerIdx;

            if (readerIdx == frameCount)
                _lastFrame = InternalGetFrameAt(writerIdx - 1);
        }

        // Set the new length
        ResizeFile(writerIdx);

        return removedFrameCount;
    }

    public override long DeleteFramesBefore(TKey lastKey)
    {
        ValidateAccess(FileAccess.ReadWrite);

        long frameCount = Header.FrameCount;
        if (frameCount == 0)
            return 0;

        long removedFrameCount = GetUpperBound(lastKey, 0, frameCount);
        long newFrameCount = frameCount - removedFrameCount;

        if (removedFrameCount == 0)
            return 0;

        if (newFrameCount == 0)
        {
            _firstFrame = _lastFrame = null;
        }
        else
        {
            long readerPos = Header.FirstFramePosition + removedFrameCount * Header.FrameLength;
            long writerPos = Header.FirstFramePosition;
            long totalBytes = newFrameCount * Header.FrameLength;

            BlockCopy(readerPos, writerPos, totalBytes);

            _firstFrame = InternalGetFrameAt(0);
        }

        // Set the new length
        ResizeFile(newFrameCount);

        return removedFrameCount;
    }

    public override long DeleteFramesAfter(TKey firstKey)
    {
        ValidateAccess(FileAccess.ReadWrite);

        long frameCount = Header.FrameCount;
        if (frameCount == 0)
            return 0;

        long newFrameCount = GetLowerBound(firstKey, 0, frameCount);
        long deletedFrameCount = frameCount - newFrameCount;

        if (deletedFrameCount == 0)
            return 0;

        if (newFrameCount == 0)
        {
            _firstFrame = _lastFrame = null;
        }
        else
        {
            _lastFrame = InternalGetFrameAt(newFrameCount - 1);
        }

        // Set the new length
        ResizeFile(newFrameCount);

        return deletedFrameCount;
    }

    public override long DeleteAllFrames()
    {
        // FrameCount getter checks if Stream == null, so no need to do it again here
        ValidateAccess(FileAccess.Write);

        long frameCount = Header.FrameCount;
        if (frameCount == 0)
            return 0;

        _firstFrame = _lastFrame = null;

        // Set the new length
        ResizeFile(0);

        return frameCount;
    }

    #endregion
}
