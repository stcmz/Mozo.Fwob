using Mozo.Fwob.Exceptions;
using Mozo.Fwob.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Mozo.Fwob;

// The implementation of interface IStringTable.
public partial class FwobFile<TFrame, TKey>
{
    private bool _isStringTableLoaded = false;
    private List<string> _strings = new();
    private Dictionary<string, int> _stringDict = new();

    public void LoadStringTable()
    {
        ValidateAccess(FileAccess.Read);

        if (_isStringTableLoaded)
            return;

        _br!.BaseStream.Seek(Header.StringTablePosition, SeekOrigin.Begin);

        List<string> list = new();
        Dictionary<string, int> dict = new();

        for (int i = 0; i < Header.StringCount; i++)
        {
            string str = _br.ReadString();
            dict[str] = list.Count;
            list.Add(str);
        }

        if (_br.BaseStream.Position != Header.StringTableEnding)
            throw new CorruptedStringTableLengthException(FilePath!, Header.StringTableLength, _br.BaseStream.Position - FwobHeader.HeaderLength);

        _strings = list;
        _stringDict = dict;
        _isStringTableLoaded = true;
    }

    /// <summary>
    /// Release the resources associated with the string table. This method is idempotent.
    /// </summary>
    public void UnloadStringTable()
    {
        if (!_isStringTableLoaded)
            return;

        _strings.Clear();
        _stringDict.Clear();
        _isStringTableLoaded = false;
    }

    public override IReadOnlyList<string> Strings
    {
        get
        {
            if (Stream == null)
                throw new FileNotOpenedException();

            return _strings;
        }
    }

    public override int StringCount
    {
        get
        {
            if (Stream == null)
                throw new FileNotOpenedException();

            if (_isStringTableLoaded)
                return _strings.Count;

            return Header.StringCount;
        }
    }

    private IEnumerable<(int index, string str)> LookupStringInFile()
    {
        _br!.BaseStream.Seek(Header.StringTablePosition, SeekOrigin.Begin);

        for (int i = 0; i < Header.StringCount; i++)
            yield return (i, _br.ReadString());
    }

    public override string? GetString(int index)
    {
        ValidateAccess(FileAccess.Read);

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
        ValidateAccess(FileAccess.Read);

        if (str == null)
            throw new ArgumentNullException(nameof(str));

        if (_isStringTableLoaded)
            return _stringDict.TryGetValue(str, out int index) ? index : -1;

        foreach ((int idx, string s) in LookupStringInFile())
            if (s == str)
                return idx;

        return -1;
    }

    private void WriteStringTable(IReadOnlyList<string> strings)
    {
        // No string table overflow check
        _bw!.BaseStream.Seek(Header.StringTablePosition, SeekOrigin.Begin);
        foreach (var str in strings)
        {
            _bw.Write(str);
        }

        Header.StringCount = strings.Count;
        Header.StringTableLength = (int)(_bw.BaseStream.Position - Header.StringTablePosition);
        _bw.UpdateStringTableLength(Header);
        _bw.Flush();

        if (_isStringTableLoaded)
        {
            List<string> list = new();
            Dictionary<string, int> dict = new();

            for (int i = 0; i < strings.Count; i++)
            {
                dict[strings[i]] = i;
                list.Add(strings[i]);
            }

            _strings = list;
            _stringDict = dict;
        }
    }

    public override int AppendString(string str)
    {
        ValidateAccess(FileAccess.Write);

        if (str == null)
            throw new ArgumentNullException(nameof(str));

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

        _bw!.BaseStream.Seek(Header.StringTableEnding, SeekOrigin.Begin);
        _bw.Write(str);

        int index = Header.StringCount++;
        Header.StringTableLength = (int)(_bw.BaseStream.Position - Header.StringTablePosition);
        _bw.UpdateStringTableLength(Header);
        _bw.Flush();

        if (_isStringTableLoaded)
        {
            _stringDict[str] = _strings.Count;
            _strings.Add(str);
        }

        return index;
    }

    public override bool ContainsString(string str)
    {
        ValidateAccess(FileAccess.Read);

        if (str == null)
            throw new ArgumentNullException(nameof(str));

        if (_isStringTableLoaded)
            return _stringDict.ContainsKey(str);

        foreach ((int _, string s) in LookupStringInFile())
            if (s == str)
                return true;

        return false;
    }

    public override int DeleteAllStrings()
    {
        ValidateAccess(FileAccess.Write);

        int len = Header.StringCount;
        if (len == 0)
            return 0;

        Header.StringCount = 0;
        Header.StringTableLength = 0;
        _bw!.UpdateStringTableLength(Header);
        _bw.Flush();

        if (_isStringTableLoaded)
        {
            _stringDict.Clear();
            _strings.Clear();
        }

        return len;
    }
}
