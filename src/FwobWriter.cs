using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Fwob
{
    public static class FwobWriter
    {
        public static void WriteHeader(this BinaryWriter bw, Header header)
        {
            //*********************** Signature and Version (5 bytes) ************************//

            // pos 0: 4 bytes
            Debug.Assert(Header.Signature.Length == 4);
            bw.Write(Header.Signature.ToCharArray());

            // pos 4: 1 byte
            Debug.Assert(header.Version == Header.CurrentVersion);
            bw.Write(header.Version);

            //*********************** Descriptors of Fields (149 bytes) ************************//

            // pos 5: 1 byte (allow up to 16 fields)
            Debug.Assert(header.FieldCount <= 16);
            bw.Write(header.FieldCount);

            // pos 6: 16 bytes (allow up to 16 fields)
            Debug.Assert(header.FieldLengths?.Length == Header.MaxFields);
            bw.Write(header.FieldLengths);

            // pos 22: 4 bytes (up to 16 types, each has 2 bits: 00 integer, 01 floating, 10 string, 11 index)
            bw.Write(header.FieldTypes);

            // pos 26: 128 bytes (allow up to 16*8 chars)
            Debug.Assert(header.FieldNames?.Length == Header.MaxFields);
            for (int i = 0; i < Header.MaxFields; i++)
            {
                if (i < header.FieldCount)
                {
                    Debug.Assert(!string.IsNullOrWhiteSpace(header.FieldNames[i]));
                    Debug.Assert(header.FieldNames[i]?.Length < Header.MaxFieldNameLength);
                    bw.Write(header.FieldNames[i].PadRight(Header.MaxFieldNameLength));
                }
                else
                {
                    Debug.Assert(string.IsNullOrEmpty(header.FieldNames[i]));
                    bw.Write(string.Empty.PadRight(Header.MaxFieldNameLength));
                }
            }

            //*********************** Size of String Tables (12 bytes) ************************//

            // pos 154: 4 bytes
            Debug.Assert(header.StringCount >= 0);
            bw.Write(header.StringCount);

            // pos 158: 4 bytes
            Debug.Assert(header.StringTableLength >= 0);
            bw.Write(header.StringTableLength);

            // pos 162: 4 bytes
            Debug.Assert(header.StringTablePreservedLength >= header.StringTableLength);
            bw.Write(header.StringTablePreservedLength);

            //*********************** Frames (44 bytes) ************************//

            // pos 166: 8 bytes
            Debug.Assert(header.FrameCount > 0);
            bw.Write(header.FrameCount);

            // pos 174: 4 bytes, should be the sum of FieldLengths
            Debug.Assert(header.FrameLength == header.FieldLengths.Take(header.FieldCount).Cast<int>().Sum());
            bw.Write(header.FrameLength);

            // pos 178: 16 bytes (up to 16 chars)
            Debug.Assert(!string.IsNullOrWhiteSpace(header.FrameName));
            Debug.Assert(header.FrameName.Length <= Header.MaxFrameNameLength);
            bw.Write(header.FrameName.PadRight(Header.MaxFrameNameLength).ToCharArray());

            // pos 194: 16 bytes (up to 16 chars)
            Debug.Assert(!string.IsNullOrWhiteSpace(header.FrameType));
            Debug.Assert(header.FrameType.Length <= Header.MaxFrameTypeLength);
            bw.Write(header.FrameType.PadRight(Header.MaxFrameTypeLength).ToCharArray());
        }

        public static long WriteStringTable(this BinaryWriter bw, List<string> stringTable)
        {
            var p = bw.BaseStream.Position;

            foreach (string s in stringTable)
            {
                bw.Write(s);
            }

            return bw.BaseStream.Position - p;
        }
    }
}
