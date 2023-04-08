using Mozo.Fwob.Abstraction;
using Mozo.Fwob.Exceptions;
using Mozo.Fwob.Extensions;
using System;
using System.IO;
using System.Text;

namespace Mozo.Fwob;

/// <summary>
/// FWOB ver1 string table definition<br/>
/// A Fixed-Width Ordered Binary (FWOB) file consisits of 3 sections:<br/>
///   1 Header: pos 0: 214 bytes<br/>
///   2 String Table: pos 214: length StringTablePreservedLength<br/>
///   3 Data Frames: pos 214 + StringTablePreservedLength, length FrameCount * FrameLength<br/>
/// </summary>
public partial class FwobFile<TFrame, TKey> : AbstractFwobFile<TFrame, TKey>, IDisposable
    where TFrame : class, new()
    where TKey : struct, IComparable<TKey>
{
    public override string Title
    {
        get => Header.Title;
        set
        {
            ValidateAccess(FileAccess.Write);

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Length == 0)
                throw new ArgumentException("Argument can not be empty", nameof(value));

            if (value.Length > Limits.MaxTitleLength)
                throw new TitleTooLongException(value, value.Length);

            Header.Title = value;

            using BinaryWriter bw = new(Stream!, Encoding.UTF8, true);
            bw.UpdateTitle(Header);
        }
    }

    public string? FilePath { get; private set; }

    public Stream? Stream { get; private set; }

    public FwobHeader Header { get; private set; }


    private const int BlockCopyBufSize = 4 * 1024 * 1024;

    private BinaryReader? _br;
    private BinaryWriter? _bw;

    /// <summary>
    /// Opens an existing FWOB file with the given file system path.
    /// </summary>
    /// <param name="path">A file system path to store the created file.</param>
    /// <param name="access">A constant that determines how the file can be accessed by the FileStream object.</param>
    /// <param name="share">A constant that determines how the file will be shared by processes.</param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="CorruptedFileHeaderException"></exception>
    /// <exception cref="FrameTypeMismatchException"></exception>
    /// <exception cref="CorruptedFileLengthException"></exception>
    public FwobFile(string path, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read)
    {
        if (!access.HasFlag(FileAccess.Read))
            throw new FileNotReadableException();

        FilePath = path;
        Stream = new FileStream(path, FileMode.Open, access, share);

        _br = new(Stream, Encoding.UTF8);

        if (access.HasFlag(FileAccess.Write))
            _bw = new(Stream, Encoding.UTF8);

        FwobHeader? header = _br.ReadHeader();
        if (header == null)
        {
            Close();
            throw new CorruptedFileHeaderException(path);
        }

        Header = header;

        if (!header.Validate(FrameInfo))
        {
            Close();
            throw new FrameTypeMismatchException(path, typeof(TFrame));
        }

        if (Header.FileLength != _br.BaseStream.Length)
        {
            long actualLength = _br.BaseStream.Length;
            long expectedLength = Header.FileLength;
            Close();
            throw new CorruptedFileLengthException(path, expectedLength, actualLength);
        }

        if (FrameCount > 0)
        {
            _firstFrame = GetFrameAt(0);
            _lastFrame = GetFrameAt(FrameCount - 1);
        }
    }

    /// <summary>
    /// Creates a new FWOB file with the given file system path and title.
    /// </summary>
    /// <param name="path">A file system path to store the created file.</param>
    /// <param name="title">A logical title of the FWOB.</param>
    /// <param name="mode">A constant that determines how to open or create the file.</param>
    /// <param name="access">A constant that determines how the file can be accessed by the FileStream object.</param>
    /// <param name="share">A constant that determines how the file will be shared by processes.</param>
    /// <param name="preservedStringTableLength">An integer that represents the number of bytes that will be preserved for the string table.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotWritableException"></exception>
    /// <exception cref="TitleTooLongException"></exception>
    public FwobFile(
        string path,
        string title,
        FileMode mode = FileMode.Create,
        FileAccess access = FileAccess.ReadWrite,
        FileShare share = FileShare.Read,
        int preservedStringTableLength = FwobHeader.DefaultStringTablePreservedLength)
    {
        if (!access.HasFlag(FileAccess.Write))
            throw new FileNotWritableException();

        // The title will be validated in the FwobHeader contructor
        Header = new FwobHeader(FrameInfo, title, preservedStringTableLength);

        FilePath = path;
        Stream = new FileStream(path, mode, access, share);

        if (access.HasFlag(FileAccess.Read))
            _br = new(Stream, Encoding.UTF8);

        _bw = new(Stream, Encoding.UTF8);

        _bw.WriteHeader(Header);
        _bw.Write(new byte[Header.StringTablePreservedLength]);

        _bw.Flush();
    }

    ~FwobFile()
    {
        Close();
    }

    private void ValidateAccess(FileAccess requiredAccess)
    {
        if (Stream == null)
            throw new FileNotOpenedException();

        if (_br == null && requiredAccess.HasFlag(FileAccess.Read))
            throw new FileNotReadableException();

        if (_bw == null && requiredAccess.HasFlag(FileAccess.Write))
            throw new FileNotWritableException();
    }

    /// <summary>
    /// Fix corrupted file length if everything but the file length is good.
    /// </summary>
    /// <remarks>
    /// To allow this method to fix the file length, all of the following conditions must be met:
    ///   1) the header format is good, and
    ///   2) the actual file size minus header length and string table length is divisible by the frame length.
    /// Otherwise, this method will fail to fix the file.
    /// 
    /// This method fix the corruption with two rules:
    ///   1) if the key ordering of all frames is good,
    ///      then update the header frame count to reflect the actual file size.
    ///   2) if the key ordering of the header-frame-count prefixing frames is good,
    ///      and the number of redundant trailing frames is less than or equal to <paramref name="maxTruncatedFrames"/>.
    ///      then truncate the file to reflect the header frame count.
    /// </remarks>
    /// <param name="path">A file system path to the file being fixed.</param>
    /// <param name="maxTruncatedFrames">A long integer that represents the maximum number of frames that can be deleted from the tail to match the header frame count.</param>
    /// <exception cref="CorruptedFileHeaderException"></exception>
    /// <exception cref="FrameTypeMismatchException"></exception>
    /// <exception cref="CorruptedFileLengthException"></exception>
    /// <exception cref="KeyOrderViolationException"></exception>
    public static void FixFileLength(string path, long maxTruncatedFrames = 2048)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using BinaryReader br = new(stream, Encoding.UTF8);

        // Read and validate file header
        FwobHeader header = br.ReadHeader() ?? throw new CorruptedFileHeaderException(path);

        if (!header.Validate(_FrameInfo))
            throw new FrameTypeMismatchException(path, typeof(TFrame));

        // Check if the actual length is divisible by frame length
        long actualLength = br.BaseStream.Length;
        long actualFramesLength = actualLength - header.FirstFramePosition;
        if (actualFramesLength % header.FrameLength != 0)
            throw new CorruptedFileLengthException(path, header.FileLength, actualLength);

        // Nothing to fix
        long actualFrameCount = actualFramesLength / header.FrameLength;
        if (actualFrameCount == header.FrameCount)
            return;

        // Traverse the frames to check the key ordering
        bool first = true;
        TKey lastKey = default;

        for (long i = 0; i < actualFrameCount; i++)
        {
            TKey key = ReadKey(br, header.FirstFramePosition + i * header.FrameLength);

            if (first)
            {
                first = false;
            }
            else if (lastKey.CompareTo(key) > 0) // Incorrect ordering
            {
                // Truncate the file if prefixing frames are correctly ordered and the trailing frame count falls within allowance
                long headerFrameCount = Math.Max(0, header.FrameCount);

                if (i >= headerFrameCount && actualFrameCount - i <= maxTruncatedFrames)
                {
                    stream.SetLength(header.FirstFramePosition + headerFrameCount * header.FrameLength);

                    if (headerFrameCount != header.FrameCount)
                    {
                        actualFrameCount = headerFrameCount;
                        break;
                    }

                    return;
                }

                throw new KeyOrderViolationException(path);
            }

            lastKey = key;
        }

        // Update header frame count as the file data passed the validation
        using BinaryWriter bw = new(stream, Encoding.UTF8);
        header.FrameCount = actualFrameCount;
        bw.UpdateFrameCount(header);
        bw.Flush();
    }

    /// <summary>
    /// Close the file and release the resources. This method is idempotent.
    /// </summary>
    public void Close()
    {
        if (_br != null)
        {
            _br.Dispose();
            _br = null;
        }

        if (_bw != null)
        {
            _bw.Dispose();
            _bw = null;
        }

        if (Stream != null)
        {
            Stream.Dispose();
            Stream = null;
        }

        UnloadStringTable();
        _firstFrame = null;
        _lastFrame = null;
        FilePath = null;
        Header = new();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// This method won't be called if exception was thrown in the constructor because the object has not successfully created.
    /// </summary>
    public void Dispose()
    {
        Close();
    }
}
