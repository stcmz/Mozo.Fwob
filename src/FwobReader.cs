using System.IO;

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

            //*********************** Descriptors of Fields (149 bytes) ************************//

            // pos 5: 1 byte (allow up to 16 fields)
            header.FieldCount = br.ReadByte();

            // pos 6: 16 bytes (allow up to 16 fields)
            header.FieldLengths = br.ReadBytes(16);

            // pos 22: 4 bytes (up to 16 types, each has 2 bits: 00 integer, 01 floating, 10 string, 11 index)
            header.FieldTypes = br.ReadUInt32();

            // pos 26: 128 bytes (allow up to 16*8 chars)
            header.FieldNames = new string[16];
            for (int i = 0; i < Header.MaxFields; i++)
            {
                header.FieldNames[i] = new string(br.ReadChars(8)).Trim();
            }

            //*********************** Size of String Tables (12 bytes) ************************//

            // pos 154: 4 bytes
            header.StringCount = br.ReadInt32();

            // pos 158: 4 bytes
            header.StringTableLength = br.ReadInt32();

            // pos 162: 4 bytes
            header.StringTablePreservedLength = br.ReadInt32();

            //*********************** Frames (44 bytes) ************************//

            // pos 166: 8 bytes
            header.FrameCount = br.ReadInt64();

            // pos 174: 4 bytes, should be the sum of FieldLengths
            header.FrameLength = br.ReadInt32();

            // pos 178: 16 bytes (up to 16 chars)
            header.FrameName = new string(br.ReadChars(16));

            // pos 194: 16 bytes (up to 16 chars)
            header.FrameType = new string(br.ReadChars(16));

            return header;
        }
    }
}
