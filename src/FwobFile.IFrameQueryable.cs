using Mozo.Fwob.Exceptions;
using Mozo.Fwob.Frame;
using Mozo.Fwob.Header;
using Mozo.Fwob.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Mozo.Fwob;

/// <summary>
/// The implementation of interface <see cref="IFrameQueryable{TFrame, TKey}"/>.
/// </summary>
/// <typeparam name="TFrame"></typeparam>
/// <typeparam name="TKey"></typeparam>
public partial class FwobFile<TFrame, TKey>
{
    // Cached first and last frames
    private TFrame? _firstFrame = null, _lastFrame = null;

    public override TFrame? FirstFrame => _firstFrame;

    public override TFrame? LastFrame => _lastFrame;

    public override long FrameCount
    {
        get
        {
            Debug.Assert(IsFileOpen);
            return Header.FrameCount;
        }
    }

    public override TFrame? GetFrame(long index)
    {
        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);

        if (index < 0 || index >= Header.FrameCount)
            return null;

        using BinaryReader br = new(Stream, Encoding.UTF8, true);

        Debug.Assert(br.BaseStream.Length == Header.FileLength);
        br.BaseStream.Seek(Header.FirstFramePosition + index * Header.FrameLength, SeekOrigin.Begin);

        return ReadFrame(br);
    }

    private long GetBound(BinaryReader br, TKey key, bool lower)
    {
        Debug.Assert(br.BaseStream.Length == Header.FileLength);

        long pos = Header.FirstFramePosition;

        long lo, hi;
        for (lo = 0, hi = Header.FrameCount; lo < hi;)
        {
            long mid = lo + (hi - lo >> 1);

            int cmp = ReadKey(br, pos + mid * Header.FrameLength).CompareTo(key);
            if (lower ? cmp < 0 : cmp <= 0)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    private static readonly Action<BinaryWriter, TFrame> WriteFrame = FwobFrameWriterGenerator<TFrame>.GenerateFrameWriter();

    private static readonly Func<BinaryReader, TFrame> ReadFrame = FwobFrameReaderGenerator<TFrame, TKey>.GenerateFrameReader();

    private static readonly Func<BinaryReader, long, TKey> ReadKey = FwobFrameReaderGenerator<TFrame, TKey>.GenerateKeyReader();

    public override IEnumerable<TFrame> GetFrames(TKey firstKey, TKey lastKey)
    {
        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);
        Debug.Assert(firstKey.CompareTo(lastKey) <= 0);

        if (Header.FrameCount == 0)
            yield break;

        using BinaryReader br = new(Stream, Encoding.UTF8, true);

        long p = GetBound(br, firstKey, true);
        br.BaseStream.Seek(Header.FirstFramePosition + p * Header.FrameLength, SeekOrigin.Begin);

        for (; p < Header.FrameCount; p++)
        {
            TFrame frame = ReadFrame(br);

            if (GetKey(frame).CompareTo(lastKey) > 0)
                yield break;

            yield return frame;
        }
    }

    public override IEnumerable<TFrame> GetFramesAfter(TKey firstKey)
    {
        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);

        if (Header.FrameCount == 0)
            yield break;

        using BinaryReader br = new(Stream, Encoding.UTF8, true);

        long p = GetBound(br, firstKey, true);
        br.BaseStream.Seek(Header.FirstFramePosition + p * Header.FrameLength, SeekOrigin.Begin);

        for (; p < Header.FrameCount; p++)
        {
            yield return ReadFrame(br);
        }
    }

    public override IEnumerable<TFrame> GetFramesBefore(TKey lastKey)
    {
        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);

        if (Header.FrameCount == 0)
            yield break;

        using BinaryReader br = new(Stream, Encoding.UTF8, true);

        long p = GetBound(br, lastKey, false);
        br.BaseStream.Seek(Header.FirstFramePosition, SeekOrigin.Begin);

        for (; p > 0; p--)
        {
            yield return ReadFrame(br);
        }
    }

    public override long AppendFrames(IEnumerable<TFrame> frames)
    {
        if (frames == null)
            throw new ArgumentNullException(nameof(frames));

        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);

        IEnumerator<TFrame> it = frames.GetEnumerator();
        if (!it.MoveNext())
            return 0;

        using BinaryWriter bw = new(Stream, Encoding.UTF8, true);

        Debug.Assert(bw.BaseStream.Length == Header.FileLength);
        bw.BaseStream.Seek(Header.LastFramePosition, SeekOrigin.Begin);

        TFrame? last = LastFrame;
        long count = 0;
        do
        {
            if (last != null && GetKey(it.Current).CompareTo(GetKey(last)) < 0)
            {
                _lastFrame = last;
                Header.FrameCount += count;
                bw.UpdateFrameCount(Header);
                throw new KeyOrderViolationException(FilePath!);
            }

            WriteFrame(bw, it.Current);
            last = it.Current;
            _firstFrame ??= last;
            count++;
        }
        while (it.MoveNext());

        _lastFrame = last;
        Header.FrameCount += count;
        bw.UpdateFrameCount(Header);

        return count;
    }

    public override long AppendFramesTx(IEnumerable<TFrame> frames)
    {
        if (frames == null)
            throw new ArgumentNullException(nameof(frames));

        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);

        TFrame? last = LastFrame;
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

        using BinaryWriter bw = new(Stream, Encoding.UTF8, true);

        Debug.Assert(bw.BaseStream.Length == Header.FileLength);
        bw.BaseStream.Seek(Header.LastFramePosition, SeekOrigin.Begin);

        foreach (TFrame frame in list)
            WriteFrame(bw, frame);

        _firstFrame ??= list[0];
        _lastFrame = last;

        Header.FrameCount += list.Count;
        bw.UpdateFrameCount(Header);

        return list.Count;
    }

    public override long DeleteFramesAfter(TKey firstKey)
    {
        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);

        if (Header.FrameCount == 0)
            return 0;

        using BinaryReader br = new(Stream, Encoding.UTF8, true);
        using BinaryWriter bw = new(Stream, Encoding.UTF8, true);

        long newFrameCount = GetBound(br, firstKey, true), deletedFrameCount = FrameCount - newFrameCount;

        if (deletedFrameCount == 0)
            return 0;

        if (newFrameCount == 0)
        {
            _firstFrame = _lastFrame = null;
        }
        else
        {
            br.BaseStream.Seek(Header.FirstFramePosition + (newFrameCount - 1) * Header.FrameLength, SeekOrigin.Begin);
            _lastFrame = ReadFrame(br);
        }

        bw.BaseStream.SetLength(Header.FirstFramePosition + newFrameCount * Header.FrameLength);
        Header.FrameCount = newFrameCount;
        bw.UpdateFrameCount(Header);

        return deletedFrameCount;
    }

    public override long DeleteFramesBefore(TKey lastKey)
    {
        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);

        if (Header.FrameCount == 0)
            return 0;

        using BinaryReader br = new(Stream, Encoding.UTF8, true);
        using BinaryWriter bw = new(Stream, Encoding.UTF8, true);

        long removedFrameCount = GetBound(br, lastKey, false), newFrameCount = FrameCount - removedFrameCount;

        if (removedFrameCount == 0)
            return 0;

        if (newFrameCount == 0)
        {
            _firstFrame = _lastFrame = null;
        }
        else
        {
            long totalBytes = newFrameCount * Header.FrameLength;
            long readerPos = Header.FirstFramePosition + removedFrameCount * Header.FrameLength;
            long writerPos = Header.FirstFramePosition;

            for (; totalBytes > 0; totalBytes -= BlockCopyBufSize)
            {
                // br and bw share the same stream, i.e., the BaseStream.Position must be set every read and write.
                br.BaseStream.Seek(readerPos, SeekOrigin.Begin);
                byte[] buf = br.ReadBytes((int)Math.Min(totalBytes, BlockCopyBufSize));

                bw.BaseStream.Seek(writerPos, SeekOrigin.Begin);
                bw.Write(buf);

                readerPos += buf.Length;
                writerPos += buf.Length;
            }

            br.BaseStream.Seek(Header.FirstFramePosition, SeekOrigin.Begin);
            _firstFrame = ReadFrame(br);
        }

        bw.BaseStream.SetLength(Header.FirstFramePosition + newFrameCount * Header.FrameLength);
        Header.FrameCount = newFrameCount;
        bw.UpdateFrameCount(Header);

        return removedFrameCount;
    }

    public override void ClearFrames()
    {
        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);

        if (Header.FrameCount == 0)
            return;

        using BinaryWriter bw = new(Stream, Encoding.UTF8, true);

        Debug.Assert(bw.BaseStream.Length > Header.FirstFramePosition);
        bw.BaseStream.SetLength(Header.FirstFramePosition);

        _firstFrame = _lastFrame = null;
        Header.FrameCount = 0;
        bw.UpdateFrameCount(Header);
    }
}
