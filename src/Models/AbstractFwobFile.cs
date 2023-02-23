using Mozo.Fwob.Frame;
using System;
using System.Collections.Generic;

namespace Mozo.Fwob.Models;

public abstract class AbstractFwobFile<TFrame, TKey> : IFrameQueryable<TFrame, TKey>, IStringTable
    where TFrame : class, IFrame<TKey>, new()
    where TKey : struct, IComparable<TKey>

{
    public FrameInfo FrameInfo { get; protected set; } = new();

    public abstract string Title { get; set; }

    public TFrame? this[long index] => GetFrame(index);

    protected static readonly Func<TFrame, TKey> GetKey = FwobFrameReaderGenerator<TFrame, TKey>.GenerateKeyGetter();

    #region Abstract implementations of IFrameQueryable<TFrame, TKey>

    public abstract long FrameCount { get; }

    public virtual TFrame? FirstFrame => GetFrame(0);

    public virtual TFrame? LastFrame => GetFrame(FrameCount - 1);

    public abstract TFrame? GetFrame(long index);

    public IEnumerable<TFrame> GetFrames(TKey key) => GetFrames(key, key);

    public abstract IEnumerable<TFrame> GetFrames(TKey firstKey, TKey lastKey);

    public abstract IEnumerable<TFrame> GetFramesAfter(TKey firstKey);

    public abstract IEnumerable<TFrame> GetFramesBefore(TKey lastKey);

    public long AppendFrames(params TFrame[] frames)
    {
        return AppendFrames((IEnumerable<TFrame>)frames);
    }

    public abstract long AppendFrames(IEnumerable<TFrame> frames);

    public long AppendFramesTx(params TFrame[] frames)
    {
        return AppendFramesTx((IEnumerable<TFrame>)frames);
    }

    public abstract long AppendFramesTx(IEnumerable<TFrame> frames);

    public abstract long DeleteFramesAfter(TKey firstKey);

    public abstract long DeleteFramesBefore(TKey lastKey);

    public abstract void ClearFrames();

    #endregion

    #region Abstract implementations of IStringTable

    public abstract IReadOnlyList<string>? Strings { get; }

    public abstract int StringCount { get; }

    public abstract int AppendString(string str);

    public abstract void ClearStrings();

    public abstract bool ContainsString(string str);

    public abstract int GetIndex(string str);

    public abstract string? GetString(int index);

    #endregion
}
