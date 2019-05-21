using Fwob.Header;
using Fwob.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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
                    throw new ArgumentException(nameof(Title), "Argument can not be empty");
                if (value.Length > FwobLimits.MaxTitleLength)
                    throw new ArgumentOutOfRangeException(nameof(Title), $"Length of argument exceeded {FwobLimits.MaxTitleLength}");
                Header.Title = value;
                using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
                    bw.UpdateTitle(Header);
            }
        }

        public string FilePath { get; private set; }

        public Stream Stream { get; private set; }

        public FwobHeader Header { get; private set; }

        private bool IsFileOpen => Stream != null && FilePath != null && Header != null;

        private const int BlockCopyBufSize = 4 * 1024 * 1024;

        public static FwobFile<TFrame, TKey> CreateNewFile(string path, string title, FileMode mode = FileMode.CreateNew, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read)
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));
            if (title.Length == 0)
                throw new ArgumentException(nameof(title), "Argument can not be empty");
            if (title.Length > FwobLimits.MaxTitleLength)
                throw new ArgumentOutOfRangeException(nameof(title), $"Length of argument exceeded {FwobLimits.MaxTitleLength}");

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

        /// <summary>
        /// Split a fwob file into multiple segments with <paramref name="firstKeys"/>
        /// being the first key of a segment (except the first segment).
        /// </summary>
        /// <param name="srcPath">File path to be loaded and splitted.</param>
        /// <param name="firstKeys">Keys that end a segment.</param>
        public static void Split(string srcPath, params TKey[] firstKeys)
        {
            if (srcPath == null)
                throw new ArgumentNullException(nameof(srcPath));

            if (firstKeys == null)
                throw new ArgumentNullException(nameof(firstKeys));

            if (firstKeys.Length == 0)
                throw new ArgumentException("Argument must contain at least one separating key", nameof(firstKeys));

            for (int i = 1; i < firstKeys.Length; i++)
                if (firstKeys[i].CompareTo(firstKeys[i - 1]) <= 0)
                    throw new KeyOrderingException($"Argument {nameof(firstKeys)} must be in strictly ascending order");

            if (!File.Exists(srcPath))
                throw new FileNotFoundException("Fwob file not found", srcPath);

            using (var srcFile = new FwobFile<TFrame, TKey>(srcPath, FileAccess.Read))
            {
                if (srcFile.FrameCount == 0)
                    throw new FrameNotFoundException($"Fwob file {srcFile} is empty");

                if (firstKeys.First().CompareTo(srcFile.FirstFrame.Key) < 0)
                    throw new ArgumentException("First item in argument beyonds file beginning", nameof(firstKeys));

                if (firstKeys.Last().CompareTo(srcFile.LastFrame.Key) > 0)
                    throw new ArgumentException("Last item in argument beyonds file ending", nameof(firstKeys));

                using (var br = new BinaryReader(srcFile.Stream, Encoding.UTF8, true))
                {
                    long[] firstIndices = new[] { 0L }
                        .Concat(firstKeys.Select(key => srcFile.GetBound(br, key, true)))
                        .Concat(new[] { srcFile.FrameCount })
                        .ToArray();

                    long framesWritten = 0;

                    for (int i = 0; i < firstIndices.Length - 1; i++)
                    {
                        string dstPath = Path.ChangeExtension(srcPath, $".part{i}.fwob");

                        long frameCount = firstIndices[i + 1] - firstIndices[i];
                        long dataPosition = srcFile.Header.FirstFramePosition + firstIndices[i] * srcFile.Header.FrameLength;
                        long dataLength = frameCount * srcFile.Header.FrameLength;

                        using (var stream = new FileStream(dstPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (var bw = new BinaryWriter(stream))
                        {
                            // clone header and string table
                            br.BaseStream.Seek(0, SeekOrigin.Begin);
                            bw.Write(br.ReadBytes((int)srcFile.Header.FirstFramePosition));

                            // clone frame data
                            br.BaseStream.Seek(dataPosition, SeekOrigin.Begin);

                            for (long k = dataLength; k > 0; k -= BlockCopyBufSize)
                                bw.Write(br.ReadBytes((int)Math.Min(k, BlockCopyBufSize)));

                            // update frame count
                            bw.UpdateFrameCount(new FwobHeader { FrameCount = frameCount });
                            framesWritten += frameCount;
                        }
                    }

                    Debug.Assert(framesWritten == srcFile.FrameCount, $"Frames written inconsistent: {framesWritten} != {srcFile.FrameCount}");
                }
            }
        }

        public static void Concat(string dstPath, params string[] srcPaths)
        {
            if (dstPath == null)
                throw new ArgumentNullException(nameof(dstPath));

            if (srcPaths == null)
                throw new ArgumentNullException(nameof(srcPaths));

            if (srcPaths.Length == 0)
                throw new ArgumentException("Argument must contain at least one file path", nameof(srcPaths));

            foreach (string path in srcPaths)
                if (!File.Exists(path))
                    throw new FileNotFoundException("File not found", path);

            var fileList = new List<FwobFile<TFrame, TKey>>();
            try
            {
                var strings = new List<string>();
                FwobFile<TFrame, TKey> patFile = null;

                foreach (string path in srcPaths)
                {
                    var file = new FwobFile<TFrame, TKey>(path, FileAccess.Read, FileShare.Read);
                    var prevFile = fileList.LastOrDefault();
                    fileList.Add(file);

                    if (file.FrameCount == 0)
                        throw new FrameNotFoundException($"File {path} is empty.");

                    if (prevFile != null)
                    {
                        if (file.FirstFrame.Key.CompareTo(prevFile.LastFrame.Key) < 0)
                            throw new KeyOrderingException($"First frame of {path} must be >= last frame in previous file.");
                        if (file.Title != prevFile.Title)
                            throw new FileTitleException($"Title of {path} must be identical as previous file.");
                    }

                    file.LoadStringTable();
                    for (int i = Math.Min(strings.Count, file.Strings.Count) - 1; i >= 0; i--)
                        if (file.Strings[i] != strings[i])
                            throw new StringTableException($"String table of {path} incompatible at {i} with previous files ({file.Strings[i]}, {strings[i]}).");
                    if (file.StringCount > strings.Count)
                        patFile = file;
                    for (int i = strings.Count; i < file.Strings.Count; i++)
                        strings.Add(file.Strings[i]);
                    file.UnloadStringTable();
                }

                using (var stream = new FileStream(dstPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(stream))
                {
                    // clone header and string table from pattern file
                    using (var br = new BinaryReader(patFile.Stream, Encoding.UTF8, true))
                    {
                        br.BaseStream.Seek(0, SeekOrigin.Begin);
                        bw.Write(br.ReadBytes((int)patFile.Header.FirstFramePosition));
                    }

                    // clone frame data from each source
                    long frameCount = 0;
                    foreach (var file in fileList)
                    {
                        using (var br = new BinaryReader(file.Stream))
                        {
                            br.BaseStream.Seek(file.Header.FirstFramePosition, SeekOrigin.Begin);

                            long dataLength = file.FrameCount * file.Header.FrameLength;
                            frameCount += file.FrameCount;
                            for (long k = dataLength; k > 0; k -= BlockCopyBufSize)
                                bw.Write(br.ReadBytes((int)Math.Min(k, BlockCopyBufSize)));
                        }
                        file.Dispose();
                    }

                    // update frame count
                    bw.UpdateFrameCount(new FwobHeader { FrameCount = frameCount });
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                foreach (var file in fileList)
                    file.Dispose();
            }
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
        private TFrame _firstFrame = null, _lastFrame = null;

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

        private long GetBound(BinaryReader br, TKey key, bool lower)
        {
            Debug.Assert(br.BaseStream.Length == Header.FileLength);

            long pos = Header.FirstFramePosition;

            long lo, hi;
            for (lo = 0, hi = Header.FrameCount; lo < hi;)
            {
                long mid = lo + (hi - lo >> 1);
                br.BaseStream.Seek(pos + mid * Header.FrameLength, SeekOrigin.Begin);

                int cmp = DeserializeKey(br).CompareTo(key);
                if (lower ? cmp < 0 : cmp <= 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        private static Action<BinaryWriter, TFrame> GenerateFrameSerializer()
        {
            var bw = Expression.Parameter(typeof(BinaryWriter), "bw");
            var frame = Expression.Parameter(typeof(TFrame), "frame");

            var expressions = new List<Expression>();

            foreach (var fieldInfo in typeof(TFrame).GetFields())
            {
                var fieldType = fieldInfo.FieldType;
                int length = fieldInfo.GetCustomAttributes(typeof(LengthAttribute), false)
                    .Cast<LengthAttribute>()
                    .FirstOrDefault()
                    ?.Length ?? 0;

                Expression valueParam;
                var fieldExp = Expression.Field(frame, fieldInfo); // frame.{field}

                if (length > 0) // string
                {
                    // [Expression] if (frame.{field} != null && frame.{field} > length) throw new KeyOrderingException("...");
                    var lengthProp = Expression.Property(fieldExp, typeof(string).GetProperty("Length")); // frame.{field}.Length
                    var notNull = Expression.NotEqual(fieldExp, Expression.Constant(null)); // frame.{field} != null
                    var greaterThan = Expression.GreaterThan(lengthProp, Expression.Constant(length)); // ... > length
                    var exceptionMsg = Expression.Constant($"Length of field {fieldInfo.Name} is greater than defined length {length} while serializing a frame.");
                    var exceptionExp = Expression.New(typeof(KeyOrderingException).GetConstructor(new[] { typeof(string) }), exceptionMsg); // new KeyOrderingException(...)
                    var ifExp = Expression.IfThen(Expression.AndAlso(notNull, greaterThan), Expression.Throw(exceptionExp)); // if (...) throw ...
                    expressions.Add(ifExp);

                    // [Expression] (frame.{field} ?? string.Empty).PadRight(length).ToCharArray()
                    var padRightMethod = typeof(string).GetMethod("PadRight", new[] { typeof(int) });
                    var coalescing = Expression.Coalesce(fieldExp, Expression.Constant(string.Empty)); // frame.{field} ?? string.Empty
                    valueParam = Expression.Call(coalescing, padRightMethod, Expression.Constant(length)); // {...}.PadRight(length)

                    var toCharArrayMethod = typeof(string).GetMethod("ToCharArray", new Type[0]);
                    valueParam = Expression.Call(valueParam, toCharArrayMethod); // {valueParam}.ToCharArray()

                    fieldType = typeof(char[]);
                }
                else
                {
                    // [Expression] frame.{field}
                    valueParam = fieldExp;
                }

                var writeMethod = typeof(BinaryWriter).GetMethod("Write", new[] { fieldType })
                    ?? throw new NotSupportedException($"Type of field {fieldInfo.Name} in {typeof(TFrame)} is not supported");

                // [Expression] bw.Write({valueParam});
                var assignExp = Expression.Call(bw, writeMethod, valueParam);
                expressions.Add(assignExp);
            }

            var blockExp = Expression.Block(expressions);

            return Expression.Lambda<Action<BinaryWriter, TFrame>>(blockExp, bw, frame).Compile();
        }

        private static Func<BinaryReader, TFrame> GenerateFrameDeserializer()
        {
            var br = Expression.Parameter(typeof(BinaryReader), "br");

            var expressions = new List<Expression>();
            var frame = Expression.Variable(typeof(TFrame), "frame");
            var newFrameExp = Expression.Assign(frame, Expression.New(typeof(TFrame))); // var frame = new TFrame()
            expressions.Add(newFrameExp);

            foreach (var fieldInfo in typeof(TFrame).GetFields())
            {
                var fieldType = fieldInfo.FieldType;
                int length = fieldInfo.GetCustomAttributes(typeof(LengthAttribute), false)
                    .Cast<LengthAttribute>()
                    .FirstOrDefault()
                    ?.Length ?? 0;

                Expression valueParam;
                if (length > 0) // string
                {
                    var readMethod = typeof(BinaryReader).GetMethod("ReadChars", new[] { typeof(int) });
                    valueParam = Expression.Call(br, readMethod, Expression.Constant(length, typeof(int))); // br.ReadChars(length)

                    var ctor = typeof(string).GetConstructor(new[] { typeof(char[]) });
                    var trimEndMethod = typeof(string).GetMethod("TrimEnd", new Type[0]);
                    valueParam = Expression.Call(Expression.New(ctor, valueParam), trimEndMethod); // new string(...).TrimEnd()
                }
                else
                {
                    var readMethod = typeof(BinaryReader).GetMethod($"Read{fieldType.Name}") ??
                        throw new NotSupportedException($"Type of field {fieldInfo.Name} in {typeof(TFrame)} is not supported");
                    valueParam = Expression.Call(br, readMethod); // br.Read{fieldType}()
                }

                var assignExp = Expression.Assign(Expression.Field(frame, fieldInfo), valueParam); // frame.{field} = ...
                expressions.Add(assignExp);
            }

            expressions.Add(frame); // return frame
            var blockExp = Expression.Block(new[] { frame }, expressions);

            return Expression.Lambda<Func<BinaryReader, TFrame>>(blockExp, br).Compile();
        }

        private static Func<BinaryReader, TKey> GenerateKeyDeserializer()
        {
            var keyField = typeof(TFrame).GetFields().FirstOrDefault();
            if (keyField.FieldType != typeof(TKey))
                throw new ArgumentException($"Incorrect type of the first field {keyField.Name}. Must be {typeof(TKey).Name} as defined by {nameof(TKey)}.");

            var br = Expression.Parameter(typeof(BinaryReader), "br");
            var readMethod = typeof(BinaryReader).GetMethod($"Read{keyField.FieldType.Name}");

            return Expression.Lambda<Func<BinaryReader, TKey>>(Expression.Call(br, readMethod), br).Compile();
        }

        private static Action<BinaryWriter, TFrame> SerializeFrame { get; } = GenerateFrameSerializer();

        private static Func<BinaryReader, TFrame> DeserializeFrame { get; } = GenerateFrameDeserializer();

        private static Func<BinaryReader, TKey> DeserializeKey { get; } = GenerateKeyDeserializer();

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
                bw.BaseStream.Seek(Header.LastFramePosition, SeekOrigin.Begin);

                var last = LastFrame;
                long count = 0;
                do
                {
                    if (last != null && it.Current.Key.CompareTo(last.Key) < 0)
                    {
                        _lastFrame = last;
                        Header.FrameCount += count;
                        bw.UpdateFrameCount(Header);
                        throw new KeyOrderingException($"Frames should be in ascending order while appending.");
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
                    throw new KeyOrderingException($"Frames should be in ascending order while appending.");
                last = frame;
                list.Add(frame);
            }

            if (list.Count == 0)
                return 0;

            using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
            {
                Debug.Assert(bw.BaseStream.Length == Header.FileLength);
                bw.BaseStream.Seek(Header.LastFramePosition, SeekOrigin.Begin);

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

        public override long DeleteFramesAfter(TKey firstKey)
        {
            Debug.Assert(IsFileOpen);

            if (Header.FrameCount == 0)
                return 0;

            using (var br = new BinaryReader(Stream, Encoding.UTF8, true))
            using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
            {
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
                    _lastFrame = DeserializeFrame(br);
                }

                bw.BaseStream.SetLength(Header.FirstFramePosition + newFrameCount * Header.FrameLength);
                Header.FrameCount = newFrameCount;
                bw.UpdateFrameCount(Header);

                return deletedFrameCount;
            }
        }

        public override long DeleteFramesBefore(TKey lastKey)
        {
            Debug.Assert(IsFileOpen);

            if (Header.FrameCount == 0)
                return 0;

            using (var br = new BinaryReader(Stream, Encoding.UTF8, true))
            using (var bw = new BinaryWriter(Stream, Encoding.UTF8, true))
            {
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
                        var buf = br.ReadBytes((int)Math.Min(totalBytes, BlockCopyBufSize));

                        bw.BaseStream.Seek(writerPos, SeekOrigin.Begin);
                        bw.Write(buf);

                        readerPos += buf.Length;
                        writerPos += buf.Length;
                    }

                    br.BaseStream.Seek(Header.FirstFramePosition, SeekOrigin.Begin);
                    _firstFrame = DeserializeFrame(br);
                }

                bw.BaseStream.SetLength(Header.FirstFramePosition + newFrameCount * Header.FrameLength);
                Header.FrameCount = newFrameCount;
                bw.UpdateFrameCount(Header);

                return removedFrameCount;
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

        private bool IsStringsLoaded => _strings != null;

        private List<string> _strings = null;
        private Dictionary<string, int> _stringDict = null;

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
                    string str = br.ReadString();
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

        private IEnumerable<(int index, string str)> LookupStringInFile()
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

            int bytes = Encoding.UTF8.GetByteCount(str);
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
