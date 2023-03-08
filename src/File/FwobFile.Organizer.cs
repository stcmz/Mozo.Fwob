using Mozo.Fwob.Exceptions;
using Mozo.Fwob.Extensions;
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
    /// Split a FWOB file into multiple segments with <paramref name="firstKeys"/>
    /// being the first key of a segment (except the first segment).
    /// </summary>
    /// <param name="srcPath">A path to a file to be loaded and splitted.</param>
    /// <param name="outDirPath">A path to a directory where the splitted files will be stored.</param>
    /// <param name="firstKeys">Keys that end a segment.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="FrameNotFoundException"></exception>
    public static void Split(string srcPath, string outDirPath, params TKey[] firstKeys)
    {
        Split(srcPath, outDirPath, (IEnumerable<TKey>)firstKeys);
    }

    /// <summary>
    /// Split a FWOB file into multiple segments with <paramref name="firstKeys"/>
    /// being the first key of a segment (except the segment containing the frames before the first key, if any).
    /// </summary>
    /// <param name="srcPath">A file path to be loaded and splitted.</param>
    /// <param name="outDirPath">A path to a directory where the splitted files will be stored.</param>
    /// <param name="firstKeys">Keys that end a segment.</param>
    /// <param name="ignoreEmptyParts">A boolean indicating if an empty part should be emitted</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="FrameNotFoundException"></exception>
    public static void Split(string srcPath, string outDirPath, IEnumerable<TKey> firstKeys,
        FileMode mode = FileMode.Create,
        FileShare share = FileShare.None,
        bool ignoreEmptyParts = true)
    {
        if (srcPath == null)
            throw new ArgumentNullException(nameof(srcPath));

        if (outDirPath == null)
            throw new ArgumentNullException(nameof(outDirPath));

        if (firstKeys == null)
            throw new ArgumentNullException(nameof(firstKeys));

        if (!firstKeys.Any())
            throw new ArgumentException("Argument must contain at least one separating key", nameof(firstKeys));

        ValidateKeys(firstKeys, nameof(firstKeys));

        if (!File.Exists(srcPath))
            throw new FileNotFoundException("Fwob file not found", srcPath);

        using FwobFile<TFrame, TKey> srcFile = new(srcPath, FileAccess.Read, FileShare.Read);

        if (srcFile.FrameCount == 0)
            throw new FrameNotFoundException(srcPath);

        if (!Directory.Exists(outDirPath))
            Directory.CreateDirectory(outDirPath);

        long framesWritten = 0, segBeginIdx = 0;
        int seg = 0;

        // Extract file stem
        string fileNameStem;
        if (Path.GetExtension(srcPath).ToLower() == ".fwob")
            fileNameStem = Path.GetFileNameWithoutExtension(srcPath);
        else
            fileNameStem = Path.GetFileName(srcPath);

        void WriteSegmentFile(long segEndIdx)
        {
            string dstPath = Path.Combine(outDirPath, $"{fileNameStem}.part{seg}.fwob");

            // Locate the range to copy
            long segFrameCount = segEndIdx - segBeginIdx;
            if (segFrameCount == 0 && ignoreEmptyParts)
                return;

            long segBeginPos = srcFile.Header.FirstFramePosition + segBeginIdx * srcFile.Header.FrameLength;
            long segBytes = segFrameCount * srcFile.Header.FrameLength;

            // Create the segment file
            using FileStream segmentStream = new(dstPath, mode, FileAccess.Write, share);
            using BinaryWriter bw = new(segmentStream, Encoding.UTF8);

            // Clone header and string table
            BlockCopy(srcFile._br!, bw, 0, 0, srcFile.Header.FirstFramePosition);

            // Clone frames
            BlockCopy(srcFile._br!, bw, segBeginPos, srcFile.Header.FirstFramePosition, segBytes);

            // Update the frame count in header
            bw.UpdateFrameCount(new FwobHeader { FrameCount = segFrameCount });

            framesWritten += segFrameCount;
            seg++;
            segBeginIdx = segEndIdx;
        }

        foreach (TKey firstKey in firstKeys)
        {
            long segEndIdx = srcFile.GetLowerBound(firstKey, segBeginIdx, srcFile.FrameCount);
            WriteSegmentFile(segEndIdx);
        }

        WriteSegmentFile(srcFile.FrameCount);

        Debug.Assert(framesWritten == srcFile.FrameCount, $"Frames written inconsistent: {framesWritten} != {srcFile.FrameCount}");
    }

    /// <summary>
    /// Concatenate a sequence of *.FWOB files whose paths are given by <paramref name="srcPaths"/>.<br/>
    /// The outcome file will contain concatenated frames that follow the specified order of the source files.<br/>
    /// The frame keys will be checked to enforce the ordering rule and a <see cref="KeyOrderingException"/> will be thrown if violated.
    /// </summary>
    /// <param name="dstPath">A path to store the resulting FWOB file.</param>
    /// <param name="srcPaths">A sequence of *.FWOB files to be concatenated.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="KeyOrderViolationException"></exception>
    /// <exception cref="TitleIncompatibleException"></exception>
    /// <exception cref="StringTableIncompatibleException"></exception>
    public static void Concat(string dstPath, params string[] srcPaths)
    {
        Concat(dstPath, (IEnumerable<string>)srcPaths);
    }

    /// <summary>
    /// Concatenate a sequence of *.FWOB files whose paths are given by <paramref name="srcPaths"/>.<br/>
    /// The outcome file will contain concatenated frames that follow the specified order of the source files.<br/>
    /// The frame keys will be checked to enforce the ordering rule and a <see cref="KeyOrderingException"/> will be thrown if violated.
    /// </summary>
    /// <param name="dstPath">A path to store the resulting FWOB file.</param>
    /// <param name="srcPaths">A sequence of *.FWOB files to be concatenated.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="KeyOrderViolationException"></exception>
    /// <exception cref="TitleIncompatibleException"></exception>
    /// <exception cref="StringTableIncompatibleException"></exception>
    public static void Concat(string dstPath,
        IEnumerable<string> srcPaths,
        FileMode mode = FileMode.Create,
        FileShare share = FileShare.None)
    {
        if (dstPath == null)
            throw new ArgumentNullException(nameof(dstPath));

        if (srcPaths == null)
            throw new ArgumentNullException(nameof(srcPaths));

        if (!srcPaths.Any())
            throw new ArgumentException("Argument must contain at least one file path", nameof(srcPaths));

        foreach (string path in srcPaths)
            if (!File.Exists(path))
                throw new FileNotFoundException("Fwob file not found", path);

        // Checks compatibility of files
        List<string> strings = new();
        string? title = null;
        TKey? lastKey = null;
        int stringTablePreservedLength = 0;
        long frameCount = 0;

        foreach (string srcPath in srcPaths)
        {
            using FwobFile<TFrame, TKey> srcFile = new(srcPath, FileAccess.Read, FileShare.Read);

            // Verify consistency of file title
            if (title == null)
                title = srcFile.Title;
            else if (title != srcFile.Title)
                throw new TitleIncompatibleException(srcFile.Title, title);

            // Verify consistency of the string tables
            srcFile.LoadStringTable();
            for (int i = 0; i < srcFile.StringCount; i++)
            {
                if (i >= strings.Count)
                    strings.Add(srcFile.Strings[i]);
                else if (strings[i] != srcFile.Strings[i])
                    throw new StringTableIncompatibleException(srcPath, i, srcFile.Strings[i], strings[i]);
            }
            srcFile.UnloadStringTable();
            stringTablePreservedLength = Math.Max(srcFile.Header.StringTablePreservedLength, stringTablePreservedLength);

            if (srcFile.Header.FrameCount == 0)
                continue;
            frameCount += srcFile.Header.FrameCount;

            // FirstFrame and LastFrame are not null if FrameCount > 0
            if (lastKey != null && GetKey(srcFile._firstFrame!).CompareTo(lastKey.Value) < 0)
                throw new KeyOrderViolationException(srcPath, $"The first frame in the file must be >= the last frame in previous file.");

            lastKey = GetKey(srcFile._lastFrame!);
        }

        using FwobFile<TFrame, TKey> dstFile = new(dstPath, title!, mode, FileAccess.Write, share, stringTablePreservedLength);

        // Write frame count
        dstFile._bw!.UpdateFrameCount(new() { FrameCount = frameCount });

        // Write the string table
        dstFile.WriteStringTable(strings);

        long writerPos = dstFile.Header.FirstFramePosition;

        // Copy frame data
        foreach (string srcPath in srcPaths)
        {
            using FwobFile<TFrame, TKey> srcFile = new(srcPath, FileAccess.Read, FileShare.Read);

            if (srcFile.FrameCount == 0)
                continue;

            long segBytes = srcFile.Header.FileLength - srcFile.Header.FirstFramePosition;

            BlockCopy(srcFile._br!, dstFile._bw!, srcFile.Header.FirstFramePosition, writerPos, segBytes);

            writerPos += segBytes;
        }

        Debug.Assert(frameCount * dstFile.Header.FrameLength == writerPos - dstFile.Header.FirstFramePosition);
    }
}