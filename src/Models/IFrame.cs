using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace Fwob.Models
{
    public interface IFrame<TKey>
        where TKey : struct, IComparable<TKey>
    {
        TKey Key { get; }

        void SerializeKey(BinaryWriter bw);

        TKey DeserializeKey(BinaryReader br);

        void SerializeFrame(BinaryWriter bw);

        void DeserializeFrame(BinaryReader br);
    }
}
