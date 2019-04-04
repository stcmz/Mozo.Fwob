using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Fwob
{
    public static class FwobReader
    {
        public static Header ReadHeader(this BinaryReader br)
        {
            // check least file length
            if (br.BaseStream.Length < Header.HeaderLength)
                return null;

            //*********************** Signature and Version (5 bytes) ************************//

            // pos 0: 4 bytes
            var sig = new string(br.ReadChars(4));
            if (sig != Header.Signature)
                return null;

            var header = new Header();

            // pos 4: 1 byte
            header.Version = br.ReadByte();

            if (header.Version != Header.CurrentVersion)
                return null;

            //*********************** Descriptors of Fields (149 bytes) ************************//

            // pos 5: 1 byte (allow up to 16 fields)
            header.FieldCount = br.ReadByte();

            if (header.FieldCount > Header.MaxFields)
                return null;

            // pos 6: 16 bytes (allow up to 16 fields)
            header.FieldLengths = br.ReadBytes(Header.MaxFields);

            // pos 22: 4 bytes (up to 16 types, each has 2 bits: 00 integer, 01 floating, 10 string, 11 index)
            header.FieldTypes = br.ReadUInt32();

            // pos 26: 128 bytes (allow up to 16*8 chars)
            header.FieldNames = new string[Header.MaxFields];
            for (int i = 0; i < Header.MaxFields; i++)
            {
                header.FieldNames[i] = new string(br.ReadChars(Header.MaxFieldNameLength)).Trim();
                if (i < header.FieldCount && header.FieldNames[i].Length == 0)
                    return null;
            }

            //*********************** Size of String Tables (12 bytes) ************************//

            // pos 154: 4 bytes
            header.StringCount = br.ReadInt32();

            if (header.StringCount < 0)
                return null;

            // pos 158: 4 bytes
            header.StringTableLength = br.ReadInt32();

            if (header.StringTableLength < 0)
                return null;

            // pos 162: 4 bytes
            header.StringTablePreservedLength = br.ReadInt32();

            if (header.StringTablePreservedLength < header.StringTableLength)
                return null;

            //*********************** Frames (44 bytes) ************************//

            // pos 166: 8 bytes
            header.FrameCount = br.ReadInt64();

            if (header.FrameCount < 0)
                return null;

            // pos 174: 4 bytes, should be the sum of FieldLengths
            header.FrameLength = br.ReadInt32();

            if (header.FrameLength != header.FieldLengths.Take(header.FieldCount).Cast<int>().Sum())
                return null;

            // pos 178: 16 bytes (up to 16 chars)
            header.FrameName = new string(br.ReadChars(16)).Trim();

            if (header.FrameName.Length == 0)
                return null;

            // pos 194: 16 bytes (up to 16 chars)
            header.FrameType = new string(br.ReadChars(16)).Trim();

            if (header.FrameType.Length == 0)
                return null;

            return header;
        }

        public static List<string> ReadStringTable(this BinaryReader br, int stringCount,
            int stringTableLength, int stringTablePreservedLength)
        {
            Debug.Assert(stringTablePreservedLength >= stringTableLength);

            var p = br.BaseStream.Position;
            if (p + stringTablePreservedLength < br.BaseStream.Length)
                return null;

            var list = new List<string>();
            for (int i = 0; i < stringCount; i++)
            {
                list.Add(br.ReadString());
            }

            if (br.BaseStream.Position - p != stringTableLength)
                return null;

            br.BaseStream.Seek(stringTablePreservedLength - stringTableLength, SeekOrigin.Current);

            return list;
        }
    }
}
