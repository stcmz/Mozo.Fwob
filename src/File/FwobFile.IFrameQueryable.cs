using Mozo.Fwob.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;

namespace Mozo.Fwob;

// The implementation of interface IFrameQueryable<TFrame, TKey>.
public partial class FwobFile<TFrame, TKey>
{
    // Cached first and last frames
    private TFrame? _firstFrame = null, _lastFrame = null;

    public override TFrame? FirstFrame
    {
        get
        {
            if (Stream == null)
                throw new FileNotOpenedException();

            return _firstFrame;
        }
    }

    public override TFrame? LastFrame
    {
        get
        {
            if (Stream == null)
                throw new FileNotOpenedException();

            return _lastFrame;
        }
    }

    public override long FrameCount
    {
        get
        {
            if (Stream == null)
                throw new FileNotOpenedException();

            return Header.FrameCount;
        }
    }

    public override long LowerBoundOf(TKey key)
    {
        ValidateAccess(FileAccess.Read);
        return GetLowerBound(key, 0, Header.FrameCount);
    }

    public override long UpperBoundOf(TKey key)
    {
        ValidateAccess(FileAccess.Read);
        return GetUpperBound(key, 0, Header.FrameCount);
    }

    public override (long LowerBound, long UpperBound) EqualRangeOf(TKey key)
    {
        ValidateAccess(FileAccess.Read);
        return GetEqualRange(key, 0, Header.FrameCount);
    }

    protected override TKey InternalGetKeyAt(long index)
    {
        return ReadKey(_br!, Header.FirstFramePosition + index * Header.FrameLength);
    }

    public override TKey? GetKeyAt(long index)
    {
        ValidateAccess(FileAccess.Read);
        return base.GetKeyAt(index);
    }

    public override TFrame? GetFrameAt(long index)
    {
        ValidateAccess(FileAccess.Read);
        return base.GetFrameAt(index);
    }

    protected override TFrame InternalGetFrameAt(long index)
    {
        _br!.BaseStream.Seek(Header.FirstFramePosition + index * Header.FrameLength, SeekOrigin.Begin);
        return ReadFrame(_br);
    }

    public override IEnumerable<TFrame> GetFrames(IEnumerable<TKey> keys)
    {
        ValidateKeys(keys, nameof(keys));
        ValidateAccess(FileAccess.Read);

        long frameCount = Header.FrameCount;
        if (frameCount == 0)
            yield break;

        long pos = 0;

        foreach (TKey key in keys)
        {
            (long lb, long ub) = GetEqualRange(key, pos, frameCount);
            _br!.BaseStream.Seek(Header.FirstFramePosition + lb * Header.FrameLength, SeekOrigin.Begin);

            for (long i = lb; i < ub; i++)
                yield return ReadFrame(_br);

            pos = ub;
        }
    }

    public override IEnumerable<TFrame> GetFramesBetween(TKey firstKey, TKey lastKey)
    {
        if (firstKey.CompareTo(lastKey) > 0)
            throw new ArgumentException($"{nameof(lastKey)} must be greater than or equal to {nameof(firstKey)}");

        ValidateAccess(FileAccess.Read);

        long frameCount = Header.FrameCount;
        if (frameCount == 0)
            yield break;

        long lb = GetLowerBound(firstKey, 0, frameCount);
        long ub = GetUpperBound(lastKey, lb, frameCount);

        _br!.BaseStream.Seek(Header.FirstFramePosition + lb * Header.FrameLength, SeekOrigin.Begin);

        while (lb++ < ub)
            yield return ReadFrame(_br);
    }

    public override IEnumerable<TFrame> GetFramesBefore(TKey lastKey)
    {
        ValidateAccess(FileAccess.Read);

        long frameCount = Header.FrameCount;
        if (frameCount == 0)
            yield break;

        long ub = GetUpperBound(lastKey, 0, frameCount);

        _br!.BaseStream.Seek(Header.FirstFramePosition, SeekOrigin.Begin);

        while (ub-- > 0)
            yield return ReadFrame(_br);
    }

    public override IEnumerable<TFrame> GetFramesAfter(TKey firstKey)
    {
        ValidateAccess(FileAccess.Read);

        long frameCount = Header.FrameCount;
        if (frameCount == 0)
            yield break;

        long lb = GetLowerBound(firstKey, 0, frameCount);

        _br!.BaseStream.Seek(Header.FirstFramePosition + lb * Header.FrameLength, SeekOrigin.Begin);

        while (lb++ < frameCount)
            yield return ReadFrame(_br);
    }

    public override IEnumerable<TFrame> GetAllFrames()
    {
        ValidateAccess(FileAccess.Read);

        long frameCount = Header.FrameCount;
        if (frameCount == 0)
            yield break;

        _br!.BaseStream.Seek(Header.FirstFramePosition, SeekOrigin.Begin);

        while (frameCount-- > 0)
            yield return ReadFrame(_br);
    }
}
