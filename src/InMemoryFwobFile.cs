using Mozo.Fwob.Exceptions;
using Mozo.Fwob.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mozo.Fwob;

public sealed class InMemoryFwobFile<TFrame, TKey> : AbstractFwobFile<TFrame, TKey>
    where TFrame : class, new()
    where TKey : struct, IComparable<TKey>
{
    public override string Title
    {
        get => _title;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Length == 0)
                throw new ArgumentException("Argument can not be empty", nameof(value));

            if (value.Length > Limits.MaxTitleLength)
                throw new TitleTooLongException(value, value.Length);

            _title = value;
        }
    }

    private string _title;

    public InMemoryFwobFile(string title)
    {
        _title = title;
    }

    #region Implementations of IFrameQueryable

    public override long FrameCount => _frames.Count;

    private readonly List<TFrame> _frames = new();

    protected override TKey InternalGetKeyAt(long index)
    {
        return GetKey(_frames[(int)index]);
    }

    protected override TFrame InternalGetFrameAt(long index)
    {
        return _frames[(int)index];
    }

    public override IEnumerable<TFrame> GetFrames(IEnumerable<TKey> keys)
    {
        ValidateKeys(keys, nameof(keys));

        if (_frames.Count == 0)
            yield break;

        long idx = 0;
        foreach (TKey key in keys)
        {
            (long lb, long ub) = GetEqualRange(key, idx, _frames.Count);

            if (lb == ub)
                continue;

            for (long i = lb; i < ub; i++)
                yield return InternalGetFrameAt(i);

            idx = ub;
        }
    }

    public override IEnumerable<TFrame> GetFramesBetween(TKey firstKey, TKey lastKey)
    {
        if (firstKey.CompareTo(lastKey) > 0)
            throw new ArgumentException($"{nameof(lastKey)} must be greater than or equal to {nameof(firstKey)}");

        if (_frames.Count == 0)
            yield break;

        long lb = GetLowerBound(firstKey, 0, _frames.Count);
        long ub = GetUpperBound(lastKey, lb, _frames.Count);

        while (lb < ub)
            yield return _frames[(int)lb++];
    }

    public override IEnumerable<TFrame> GetFramesBefore(TKey lastKey)
    {
        if (_frames.Count == 0)
            yield break;

        long ub = GetUpperBound(lastKey, 0, _frames.Count);

        for (int i = 0; i < ub; i++)
            yield return _frames[i];
    }

    public override IEnumerable<TFrame> GetFramesAfter(TKey firstKey)
    {
        if (_frames.Count == 0)
            yield break;

        long lb = GetLowerBound(firstKey, 0, _frames.Count);

        for (int i = (int)lb; i < _frames.Count; i++)
            yield return _frames[i];
    }

    public override IEnumerable<TFrame> GetAllFrames()
    {
        return _frames;
    }

    public override long AppendFrames(IEnumerable<TFrame> frames)
    {
        if (frames == null)
            throw new ArgumentNullException(nameof(frames));

        TFrame? last = _frames.LastOrDefault();
        long count = 0;

        foreach (TFrame frame in frames)
        {
            if (last != null && GetKey(frame).CompareTo(GetKey(last)) < 0)
                throw new KeyOrderViolationException();

            _frames.Add(frame);
            last = frame;
            count++;
        }

        return count;
    }

    public override long AppendFramesTx(IEnumerable<TFrame> frames)
    {
        if (frames == null)
            throw new ArgumentNullException(nameof(frames));

        TFrame? last = _frames.LastOrDefault();
        long count = 0;
        foreach (TFrame frame in frames)
        {
            if (last != null && GetKey(frame).CompareTo(GetKey(last)) < 0)
                throw new KeyOrderViolationException();
            last = frame;
            count++;
        }

        _frames.AddRange(frames);
        return count;
    }

    public override long DeleteFrames(IEnumerable<TKey> keys)
    {
        ValidateKeys(keys, nameof(keys));

        if (_frames.Count == 0)
            return 0;

        IEnumerator<TKey> it = keys.GetEnumerator();

        long readerIdx = 0, writerIdx = 0;

        while (readerIdx == writerIdx && it.MoveNext())
        {
            (writerIdx, readerIdx) = GetEqualRange(it.Current, readerIdx, _frames.Count);
        }

        if (readerIdx == writerIdx)
            return 0;

        while (it.MoveNext())
        {
            (long lb, long ub) = GetEqualRange(it.Current, readerIdx, _frames.Count);

            while (readerIdx < lb)
                _frames[(int)(writerIdx++)] = _frames[(int)readerIdx++];

            readerIdx = ub;
        }

        int len = _frames.Count;

        while (readerIdx < len)
            _frames[(int)writerIdx++] = _frames[(int)readerIdx++];

        _frames.RemoveRange((int)writerIdx, len - (int)writerIdx);

        return len - _frames.Count;
    }

    public override long DeleteFramesBetween(TKey firstKey, TKey lastKey)
    {
        if (firstKey.CompareTo(lastKey) > 0)
            throw new ArgumentException($"{nameof(lastKey)} must be greater than or equal to {nameof(firstKey)}");

        if (_frames.Count == 0)
            return 0;

        int lb = (int)GetLowerBound(firstKey, 0, _frames.Count);
        int ub = (int)GetUpperBound(lastKey, lb, _frames.Count);

        _frames.RemoveRange(lb, ub - lb);

        return ub - lb;
    }

    public override long DeleteFramesBefore(TKey lastKey)
    {
        if (_frames.Count == 0)
            return 0;

        int len = (int)GetUpperBound(lastKey, 0, _frames.Count);

        _frames.RemoveRange(0, len);

        return len;
    }

    public override long DeleteFramesAfter(TKey firstKey)
    {
        if (_frames.Count == 0)
            return 0;

        int lb = (int)GetLowerBound(firstKey, 0, _frames.Count);
        int len = _frames.Count - lb;

        _frames.RemoveRange(lb, len);

        return len;
    }

    public override long DeleteAllFrames()
    {
        int len = _frames.Count;

        _frames.Clear();

        return len;
    }

    #endregion

    #region Implementations of IStringTable

    public override IReadOnlyList<string> Strings => _data;

    public override int StringCount => _data.Count;

    private readonly List<string> _data = new();
    private readonly Dictionary<string, int> _dict = new();

    public override string GetString(int index)
    {
        return _data[index];
    }

    public override int GetIndex(string str)
    {
        return _dict.TryGetValue(str, out int index) ? index : -1;
    }

    public override int AppendString(string str)
    {
        int key = _dict[str] = _data.Count;
        _data.Add(str);
        return key;
    }

    public override bool ContainsString(string str)
    {
        return _dict.ContainsKey(str);
    }

    public override int DeleteAllStrings()
    {
        int len = _data.Count;
        _data.Clear();
        _dict.Clear();
        return len;
    }

    #endregion
}
