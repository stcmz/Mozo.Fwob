using Fwob.Models;
using System;
using System.Diagnostics;
using System.Linq;

namespace Fwob.Header
{
    //*********************** Total Header Size (214 bytes) ************************//
    public class FwobHeader
    {
        public const int CurrentVersion = 1;
        public const int HeaderLength = 214;
        public const int DefaultStringTablePreservedLength = 2048 - HeaderLength;

        //*********************** Signature and Version (5 bytes) ************************//

        // pos 0: 4 bytes
        public const string Signature = "FWOB";

        // pos 4: 1 byte
        public byte Version { get; set; }

        //*********************** Descriptors of Fields (153 bytes) ************************//

        // pos 5: 1 byte (allow up to 16 fields)
        public byte FieldCount { get; set; }

        // pos 6: 16 bytes (allow up to 16 fields)
        public byte[] FieldLengths { get; set; }

        // pos 22: 8 bytes (up to 16 types, each has 3 bits, up to 8 types defined on FieldType)
        public ulong FieldTypes { get; set; }

        // pos 30: 128 bytes (allow up to 16*8 chars)
        public string[] FieldNames { get; set; }

        //*********************** Size of String Tables (12 bytes) ************************//

        // pos 158: 4 bytes
        public int StringCount { get; set; }

        // pos 162: 4 bytes
        public int StringTableLength { get; set; }

        // pos 166: 4 bytes
        public int StringTablePreservedLength { get; set; }

        //*********************** Frames (44 bytes) ************************//

        // pos 170: 8 bytes
        public long FrameCount { get; set; }

        // pos 178: 4 bytes (should be the sum of FieldLengths)
        public int FrameLength { get; set; }

        // pos 182: 16 bytes (up to 16 chars)
        public string FrameType { get; set; }

        // pos 198: 16 bytes (up to 16 chars)
        public string Title { get; set; }

        public long StringTablePosition => HeaderLength;

        public long StringTableEnding => HeaderLength + StringTableLength;

        public long FirstFramePosition => StringTablePosition + StringTablePreservedLength;

        public long LastFramePosition => FirstFramePosition + FrameLength * FrameCount;

        public long FileLength => StringTablePosition + StringTablePreservedLength + FrameLength * FrameCount;

        public static FwobHeader CreateNew<TFrame>(string title)
        {
            var frameType = typeof(TFrame);
            Debug.Assert(frameType.Name.Length <= FwobLimits.MaxFrameTypeLength);

            if (string.IsNullOrEmpty(title))
                throw new ArgumentNullException(nameof(title), "Argument should not be null or empty");
            if (title.Length > FwobLimits.MaxTitleLength)
                throw new ArgumentOutOfRangeException(nameof(title), title, $"Length of argument exceeded MaxTitleLength {FwobLimits.MaxTitleLength}");

            var frameInfo = FrameInfo.FromSystem(frameType);

            var header = new FwobHeader
            {
                // "FWOB": Signature
                Version = CurrentVersion,

                FieldCount = (byte)frameInfo.Fields.Count,
                FieldLengths = frameInfo.Fields.Select(o => (byte)o.FieldLength).ToArray(),
                FieldTypes = frameInfo.FieldTypes,
                FieldNames = frameInfo.Fields.Select(o => o.FieldName).ToArray(),

                StringCount = 0,
                StringTableLength = 0,
                StringTablePreservedLength = DefaultStringTablePreservedLength,

                FrameCount = 0,
                FrameLength = frameInfo.FrameLength,
                FrameType = frameType.Name,
                Title = title,
            };

            return header;
        }

        public FrameInfo GetFrameInfo<TFrame>()
        {
            var frameInfo = FrameInfo.FromSystem<TFrame>();

            if (FrameType != frameInfo.FrameType)
                return null;
            if (FrameLength != frameInfo.FrameLength)
                return null;

            if (FieldCount != frameInfo.Fields.Count)
                return null;
            if (FieldTypes != frameInfo.FieldTypes)
                return null;

            for (int i = 0; i < frameInfo.Fields.Count; i++)
            {
                var fi = frameInfo.Fields[i];
                if (FieldLengths[i] != fi.FieldLength)
                    return null;
                if (FieldNames[i] != fi.FieldName)
                    return null;
            }

            return frameInfo;
        }
    }
}
