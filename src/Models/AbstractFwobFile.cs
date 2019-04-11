using System;
using System.Collections.Generic;
using System.Text;

namespace Fwob.Models
{
    public abstract class AbstractFwobFile<TFrame, TKey> : IFrameQueryable<TFrame, TKey>, IStringTable
        where TFrame : class, IFrame<TKey>
        where TKey : struct, IComparable<TKey>

    {
        public FrameInfo FrameInfo { get; protected set; }

        public abstract string Title { get; set; }

        public TFrame this[long index] => GetFrame(index);

        #region Abstract implementations of IFrameQueryable<TFrame, TKey>

        public abstract long FrameCount { get; }

        public TFrame FirstFrame => GetFrame(0);

        public TFrame LastFrame => GetFrame(FrameCount - 1);

        public abstract TFrame GetFrame(long index);

        public IEnumerable<TFrame> GetFrames(TKey key) => GetFrames(key, key);

        public abstract IEnumerable<TFrame> GetFrames(TKey firstKey, TKey lastKey);

        public abstract IEnumerable<TFrame> GetFramesAfter(TKey firstKey);

        public abstract IEnumerable<TFrame> GetFramesBefore(TKey lastKey);

        public abstract long AppendFrames(IEnumerable<TFrame> frames);

        public abstract void ClearFrames();

        #endregion

        #region Abstract implementations of IStringTable

        public abstract IReadOnlyList<string> Strings { get; }

        public abstract int StringCount { get; }

        public abstract int AppendString(string str);

        public abstract void ClearStrings();

        public abstract bool ContainsString(string str);

        public abstract int GetIndex(string str);

        public abstract string GetString(int index);

        #endregion
    }
}
