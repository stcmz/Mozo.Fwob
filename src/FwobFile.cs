using System;
using System.Collections.Generic;
using System.Text;

namespace Fwob
{
    /// <summary>
    /// FWOB ver1 string table definition
    /// A Fixed-Width Ordered Binary (FWOB) file consisits of 3 sections:
    ///   1 Header: pos 0: 210 bytes
    ///   2 String Table: pos 210: length StringTablePreservedLength
    ///   3 Data Frames: pos 210 + StringTablePreservedLength, length FrameCount * FrameLength
    /// </summary>
    public class FwobFile<TFrame>
    {
        public Header Header { get; set; }

        public List<FieldInfo> Fields { get; set; }

        public List<string> StringTable { get; set; }

        public List<TFrame> DataFrames { get; set; }
    }
}
