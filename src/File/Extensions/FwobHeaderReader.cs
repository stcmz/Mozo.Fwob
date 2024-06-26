﻿using Mozo.Fwob.Abstraction;
using System.IO;
using System.Linq;

namespace Mozo.Fwob.Extensions;

public static class FwobHeaderReader
{
    public static FwobHeader? ReadHeader(this BinaryReader br)
    {
        // check least file length
        if (br.BaseStream.Length < FwobHeader.HeaderLength)
            return null;

        //*********************** Signature and Version (5 bytes) ************************//

        // pos 0: 4 bytes
        string sig = new(br.ReadChars(4));
        if (sig != FwobHeader.Signature)
            return null;

        FwobHeader header = new()
        {
            // pos 4: 1 byte
            Version = br.ReadByte()
        };

        if (header.Version != FwobHeader.CurrentVersion)
            return null;

        //*********************** Descriptors of Fields (153 bytes) ************************//

        // pos 5: 1 byte (allow up to 16 fields)
        header.FieldCount = br.ReadByte();

        if (header.FieldCount > Limits.MaxFields)
            return null;

        // pos 6: 16 bytes (allow up to 16 fields)
        header.FieldLengths = br.ReadBytes(Limits.MaxFields).Take(header.FieldCount).ToArray();

        // pos 22: 8 bytes (up to 16 types, each has 4 bits, up to 16 types defined on FieldType)
        header.FieldTypes = br.ReadUInt64();

        // pos 30: 128 bytes (allow up to 16*8 chars)
        header.FieldNames = new string[Limits.MaxFields];
        for (int i = 0; i < Limits.MaxFields; i++)
        {
            header.FieldNames[i] = new string(br.ReadChars(Limits.MaxFieldNameLength)).Trim();
            if (i < header.FieldCount && header.FieldNames[i].Length == 0)
                return null;
        }
        header.FieldNames = header.FieldNames.Take(header.FieldCount).ToArray();

        //*********************** Size of String Tables (12 bytes) ************************//

        // pos 158: 4 bytes
        header.StringCount = br.ReadInt32();

        if (header.StringCount < 0)
            return null;

        // pos 162: 4 bytes
        header.StringTableLength = br.ReadInt32();

        if (header.StringTableLength < 0)
            return null;

        // pos 166: 4 bytes
        header.StringTablePreservedLength = br.ReadInt32();

        if (header.StringTablePreservedLength < header.StringTableLength)
            return null;

        //*********************** Frames (44 bytes) ************************//

        // pos 170: 8 bytes
        header.FrameCount = br.ReadInt64();

        // pos 178: 4 bytes, should be the sum of FieldLengths
        header.FrameLength = br.ReadInt32();

        if (header.FrameLength != header.FieldLengths.Take(header.FieldCount).Select(o => (int)o).Sum())
            return null;

        // pos 182: 16 bytes (up to 16 chars)
        header.FrameType = new string(br.ReadChars(Limits.MaxFrameTypeLength)).Trim();

        if (header.FrameType.Length == 0)
            return null;

        // pos 198: 16 bytes (up to 16 chars)
        header.Title = new string(br.ReadChars(Limits.MaxTitleLength)).Trim();

        if (header.Title.Length == 0)
            return null;

        return header;
    }
}
