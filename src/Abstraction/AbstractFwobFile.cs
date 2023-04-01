using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Mozo.Fwob.Abstraction;

public abstract class AbstractFwobFile<TFrame, TKey> : IFrameCollection<TFrame, TKey>, IStringTable
    where TFrame : class, new()
    where TKey : struct, IComparable<TKey>

{
    public FrameInfo FrameInfo { get; } = FrameInfo.FromTypes<TFrame, TKey>();

    protected static readonly FrameInfo _FrameInfo = FrameInfo.FromTypes<TFrame, TKey>();

    public abstract string Title { get; set; }

    public TFrame? this[long index] => GetFrameAt(index);

    public static readonly Func<TFrame, TKey> GetKey = GenerateKeyGetter();

    /// <summary>
    /// Dynamically generate a function that gets the key of the given frame<br/>
    /// function: TKey GetKey(TFrame frame)
    /// </summary>
    /// <returns></returns>
    private static Func<TFrame, TKey> GenerateKeyGetter()
    {
        ParameterExpression frame = Expression.Parameter(typeof(TFrame), nameof(frame)); // (TFrame frame)
        MemberExpression field = Expression.Field(frame, _FrameInfo.SystemKeyFieldInfo); // frame.KeyField

        var lambda = Expression.Lambda<Func<TFrame, TKey>>(field, frame); // (TFrame frame) => frame.KeyField

        return lambda.Compile();
    }

    #region Implementations of IEnumerable<TFrame>

    public IEnumerator<TFrame> GetEnumerator()
    {
        return GetAllFrames().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetAllFrames().GetEnumerator();
    }

    #endregion

    #region Abstract implementations of IFrameQueryable<TFrame, TKey>

    public abstract long FrameCount { get; }

    public virtual TFrame? FirstFrame => GetFrameAt(0);

    public virtual TFrame? LastFrame => GetFrameAt(FrameCount - 1);

    protected static void ValidateKeys(IEnumerable<TKey> keys, string paramName)
    {
        if (keys == null)
            throw new ArgumentNullException(paramName);

        bool first = true;
        TKey last = default;

        foreach (TKey key in keys)
        {
            if (first)
                first = false;
            else
            {
                int cmp = last.CompareTo(key);
                if (cmp > 0)
                    throw new ArgumentException("Argument must be an ordered list", paramName);
                if (cmp == 0)
                    throw new ArgumentException("Argument contains duplicate items", paramName);
            }
            last = key;
        }
    }

    protected long GetLowerBound(TKey key, long begin, long end)
    {
        long lo, hi;
        for (lo = begin, hi = end; lo < hi;)
        {
            long mid = lo + (hi - lo >> 1);
            int cmp = InternalGetKeyAt(mid).CompareTo(key);
            if (cmp < 0)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    protected long GetUpperBound(TKey key, long begin, long end)
    {
        long lo, hi;
        for (lo = begin, hi = end; lo < hi;)
        {
            long mid = lo + (hi - lo >> 1);
            int cmp = InternalGetKeyAt(mid).CompareTo(key);
            if (cmp <= 0)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    protected (long LowerBound, long UpperBound) GetEqualRange(TKey key, long begin, long end)
    {
        long lo, hi, ub = end;
        for (lo = begin, hi = end; lo < hi;)
        {
            long mid = lo + (hi - lo >> 1);
            int cmp = InternalGetKeyAt(mid).CompareTo(key);
            if (cmp < 0)
                lo = mid + 1;
            else if (cmp > 0)
                hi = ub = mid;
            else
                hi = mid;
        }

        long lb = lo;
        for (hi = ub; lo < hi;)
        {
            long mid = lo + (hi - lo >> 1);
            int cmp = InternalGetKeyAt(mid).CompareTo(key);
            if (cmp <= 0)
                lo = mid + 1;
            else
                hi = mid;
        }
        return (lb, hi);
    }

    public virtual long LowerBoundOf(TKey key)
    {
        return GetLowerBound(key, 0, FrameCount);
    }

    public virtual long UpperBoundOf(TKey key)
    {
        return GetUpperBound(key, 0, FrameCount);
    }

    public virtual (long LowerBound, long UpperBound) EqualRangeOf(TKey key)
    {
        return GetEqualRange(key, 0, FrameCount);
    }

    /// <summary>
    /// Get the key of the <typeparamref name="TFrame"/> instance at the given position, assuming the <paramref name="index"/> is valid.
    /// </summary>
    /// <param name="index">An integer representing the position of a <typeparamref name="TFrame"/> instance.</param>
    /// <returns>The key of the <typeparamref name="TFrame"/> instance at the given position.</returns>
    protected abstract TKey InternalGetKeyAt(long index);

    /// <summary>
    /// Get the <typeparamref name="TFrame"/> instance at the given position, assuming the <paramref name="index"/> is valid.
    /// </summary>
    /// <param name="index">An integer representing the position of a <typeparamref name="TFrame"/> instance.</param>
    /// <returns>The key of the <typeparamref name="TFrame"/> instance at the given position.</returns>
    protected abstract TFrame InternalGetFrameAt(long index);

    public virtual TKey? GetKeyAt(long index)
    {
        if (index < 0 || index >= FrameCount)
            return null;

        return InternalGetKeyAt(index);
    }

    public virtual TFrame? GetFrameAt(long index)
    {
        if (index < 0 || index >= FrameCount)
            return null;

        return InternalGetFrameAt(index);
    }

    public IEnumerable<TFrame> GetFrames(params TKey[] keys) => GetFrames((IEnumerable<TKey>)keys);

    public abstract IEnumerable<TFrame> GetFrames(IEnumerable<TKey> keys);

    public abstract IEnumerable<TFrame> GetFramesBetween(TKey firstKey, TKey lastKey);

    public abstract IEnumerable<TFrame> GetFramesBefore(TKey lastKey);

    public abstract IEnumerable<TFrame> GetFramesAfter(TKey firstKey);

    public abstract IEnumerable<TFrame> GetAllFrames();

    #endregion

    #region Abstract implementations of IFrameCollection<TFrame, TKey>

    public long AppendFrames(params TFrame[] frames) => AppendFrames((IEnumerable<TFrame>)frames);

    public abstract long AppendFrames(IEnumerable<TFrame> frames);

    public long AppendFramesTx(params TFrame[] frames) => AppendFramesTx((IEnumerable<TFrame>)frames);

    public abstract long AppendFramesTx(IEnumerable<TFrame> frames);

    public long DeleteFrames(params TKey[] keys) => DeleteFrames((IEnumerable<TKey>)keys);

    public abstract long DeleteFrames(IEnumerable<TKey> keys);

    public abstract long DeleteFramesBetween(TKey firstKey, TKey lastKey);

    public abstract long DeleteFramesBefore(TKey lastKey);

    public abstract long DeleteFramesAfter(TKey firstKey);

    public abstract long DeleteAllFrames();

    #endregion

    #region Abstract implementations of IStringTable

    public abstract IReadOnlyList<string>? Strings { get; }

    public abstract int StringCount { get; }

    public abstract int AppendString(string str);

    public abstract int DeleteAllStrings();

    public abstract bool ContainsString(string str);

    public abstract int GetIndex(string str);

    public abstract string? GetString(int index);

    #endregion
}
