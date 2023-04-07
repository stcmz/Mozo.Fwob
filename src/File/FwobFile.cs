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
