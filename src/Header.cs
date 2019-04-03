namespace Fwob
{
    /// <summary>
    /// A Fixed-Width Ordered Binary (FWOB) file consisits of 3 sections:
    ///   1 Header: pos 0: 210 bytes
    ///   2 String Table: pos 210: length StringTablePreservedLength
    ///   3 Data Frames: pos 210 + StringTablePreservedLength, length FrameCount * FrameLength
    /// </summary>
    public class Header
    {
        public const int HeaderLength = 210;
        public const int MaxFields = 16;

        //*********************** Signature and Version (5 bytes) ************************//

        // pos 0: 4 bytes
        public const string Signature = "FWOB";

        // pos 4: 1 byte
        public byte Version { get; set; }

        //*********************** Descriptors of Fields (149 bytes) ************************//

        // pos 5: 1 byte (allow up to 16 fields)
        public byte FieldCount { get; set; }

        // pos 6: 16 bytes (allow up to 16 fields)
        public byte[] FieldLengths { get; set; }

        // pos 22: 4 bytes (up to 16 types, each has 2 bits: 00 integer, 01 floating, 10 string, 11 index)
        public uint FieldTypes { get; set; }

        // pos 26: 128 bytes (allow up to 16*8 chars)
        public string[] FieldNames { get; set; }

        //*********************** Size of String Tables (12 bytes) ************************//

        // pos 154: 4 bytes
        public int StringCount { get; set; }

        // pos 158: 4 bytes
        public int StringTableLength { get; set; }

        // pos 162: 4 bytes
        public int StringTablePreservedLength { get; set; }

        //*********************** Frames (44 bytes) ************************//

        // pos 166: 8 bytes
        public long FrameCount { get; set; }

        // pos 174: 4 bytes (should be the sum of FieldLengths)
        public int FrameLength { get; set; }

        // pos 178: 16 bytes (up to 16 chars)
        public string FrameName { get; set; }

        // pos 194: 16 bytes (up to 16 chars)
        public string FrameType { get; set; }
    }
}
