using Mozo.Fwob.Exceptions;
using Mozo.Fwob.Header;
using Mozo.Fwob.Models;
using System;
using System.IO;
using System.Text;

namespace Mozo.Fwob;

/// <summary>
/// FWOB ver1 string table definition
/// A Fixed-Width Ordered Binary (FWOB) file consisits of 3 sections:
///   1 Header: pos 0: 214 bytes
///   2 String Table: pos 214: length StringTablePreservedLength
///   3 Data Frames: pos 214 + StringTablePreservedLength, length FrameCount * FrameLength
/// </summary>
public partial class FwobFile<TFrame, TKey> : AbstractFwobFile<TFrame, TKey>, IDisposable
    where TFrame : class, IFrame<TKey>, new()
    where TKey : struct, IComparable<TKey>
{
    public override string Title
    {
        get => Header.Title;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length == 0)
                throw new ArgumentException("Argument can not be empty", nameof(value));
            if (value.Length > FwobLimits.MaxTitleLength)
                throw new TitleTooLongException(value, value.Length);
            if (Stream == null)
                throw new FileNotOpenedException();

            Header.Title = value;

            using BinaryWriter bw = new(Stream, Encoding.UTF8, true);
            bw.UpdateTitle(Header);
        }
    }

    public string? FilePath { get; private set; }

    public Stream? Stream { get; private set; }

    public FwobHeader Header { get; private set; }

    private bool IsFileOpen => Stream != null && FilePath != null;

    private const int BlockCopyBufSize = 4 * 1024 * 1024;

    private FwobFile()
    {
        Header = new();
    }

    public FwobFile(string path, FileAccess fileAccess = FileAccess.ReadWrite, FileShare fileShare = FileShare.Read)
    {
        FilePath = path;
        Stream = new FileStream(path, FileMode.Open, fileAccess, fileShare);

        using BinaryReader br = new(Stream, Encoding.UTF8, true);

        FwobHeader? header = br.ReadHeader();
        if (header == null)
            throw new CorruptedFileHeaderException(path);

        Header = header;

        FrameInfo? frameInfo = header.GetFrameInfo<TFrame, TKey>();
        if (frameInfo == null)
            throw new FrameTypeMismatchException(path, typeof(TFrame));

        FrameInfo = frameInfo;

        if (Header.FileLength != br.BaseStream.Length)
            throw new CorruptedFileLengthException(path, Header.FileLength, br.BaseStream.Length);

        if (FrameCount > 0)
        {
            _firstFrame = GetFrame(0);
            _lastFrame = GetFrame(FrameCount - 1);
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
        Header = new();

        GC.SuppressFinalize(this);
    }
}
