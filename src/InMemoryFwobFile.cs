using Fwob.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                    throw new ArgumentException(nameof(Title), $"Length of argument {{0}} exceeded {FwobLimits.MaxTitleLength}");
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

        int GetLowerBound(TKey key)
        {
            int lo, hi;
            for (lo = 0, hi = _frames.Count; lo < hi;)
            {
                int mid = lo + (hi - lo >> 1);
                if (_frames[mid].Key.CompareTo(key) < 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        public override IEnumerable<TFrame> GetFrames(TKey firstKey, TKey lastKey)
        {
            Debug.Assert(firstKey.CompareTo(lastKey) <= 0);

            for (int i = GetLowerBound(firstKey); i < _frames.Count && _frames[i].Key.CompareTo(lastKey) <= 0; i++)
                yield return _frames[i];
        }

        public override IEnumerable<TFrame> GetFramesAfter(TKey firstKey)
        {
            for (int i = GetLowerBound(firstKey); i < _frames.Count; i++)
                yield return _frames[i];
        }

        public override IEnumerable<TFrame> GetFramesBefore(TKey lastKey)
        {
            for (int i = 0; i < _frames.Count && _frames[i].Key.CompareTo(lastKey) <= 0; i++)
                yield return _frames[i];
        }

        public override long AppendFrames(IEnumerable<TFrame> frames)
        {
            var first = frames.FirstOrDefault();
            if (first == null)
                return 0;

            if (_frames.Count == 0)
            {
                _frames.AddRange(frames);
                return _frames.Count;
            }

            if (first.Key.CompareTo(_frames.Last().Key) > 0)
                throw new InvalidDataException($"Frames should be in ascending order while appending.");

            var length = _frames.Count;
            _frames.AddRange(frames);
            return _frames.Count - length;
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
            return _dict[str];
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
