using Fwob.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Fwob
{
    public class InMemoryFwobFile<TFrame, TKey> : AbstractFwobFile<TFrame, TKey>
        where TFrame : class, IFrame<TKey>
        where TKey : struct, IComparable<TKey>
    {
        public override string Title
        {
            get => title;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(Title));
                if (value.Length > FwobLimits.MaxTitleLength)
                    throw new ArgumentException(nameof(Title), $"Length of argument exceeded {FwobLimits.MaxTitleLength}");
                title = value;
            }
        }

        private string title;

        public InMemoryFwobFile(string title)
        {
            Title = title;
            FrameInfo = FrameInfo.FromSystem(typeof(TFrame));
        }

        #region Implementations of IFrameQueryable

        public override long FrameCount => _frames.Count;

        private readonly List<TFrame> _frames = new List<TFrame>();

        public override TFrame GetFrame(long index)
        {
            if (index < 0 || index >= FrameCount)
                return null;
            return _frames[(int)index];
        }

        private int GetBound(TKey key, bool lower)
        {
            int lo, hi;
            for (lo = 0, hi = _frames.Count; lo < hi;)
            {
                int mid = lo + (hi - lo >> 1);
                int cmp = _frames[mid].Key.CompareTo(key);
                if (lower ? cmp < 0 : cmp <= 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        public override IEnumerable<TFrame> GetFrames(TKey firstKey, TKey lastKey)
        {
            Debug.Assert(firstKey.CompareTo(lastKey) <= 0);

            for (int i = GetBound(firstKey, true); i < _frames.Count && _frames[i].Key.CompareTo(lastKey) <= 0; i++)
                yield return _frames[i];
        }

        public override IEnumerable<TFrame> GetFramesAfter(TKey firstKey)
        {
            for (int i = GetBound(firstKey, true); i < _frames.Count; i++)
                yield return _frames[i];
        }

        public override IEnumerable<TFrame> GetFramesBefore(TKey lastKey)
        {
            for (int i = 0; i < _frames.Count && _frames[i].Key.CompareTo(lastKey) <= 0; i++)
                yield return _frames[i];
        }

        public override long AppendFrames(IEnumerable<TFrame> frames)
        {
            if (frames == null)
                throw new ArgumentNullException(nameof(frames));

            var last = _frames.LastOrDefault();
            long count = 0;
            foreach (var frame in frames)
            {
                if (last != null && frame.Key.CompareTo(last.Key) < 0)
                    throw new KeyOrderingException($"Frames should be in ascending order while appending.");
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

            var last = _frames.LastOrDefault();
            long count = 0;
            foreach (var frame in frames)
            {
                if (last != null && frame.Key.CompareTo(last.Key) < 0)
                    throw new KeyOrderingException($"Frames should be in ascending order while appending.");
                last = frame;
                count++;
            }

            _frames.AddRange(frames);
            return count;
        }

        public override long DeleteFramesAfter(TKey firstKey)
        {
            int lb = GetBound(firstKey, true), len = _frames.Count - lb;
            _frames.RemoveRange(lb, len);
            return len;
        }

        public override long DeleteFramesBefore(TKey lastKey)
        {
            int len = GetBound(lastKey, false);
            _frames.RemoveRange(0, len);
            return len;
        }

        public override void ClearFrames()
        {
            _frames.Clear();
        }

        #endregion

        #region Implementations of IStringTable

        public override IReadOnlyList<string> Strings => _data;

        public override int StringCount => _data.Count;

        private readonly List<string> _data = new List<string>();
        private readonly Dictionary<string, int> _dict = new Dictionary<string, int>();

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
            if (_dict.TryGetValue(str, out int key))
                return key;
            _dict[str] = key = _data.Count;
            _data.Add(str);
            return key;
        }

        public override bool ContainsString(string str)
        {
            return _dict.ContainsKey(str);
        }

        public override void ClearStrings()
        {
            _data.Clear();
            _dict.Clear();
        }

        #endregion
    }
}
