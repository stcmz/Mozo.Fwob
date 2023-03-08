using Mozo.Fwob.Abstraction;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Mozo.Fwob.Extensions;

public static class FwobHeaderWriter
{
    public static void WriteHeader(this BinaryWriter bw, FwobHeader header)
    {
        //*********************** Signature and Version (5 bytes) ************************//

        // pos 0: 4 bytes
        Debug.Assert(FwobHeader.Signature.Length == 4);
        bw.Write(FwobHeader.Signature.ToCharArray());

        // pos 4: 1 byte
        Debug.Assert(header.Version == FwobHeader.CurrentVersion);
        bw.Write(header.Version);

        //*********************** Descriptors of Fields (153 bytes) ************************//

        // pos 5: 1 byte (allow up to 16 fields)
        Debug.Assert(header.FieldCount <= Limits.MaxFields);
        bw.Write(header.FieldCount);

        // pos 6: 16 bytes (allow up to 16 fields)
        Debug.Assert(header.FieldLengths.Length == header.FieldCount);
        bw.Write(header.FieldLengths);

        if (header.FieldLengths.Length < Limits.MaxFields)
            bw.Write(new byte[Limits.MaxFields - header.FieldLengths.Length]);

        // pos 22: 8 bytes (up to 16 types, each has 4 bits, up to 16 types defined on FieldType)
        bw.Write(header.FieldTypes);

        // pos 30: 128 bytes (allow up to 16*8 chars)
        Debug.Assert(header.FieldNames.Length == header.FieldCount);

        for (int i = 0; i < header.FieldCount; i++)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(header.FieldNames[i]));
            Debug.Assert(header.FieldNames[i].Length <= Limits.MaxFieldNameLength);
            bw.Write(header.FieldNames[i].PadRight(Limits.MaxFieldNameLength).ToCharArray());
        }

        if (header.FieldNames.Length < Limits.MaxFields)
            bw.Write(new byte[(Limits.MaxFields - header.FieldNames.Length) * Limits.MaxFieldNameLength]);

        //*********************** Size of String Tables (12 bytes) ************************//

        // pos 158: 4 bytes
        Debug.Assert(header.StringCount >= 0);
        bw.Write(header.StringCount);

        // pos 162: 4 bytes
        Debug.Assert(header.StringTableLength >= 0);
        bw.Write(header.StringTableLength);

        // pos 166: 4 bytes
        Debug.Assert(header.StringTablePreservedLength >= header.StringTableLength);
        bw.Write(header.StringTablePreservedLength);

        //*********************** Frames (44 bytes) ************************//

        // pos 170: 8 bytes
        Debug.Assert(header.FrameCount >= 0);
        bw.Write(header.FrameCount);

        // pos 178: 4 bytes, should be the sum of FieldLengths
        Debug.Assert(header.FrameLength == header.FieldLengths.Take(header.FieldCount).Select(o => (int)o).Sum());
        bw.Write(header.FrameLength);

        // pos 182: 16 bytes (up to 16 chars)
        Debug.Assert(!string.IsNullOrWhiteSpace(header.FrameType));
        Debug.Assert(header.FrameType.Length <= Limits.MaxFrameTypeLength);
        bw.Write(header.FrameType.PadRight(Limits.MaxFrameTypeLength).ToCharArray());

        // pos 198: 16 bytes (up to 16 chars)
        Debug.Assert(!string.IsNullOrWhiteSpace(header.Title));
        Debug.Assert(header.Title.Length <= Limits.MaxTitleLength);
        bw.Write(header.Title.PadRight(Limits.MaxTitleLength).ToCharArray());
    }

    public static void UpdateTitle(this BinaryWriter bw, FwobHeader header)
    {
        bw.Seek(198, SeekOrigin.Begin);

        // pos 198: 16 bytes (up to 16 chars)
        Debug.Assert(!string.IsNullOrWhiteSpace(header.Title));
        Debug.Assert(header.Title.Length <= Limits.MaxTitleLength);
        bw.Write(header.Title.PadRight(Limits.MaxTitleLength).ToCharArray());
    }

    public static void UpdateFrameCount(this BinaryWriter bw, FwobHeader header)
    {
        bw.Seek(170, SeekOrigin.Begin);

        // pos 170: 8 bytes
        Debug.Assert(header.FrameCount >= 0);
        bw.Write(header.FrameCount);
    }

    public static void UpdateStringTableLength(this BinaryWriter bw, FwobHeader header)
    {
        bw.Seek(158, SeekOrigin.Begin);

        // pos 158: 4 bytes
        Debug.Assert(header.StringCount >= 0);
        bw.Write(header.StringCount);

        // pos 162: 4 bytes
        Debug.Assert(header.StringTableLength >= 0);
        bw.Write(header.StringTableLength);
    }

}
