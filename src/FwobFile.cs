using Fwob.Header;
using Fwob.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Fwob
{
    /// <summary>
    /// FWOB ver1 string table definition
    /// A Fixed-Width Ordered Binary (FWOB) file consisits of 3 sections:
    ///   1 Header: pos 0: 214 bytes
    ///   2 String Table: pos 214: length StringTablePreservedLength
    ///   3 Data Frames: pos 214 + StringTablePreservedLength, length FrameCount * FrameLength
    /// </summary>
    public class FwobFile<TFrame, TKey> : AbstractFwobFile<TFrame, TKey>, IDisposable
        where TFrame : class, ISerializableFrame<TKey>, new()
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

        public string FilePath { get; private set; }

        public Stream Stream { get; private set; }

        public FwobHeader Header { get; private set; }

        bool IsFileOpen => Stream != null && FilePath != null && Header != null;

        public static FwobFile<TFrame, TKey> CreateNewFile(string path, string title, FileMode mode = FileMode.CreateNew, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read)
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));
            if (title.Length > FwobLimits.MaxTitleLength)
                throw new ArgumentOutOfRangeException(nameof(title), $"Length of argument {{0}} exceeded {FwobLimits.MaxTitleLength}");

            var file = new FwobFile<TFrame, TKey>
            {
                FrameInfo = FrameInfo.FromSystem<TFrame>(),
                Title = title,
                FilePath = path,
                Stream = new FileStream(path, mode, access, share),
                Header = FwobHeader.CreateNew<TFrame>(title),
            };

            using (var bw = new BinaryWriter(file.Stream, Encoding.UTF8, true))
            {
                bw.WriteHeader(file.Header);
                bw.Write(new byte[file.Header.StringTablePreservedLength]);
            }

            return file;
        }

        private FwobFile() { }

        public FwobFile(string path, FileAccess fileAccess = FileAccess.ReadWrite, FileShare fileShare = FileShare.Read)
        {
            FilePath = path;
            Stream = new FileStream(path, FileMode.Open, fileAccess, fileShare);

            using (var br = new BinaryReader(Stream, Encoding.UTF8, true))
            {
                Header = br.ReadHeader();

                if (Header == null)
                    throw new InvalidDataException($"Input file {path} is not in a FWOB format.");

                FrameInfo = Header.GetFrameInfo<TFrame>();

                if (FrameInfo == null)
                    throw new InvalidDataException($"Input file {path} does not match frame type {typeof(TFrame)}.");

                if (Header.FileLength != br.BaseStream.Length)
                    throw new InvalidDataException($"Input file {path} length verification failed.");

                title = Header.Title;
            }
        }

        public void Close()
        {
            Dispose();
            FilePath = null;
        }

        public void Dispose()
        {
            if (Stream != null)
            {
                Stream.Close();
                Stream.Dispose();
                Stream = null;
            }
        }

        #region Implementations of IFrameQueryable

        public override long FrameCount
        {
            get
            {
                Debug.Assert(IsFileOpen);
                return Header.FrameCount;
            }
        }

        public override TFrame GetFrame(long index)
        {
            Debug.Assert(IsFileOpen);

            if (index < 0 || index >= Header.FrameCount)
                return null;

            using (var br = new BinaryReader(Stream, Encoding.UTF8, true))
            {
                Debug.Assert(br.BaseStream.Length == Header.FileLength);
                br.BaseStream.Seek(Header.FirstFramePosition + index * Header.FrameLength, SeekOrigin.Begin);

                var frame = new TFrame();
                frame.DeserializeFrame(br);
                return frame;
            }
        }

        long GetBound(BinaryReader br, TKey key, bool lower)
        {
            Debug.Assert(br.BaseStream.Length == Header.FileLength);

            long pos = Header.FirstFramePosition;
            var frame = new TFrame();

            long lo, hi;
            for (lo = 0, hi = Header.FrameCount; lo < hi;)
            {
                long mid = lo + (hi - lo >> 1);
                br.BaseStream.Seek(pos + mid * Header.FrameLength, SeekOrigin.Begin);

                int cmp = frame.DeserializeKey(br).CompareTo(key);
                if (lower && cmp < 0 || cmp <= 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        public override IEnumerable<TFrame> GetFrames(TKey firstKey, TKey lastKey)
        {
            Debug.Assert(IsFileOpen);
            Debug.Assert(firstKey.CompareTo(lastKey) <= 0);

            if (Header.FrameCount == 0)
                yield break;

            using (var br = new BinaryReader(Stream, Encoding.UTF8, true))
            {
                long p = GetBound(br, firstKey, true);

                br.BaseStream.Seek(Header.FirstFramePosition + p * Header.FrameLength, SeekOrigin.Begin);

                for (; p < Header.FrameCount; p++)
                {
                    var frame = new TFrame();
                    frame.DeserializeFrame(br);

                    if (frame.Key.CompareTo(lastKey) > 0)
                        yield break;

                    yield return frame;
                }
            }
        }

        public override IEnumerable<TFrame> GetFramesAfter(TKey firstKey)
        {
            Debug.Assert(IsFileOpen);

            if (Header.FrameCount == 0)
                yield break;

            using (var br = new BinaryReader(Stream, Encoding.UTF8, true))
            {
                long p = GetBound(br, firstKey, true);

                br.BaseStream.Seek(Header.FirstFramePosition + p * Header.FrameLength, SeekOrigin.Begin);

                for (; p < Header.FrameCount; p++)
                {
                    var frame = new TFrame();
                    frame.DeserializeFrame(br);
                    yield return frame;
                }
            }
        }

        public override IEnumerable<TFrame> GetFramesBefore(TKey lastKey)
        {
            Debug.Assert(IsFileOpen);

            if (Header.FrameCount == 0)
                yield break;

            using (var br = new BinaryReader(Stream, Encoding.UTF8, true))
            {
                long p = GetBound(br, lastKey, false);

                br.BaseStream.Seek(Header.FirstFramePosition, SeekOrigin.Begin);

                for (; p > 0; p--)
                {
                    var frame = new TFrame();
                    frame.DeserializeFrame(br);
                    yield return frame;
                }
            }
        }

        public override long AppendFrames(IEnumerable<TFrame> frames)
        {
            if (frames == null)
                throw new ArgumentNullException(nameof(frames));

            Debug.Assert(IsFileOpen);

            var it = frames.GetEnumerator();
            if (!it.MoveNext())
                return 0;

            using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
            {
                Debug.Assert(bw.BaseStream.Length == Header.FileLength);

                var last = LastFrame;
                long count = 0;
                do
                {
                    if (it.Current.Key.CompareTo(last.Key) < 0)
                        throw new InvalidOperationException("Frame keys must be greater than or equal to key of the existing tail frame.");

                    it.Current.SerializeFrame(bw);
                    last = it.Current;
                    count++;
                }
                while (it.MoveNext());

                Header.FrameCount += count;
                return count;
            }
        }

        public override void ClearFrames()
        {
            Debug.Assert(IsFileOpen);

            if (Header.FrameCount == 0)
                return;

            using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
            {
                Debug.Assert(bw.BaseStream.Length >= Header.FirstFramePosition);
                bw.BaseStream.SetLength(Header.FirstFramePosition);

                Header.FrameCount = 0;
            }
        }

        #endregion

        #region Implementations of IStringTable

        bool IsStringsLoaded => _strings != null;
        List<string> _strings = null;
        Dictionary<string, int> _stringDict = null;

        public void LoadStringTable()
        {
            if (IsStringsLoaded)
                return;

            Debug.Assert(IsFileOpen);

            using (var br = new BinaryReader(Stream, Encoding.UTF8, true))
            {
                Debug.Assert(br.BaseStream.Length >= Header.FirstFramePosition);
                br.BaseStream.Seek(Header.StringTablePosition, SeekOrigin.Begin);

                var list = new List<string>();
                var dict = new Dictionary<string, int>();

                for (int i = 0; i < Header.StringCount; i++)
                {
                    var str = br.ReadString();
                    dict[str] = list.Count;
                    list.Add(str);
                }

                if (br.BaseStream.Position != Header.StringTableEnding)
                    throw new Exception("String table length inconsistent with header");

                _strings = list;
                _stringDict = dict;
            }
        }

        public override IReadOnlyList<string> Strings
        {
            get
            {
                LoadStringTable();
                return _strings;
            }
        }

        public override int StringCount
        {
            get
            {
                if (IsStringsLoaded)
                    return _strings.Count;
                Debug.Assert(IsFileOpen);
                return Header.StringCount;
            }
        }

        IEnumerable<(int index, string str)> LookupStringInFile()
        {
            Debug.Assert(IsFileOpen);

            using (var br = new BinaryReader(Stream, Encoding.UTF8, true))
            {
                Debug.Assert(br.BaseStream.Length >= Header.FirstFramePosition);
                br.BaseStream.Seek(Header.StringTablePosition, SeekOrigin.Begin);

                for (int i = 0; i < Header.StringCount; i++)
                    yield return (i, br.ReadString());
            }
        }

        public override string GetString(int index)
        {
            if (index < 0 || index >= Header.StringCount)
                throw new IndexOutOfRangeException();

            if (IsStringsLoaded)
                return _strings[index];

            Debug.Assert(Header != null);

            foreach (var (idx, str) in LookupStringInFile())
                if (idx == index)
                    return str;

            return null;
        }

        public override int GetIndex(string str)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (IsStringsLoaded)
                return _stringDict.TryGetValue(str, out int index) ? index : -1;

            foreach (var (idx, s) in LookupStringInFile())
                if (s == str)
                    return idx;

            return -1;
        }

        public override int AppendString(string str)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));

            int index = GetIndex(str);

            if (index != -1)
                return index;

            Debug.Assert(IsFileOpen);

            if (Header.StringTableLength + Encoding.UTF8.GetByteCount(str) + Marshal.SizeOf(str.Length) > Header.StringTablePreservedLength)
                throw new InternalBufferOverflowException("No more preserved space for appending string");

            using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
            {
                Debug.Assert(bw.BaseStream.Length >= Header.FirstFramePosition);
                bw.BaseStream.Seek(Header.StringTableEnding, SeekOrigin.Begin);
                bw.Write(str);

                Header.StringCount++;
            }

            if (IsStringsLoaded)
            {
                _stringDict[str] = _strings.Count;
                _strings.Add(str);
            }
            return _strings.Count - 1;
        }

        public override bool ContainsString(string str)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (IsStringsLoaded)
                return _stringDict.ContainsKey(str);

            foreach (var (idx, s) in LookupStringInFile())
                if (s == str)
                    return true;

            return false;
        }

        public override void ClearStrings()
        {
            Debug.Assert(IsFileOpen);

            using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
            {
                bw.BaseStream.Seek(158, SeekOrigin.Begin);
                bw.Write(0); // StringCount
                Header.StringCount = 0;

                bw.Write(0); // StringTableLength
                Header.StringTableLength = 0;
            }

            if (IsStringsLoaded)
            {
                _stringDict.Clear();
                _strings.Clear();
            }
        }

        #endregion
    }
}
