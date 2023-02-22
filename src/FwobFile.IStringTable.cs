using Mozo.Fwob.Exceptions;
using Mozo.Fwob.Header;
using Mozo.Fwob.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Mozo.Fwob;

/// <summary>
/// The implementation of interface <see cref="IStringTable"/>.
/// </summary>
/// <typeparam name="TFrame"></typeparam>
/// <typeparam name="TKey"></typeparam>
public partial class FwobFile<TFrame, TKey>
{
    private bool _isStringTableLoaded = false;
    private List<string> _strings = new();
    private Dictionary<string, int> _stringDict = new();

    public void LoadStringTable()
    {
        if (_isStringTableLoaded)
            return;

        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);

        using BinaryReader br = new(Stream, Encoding.UTF8, true);

        Debug.Assert(br.BaseStream.Length >= Header.FirstFramePosition);
        br.BaseStream.Seek(Header.StringTablePosition, SeekOrigin.Begin);

        List<string> list = new();
        Dictionary<string, int> dict = new();

        for (int i = 0; i < Header.StringCount; i++)
        {
            string str = br.ReadString();
            dict[str] = list.Count;
            list.Add(str);
        }

        if (br.BaseStream.Position != Header.StringTableEnding)
            throw new CorruptedStringTableLengthException(FilePath!, Header.StringTableLength, br.BaseStream.Position - FwobHeader.HeaderLength);

        _strings = list;
        _stringDict = dict;
        _isStringTableLoaded = true;
    }

    public void UnloadStringTable()
    {
        if (!_isStringTableLoaded)
            return;

        _strings.Clear();
        _stringDict.Clear();
        _isStringTableLoaded = false;
    }

    public override IReadOnlyList<string> Strings => _strings;

    public override int StringCount
    {
        get
        {
            if (_isStringTableLoaded)
                return _strings.Count;

            Debug.Assert(IsFileOpen);
            return Header.StringCount;
        }
    }

    private IEnumerable<(int index, string str)> LookupStringInFile()
    {
        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);

        using BinaryReader br = new(Stream, Encoding.UTF8, true);

        Debug.Assert(br.BaseStream.Length >= Header.FirstFramePosition);
        br.BaseStream.Seek(Header.StringTablePosition, SeekOrigin.Begin);

        for (int i = 0; i < Header.StringCount; i++)
            yield return (i, br.ReadString());
    }

    public override string? GetString(int index)
    {
        if (index < 0 || index >= Header.StringCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (_isStringTableLoaded)
            return _strings[index];

        foreach ((int idx, string str) in LookupStringInFile())
            if (idx == index)
                return str;

        return null;
    }

    public override int GetIndex(string str)
    {
        if (str == null)
            throw new ArgumentNullException(nameof(str));

        if (_isStringTableLoaded)
            return _stringDict.TryGetValue(str, out int index) ? index : -1;

        foreach ((int idx, string s) in LookupStringInFile())
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
        Debug.Assert(Stream != null);

        int bytes = Encoding.UTF8.GetByteCount(str);
        int length = bytes < 128 ? 1 : bytes < 128 * 128 ? 2 : bytes < 128 * 128 * 128 ? 3 : 4;
        // A string is serialized with a 7-bit encoded integer prefix
        // 1 byte  length prefix: 00~7f (0~128-1)
        // 2 bytes length prefix: 8001~ff01~8002~807f~ff7f (128~128^2-1)
        // 3 bytes length prefix: 808001~ff8001~808101~807f01~ff7f01~808002~ffff7f (128^2~128^3-1)
        // 4 bytes length prefix: 80808001~ffffff7f (128^3~128^4-1)
        int requiredLength = Header.StringTableLength + bytes + length;
        if (requiredLength > Header.StringTablePreservedLength)
            throw new StringTableOutOfSpaceException(FilePath!, requiredLength, Header.StringTablePreservedLength);

        using (BinaryWriter bw = new(Stream, Encoding.UTF8, true))
        {
            Debug.Assert(bw.BaseStream.Length >= Header.FirstFramePosition);
            bw.BaseStream.Seek(Header.StringTableEnding, SeekOrigin.Begin);
            bw.Write(str);

            index = Header.StringCount++;
            Header.StringTableLength = (int)(bw.BaseStream.Position - Header.StringTablePosition);
            bw.UpdateStringTableLength(Header);
        }

        if (_isStringTableLoaded)
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

        if (_isStringTableLoaded)
            return _stringDict.ContainsKey(str);

        foreach ((int _, string s) in LookupStringInFile())
            if (s == str)
                return true;

        return false;
    }

    public override void ClearStrings()
    {
        Debug.Assert(IsFileOpen);
        Debug.Assert(Stream != null);

        using (BinaryWriter bw = new(Stream, Encoding.UTF8, true))
        {
            Header.StringCount = 0;
            Header.StringTableLength = 0;
            bw.UpdateStringTableLength(Header);
        }

        if (_isStringTableLoaded)
        {
            _stringDict.Clear();
            _strings.Clear();
        }
    }
}
