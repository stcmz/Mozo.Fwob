using Mozo.Fwob.Exceptions;
using Mozo.Fwob.Header;
using Mozo.Fwob.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Mozo.Fwob;

public partial class FwobFile<TFrame, TKey>
{
    /// <summary>
    /// Creates a new FWOB file with the given file system path and title
    /// </summary>
    /// <param name="path">A file system path to store the created file.</param>
    /// <param name="title">A logical title of the FWOB.</param>
    /// <param name="mode">A constant that determines how to open or create the file.</param>
    /// <param name="access">A constant that determines how the file can be accessed by the FileStream object.</param>
    /// <param name="share">A constant that determines how the file will be shared by processes.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static FwobFile<TFrame, TKey> CreateNew(
        string path,
        string title,
        FileMode mode = FileMode.CreateNew,
        FileAccess access = FileAccess.ReadWrite,
        FileShare share = FileShare.Read)
    {
        if (title == null)
            throw new ArgumentNullException(nameof(title));

        if (title.Length == 0)
            throw new ArgumentException("Argument can not be empty", nameof(title));

        if (title.Length > FwobLimits.MaxTitleLength)
            throw new TitleTooLongException(title, title.Length);

        FwobFile<TFrame, TKey> file = new()
        {
            FrameInfo = FrameInfo.FromSystem<TFrame, TKey>(),
            FilePath = path,
            Stream = new FileStream(path, mode, access, share),
            Header = FwobHeader.CreateNew<TFrame, TKey>(title),
        };

        using (BinaryWriter bw = new(file.Stream, Encoding.UTF8, true))
        {
            bw.WriteHeader(file.Header);
            bw.Write(new byte[file.Header.StringTablePreservedLength]);
        }

        return file;
    }

    /// <summary>
    /// Split a FWOB file into multiple segments with <paramref name="firstKeys"/>
    /// being the first key of a segment (except the first segment).
    /// </summary>
    /// <param name="srcPath">A file path to be loaded and splitted.</param>
    /// <param name="firstKeys">Keys that end a segment.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="KeyOrderingException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="FrameNotFoundException"></exception>
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
                throw new ArgumentException("Argument must be in strictly ascending order", nameof(firstKeys));

        if (!File.Exists(srcPath))
            throw new FileNotFoundException("Fwob file not found", srcPath);

        using FwobFile<TFrame, TKey> srcFile = new(srcPath, FileAccess.Read);

        if (srcFile.FrameCount == 0)
            throw new FrameNotFoundException(srcPath);

        // Not null if FrameCount > 0
        if (firstKeys.First().CompareTo(GetKey(srcFile.FirstFrame!)) < 0)
            throw new ArgumentOutOfRangeException(nameof(firstKeys), "First item in argument beyonds file beginning");

        if (firstKeys.Last().CompareTo(GetKey(srcFile.LastFrame!)) > 0)
            throw new ArgumentOutOfRangeException(nameof(firstKeys), "Last item in argument beyonds file ending");

        using BinaryReader br = new(srcFile.Stream!, Encoding.UTF8, true);

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

            using FileStream stream = new(dstPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using BinaryWriter bw = new(stream);

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

        Debug.Assert(framesWritten == srcFile.FrameCount, $"Frames written inconsistent: {framesWritten} != {srcFile.FrameCount}");
    }

    /// <summary>
    /// Concatenate a sequence of *.FWOB files whose paths are given by <paramref name="srcPaths"/>.
    /// The outcome file will contain concatenated frames that follow the specified order of the source files.
    /// The frame keys will be checked to enforce the ordering rule and a <see cref="KeyOrderingException"/> will be thrown if violated.
    /// </summary>
    /// <param name="dstPath">A path to store the resulting FWOB file.</param>
    /// <param name="srcPaths">A sequence of *.FWOB files to be concatenated.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="KeyOrderingException"></exception>
    /// <exception cref="FrameNotFoundException"></exception>
    /// <exception cref="FileTitleException"></exception>
    /// <exception cref="StringTableException"></exception>
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

        List<FwobFile<TFrame, TKey>> fileList = new();

        try
        {
            List<string> strings = new();

            // Template file for string table
            FwobFile<TFrame, TKey>? templateFile = null;

            foreach (string path in srcPaths)
            {
                FwobFile<TFrame, TKey> file = new(path, FileAccess.Read, FileShare.Read);
                FwobFile<TFrame, TKey>? prevFile = fileList.LastOrDefault();
                fileList.Add(file);

                if (file.FrameCount == 0)
                    throw new FrameNotFoundException(path);

                if (prevFile != null)
                {
                    // Not null if FrameCount > 0
                    if (GetKey(file.FirstFrame!).CompareTo(GetKey(prevFile.LastFrame!)) < 0)
                        throw new KeyOrderViolationException(path, $"First frame of {path} must be >= last frame in previous file.");

                    if (file.Title != prevFile.Title)
                        throw new TitleIncompatibleException(file.Title, prevFile.Title);
                }

                file.LoadStringTable();

                // Check string table compatibility
                for (int i = Math.Min(strings.Count, file.Strings.Count) - 1; i >= 0; i--)
                    if (file.Strings[i] != strings[i])
                        throw new StringTableIncompatibleException(path, i, file.Strings[i], strings[i]);

                if (file.StringCount > strings.Count)
                    templateFile = file;

                for (int i = strings.Count; i < file.Strings.Count; i++)
                    strings.Add(file.Strings[i]);
                file.UnloadStringTable();
            }

            // No string table, use the first file as a template
            templateFile ??= fileList[0];

            Debug.Assert(templateFile.Stream != null);

            using FileStream stream = new(dstPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using BinaryWriter bw = new(stream);

            // Clone header and string table from template file
            using (BinaryReader br = new(templateFile.Stream, Encoding.UTF8, true))
            {
                br.BaseStream.Seek(0, SeekOrigin.Begin);
                bw.Write(br.ReadBytes((int)templateFile.Header.FirstFramePosition));
            }

            // Clone frame data from each source
            long frameCount = 0;
            foreach (FwobFile<TFrame, TKey> file in fileList)
            {
                Debug.Assert(file.Stream != null);

                using (BinaryReader br = new(file.Stream))
                {
                    br.BaseStream.Seek(file.Header.FirstFramePosition, SeekOrigin.Begin);

                    long dataLength = file.FrameCount * file.Header.FrameLength;
                    frameCount += file.FrameCount;

                    for (long k = dataLength; k > 0; k -= BlockCopyBufSize)
                        bw.Write(br.ReadBytes((int)Math.Min(k, BlockCopyBufSize)));
                }
                file.Dispose();
            }

            // Update frame count
            bw.UpdateFrameCount(new FwobHeader { FrameCount = frameCount });
        }
        finally
        {
            foreach (FwobFile<TFrame, TKey> file in fileList)
                file.Dispose();
        }
    }
}
