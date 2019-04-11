using Fwob.Models;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Fwob.Header
{
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
            Debug.Assert(header.FieldCount <= FwobLimits.MaxFields);
            bw.Write(header.FieldCount);

            // pos 6: 16 bytes (allow up to 16 fields)
            Debug.Assert(header.FieldLengths != null);
            Debug.Assert(header.FieldLengths.Length == header.FieldCount);
            bw.Write(header.FieldLengths);

            if (header.FieldLengths.Length < FwobLimits.MaxFields)
                bw.Write(new byte[FwobLimits.MaxFields - header.FieldLengths.Length]);

            // pos 22: 8 bytes (up to 16 types, each has 4 bits, up to 16 types defined on FieldType)
            bw.Write(header.FieldTypes);

            // pos 30: 128 bytes (allow up to 16*8 chars)
            Debug.Assert(header.FieldNames != null);
            Debug.Assert(header.FieldNames.Length == header.FieldCount);

            for (int i = 0; i < header.FieldCount; i++)
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(header.FieldNames[i]));
                Debug.Assert(header.FieldNames[i].Length < FwobLimits.MaxFieldNameLength);
                bw.Write(header.FieldNames[i].PadRight(FwobLimits.MaxFieldNameLength).ToCharArray());
            }

            if (header.FieldNames.Length < FwobLimits.MaxFields)
                bw.Write(new byte[(FwobLimits.MaxFields - header.FieldNames.Length) * FwobLimits.MaxFieldNameLength]);

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
            Debug.Assert(header.FrameType.Length <= FwobLimits.MaxFrameTypeLength);
            bw.Write(header.FrameType.PadRight(FwobLimits.MaxFrameTypeLength).ToCharArray());

            // pos 198: 16 bytes (up to 16 chars)
            Debug.Assert(!string.IsNullOrWhiteSpace(header.Title));
            Debug.Assert(header.Title.Length <= FwobLimits.MaxTitleLength);
            bw.Write(header.Title.PadRight(FwobLimits.MaxTitleLength).ToCharArray());
        }
    }
}
