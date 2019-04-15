using Fwob.Header;
using Fwob.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Fwob.Extensions;

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
        where TFrame : class, IFrame<TKey>, new()
        where TKey : struct, IComparable<TKey>
    {
        public override string Title
        {
            get => Header.Title;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(Title));
                if (value.Length == 0)
                    throw new ArgumentException(nameof(Title), $"Argument {{0}} can not be empty");
                if (value.Length > FwobLimits.MaxTitleLength)
                    throw new ArgumentOutOfRangeException(nameof(Title), $"Length of argument {{0}} exceeded {FwobLimits.MaxTitleLength}");
                Header.Title = value;
                using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
                    bw.UpdateTitle(Header);
            }
        }

        public string FilePath { get; private set; }

        public Stream Stream { get; private set; }

        public FwobHeader Header { get; private set; }

        bool IsFileOpen => Stream != null && FilePath != null && Header != null;

        public static FwobFile<TFrame, TKey> CreateNewFile(string path, string title, FileMode mode = FileMode.CreateNew, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read)
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));
            if (title.Length == 0)
                throw new ArgumentException(nameof(title), $"Argument {{0}} can not be empty");
            if (title.Length > FwobLimits.MaxTitleLength)
                throw new ArgumentOutOfRangeException(nameof(title), $"Length of argument {{0}} exceeded {FwobLimits.MaxTitleLength}");

            var file = new FwobFile<TFrame, TKey>
            {
                FrameInfo = FrameInfo.FromSystem<TFrame>(),
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

                if (FrameCount > 0)
                {
                    _firstFrame = GetFrame(0);
                    _lastFrame = GetFrame(FrameCount - 1);
                }
            }
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (Stream != null)
            {
                Stream.Close();
                Stream.Dispose();
                Stream = null;
            }
            UnloadStringTable();
            _firstFrame = null;
            _lastFrame = null;
            FilePath = null;
            Header = null;
        }

        #region Implementations of IFrameQueryable

        // Cached first and last frames
        TFrame _firstFrame = null, _lastFrame = null;

        public override TFrame FirstFrame => _firstFrame;

        public override TFrame LastFrame => _lastFrame;

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

                return DeserializeFrame(br);
            }
        }

        long GetBound(BinaryReader br, TKey key, bool lower)
        {
            Debug.Assert(br.BaseStream.Length == Header.FileLength);

            long pos = Header.FirstFramePosition;

            long lo, hi;
            for (lo = 0, hi = Header.FrameCount; lo < hi;)
            {
                long mid = lo + (hi - lo >> 1);
                br.BaseStream.Seek(pos + mid * Header.FrameLength, SeekOrigin.Begin);

                int cmp = br.Read<TKey>().CompareTo(key);
                if (lower ? cmp < 0 : cmp <= 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        readonly System.Reflection.FieldInfo[] _fields = typeof(TFrame).GetFields();
        readonly int[] _lengths = typeof(TFrame).GetFields()
            .Select(o => o.GetCustomAttributes(typeof(LengthAttribute), false))
            .Select(o => (LengthAttribute)o.FirstOrDefault())
            .Select(o => o?.Length ?? 0)
            .ToArray();

        private void SerializeFrame(BinaryWriter bw, TFrame frame)
        {
            for (int i = 0; i < _fields.Length; i++)
            {
                var field = _fields[i];
                var length = _lengths[i];

                if (length > 0) // only string type defines length
                {
                    var str = (string)field.GetValue(frame);
                    if (str?.Length > length)
                        throw new InvalidDataException($"Length of field {field.FieldType.Name} is greater than defined length {length} while serializing a frame.");
                    bw.Write((str ?? string.Empty).PadRight(length).ToCharArray());
                }
                else
                {
                    bw.Write(field.GetValue(frame));
                }
            }
        }

        private TFrame DeserializeFrame(BinaryReader br)
        {
            var frame = new TFrame();

            for (int i = 0; i < _fields.Length; i++)
            {
                var field = _fields[i];
                var length = _lengths[i];

                object val;
                if (length > 0) // only string type defines length
                    val = new string(br.ReadChars(length)).TrimEnd();
                else
                    val = br.Read(field.FieldType);

                field.SetValue(frame, val);
            }

            return frame;
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
                    var frame = DeserializeFrame(br);

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
                    yield return DeserializeFrame(br);
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
                    yield return DeserializeFrame(br);
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
                bw.Seek((int)Header.LastFramePosition, SeekOrigin.Begin);

                var last = LastFrame;
                long count = 0;
                do
                {
                    if (last != null && it.Current.Key.CompareTo(last.Key) < 0)
                    {
                        _lastFrame = last;
                        Header.FrameCount += count;
                        bw.UpdateFrameCount(Header);
                        throw new InvalidDataException($"Frames should be in ascending order while appending.");
                    }

                    SerializeFrame(bw, it.Current);
                    last = it.Current;
                    if (_firstFrame == null)
                        _firstFrame = last;
                    count++;
                }
                while (it.MoveNext());

                _lastFrame = last;
                Header.FrameCount += count;
                bw.UpdateFrameCount(Header);
                return count;
            }
        }

        public override long AppendFramesTx(IEnumerable<TFrame> frames)
        {
            if (frames == null)
                throw new ArgumentNullException(nameof(frames));

            Debug.Assert(IsFileOpen);

            var last = LastFrame;
            var list = new List<TFrame>();
            foreach (var frame in frames)
            {
                if (last != null && frame.Key.CompareTo(last.Key) < 0)
                    throw new InvalidDataException($"Frames should be in ascending order while appending.");
                last = frame;
                list.Add(frame);
            }

            if (list.Count == 0)
                return 0;

            using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
            {
                Debug.Assert(bw.BaseStream.Length == Header.FileLength);
                bw.Seek((int)Header.LastFramePosition, SeekOrigin.Begin);

                foreach (var frame in list)
                    SerializeFrame(bw, frame);

                if (_firstFrame == null)
                    _firstFrame = list[0];
                _lastFrame = last;
                Header.FrameCount += list.Count;
                bw.UpdateFrameCount(Header);
                return list.Count;
            }
        }

        public override void ClearFrames()
        {
            Debug.Assert(IsFileOpen);

            if (Header.FrameCount == 0)
                return;

            using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
            {
                Debug.Assert(bw.BaseStream.Length > Header.FirstFramePosition);
                bw.BaseStream.SetLength(Header.FirstFramePosition);

                _firstFrame = _lastFrame = null;
                Header.FrameCount = 0;
                bw.UpdateFrameCount(Header);
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

        public void UnloadStringTable()
        {
            if (!IsStringsLoaded)
                return;

            _strings.Clear();
            _stringDict.Clear();
            _strings = null;
            _stringDict = null;
        }

        public override IReadOnlyList<string> Strings => _strings;

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
                throw new ArgumentOutOfRangeException();

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

            var bytes = Encoding.UTF8.GetByteCount(str);
            int length = bytes < 128 ? 1 : bytes < 128 * 128 ? 2 : bytes < 128 * 128 * 128 ? 3 : 4;
            // A string is serialized with a 7-bit encoded integer prefix
            // 1 byte  length prefix: 00~7f (0~128-1)
            // 2 bytes length prefix: 8001~ff01~8002~807f~ff7f (128~128^2-1)
            // 3 bytes length prefix: 808001~ff8001~808101~807f01~ff7f01~808002~ffff7f (128^2~128^3-1)
            // 4 bytes length prefix: 80808001~ffffff7f (128^3~128^4-1)
            if (Header.StringTableLength + bytes + length > Header.StringTablePreservedLength)
                throw new InternalBufferOverflowException("No more preserved space for appending string");

            using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
            {
                Debug.Assert(bw.BaseStream.Length >= Header.FirstFramePosition);
                bw.BaseStream.Seek(Header.StringTableEnding, SeekOrigin.Begin);
                bw.Write(str);

                index = Header.StringCount++;
                Header.StringTableLength = (int)(bw.BaseStream.Position - Header.StringTablePosition);
                bw.UpdateStringTableLength(Header);
            }

            if (IsStringsLoaded)
            {
                _stringDict[str] = _strings.Count;
                _strings.Add(str);
            }

            return index;
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
                Header.StringCount = 0;
                Header.StringTableLength = 0;
                bw.UpdateStringTableLength(Header);
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
