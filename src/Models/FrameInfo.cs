using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Fwob.Models
{
    public class FrameInfo
    {
        public IReadOnlyList<FieldInfo> Fields { get; private set; }

        public ulong FieldTypes { get; set; }

        public int FrameLength { get; private set; }

        public string FrameType { get; private set; }

        public static FrameInfo FromSystem(Type frameType)
        {
            var fields = frameType.GetFields();

            Debug.Assert(fields.Length > 0 && fields.Length <= FwobLimits.MaxFields);

            int length = 0;
            ulong fieldTypes = 0;

            var fis = new List<FieldInfo>();
            for (int i = 0; i < fields.Length; i++)
            {
                var fi = FieldInfo.FromSystem(fields[i]);
                length += fi.FieldLength;

                Debug.Assert((ulong)fi.FieldType < 16);
                fieldTypes |= (ulong)fi.FieldType << (i << 2);

                fis.Add(fi);
            }

            return new FrameInfo
            {
                Fields = fis,
                FrameLength = length,
                FrameType = frameType.Name,
                FieldTypes = fieldTypes,
            };
        }

        public static FrameInfo FromSystem<TFrame>()
        {
            return FromSystem(typeof(TFrame));
        }
    }
}
