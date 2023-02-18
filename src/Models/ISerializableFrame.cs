using System;
using System.IO;

namespace Fwob.Models
{
    public interface ISerializableFrame<TKey> : IFrame<TKey>
        where TKey : struct, IComparable<TKey>
    {
        void SerializeKey(BinaryWriter bw);

        TKey DeserializeKey(BinaryReader br);

        void SerializeFrame(BinaryWriter bw);

        void DeserializeFrame(BinaryReader br);
    }
}
