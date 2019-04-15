using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Fwob.Extensions
{
    public static class BinaryReaderExtensions
    {
        public static TValue Read<TValue>(this BinaryReader br)
        {
            return (TValue)br.Read(typeof(TValue));
        }

        public static object Read(this BinaryReader br, Type type)
        {
            if (type == typeof(byte))
                return br.ReadByte();
            if (type == typeof(sbyte))
                return br.ReadSByte();

            if (type == typeof(short))
                return br.ReadInt16();
            if (type == typeof(int))
                return br.ReadInt32();
            if (type == typeof(long))
                return br.ReadInt64();

            if (type == typeof(ushort))
                return br.ReadUInt16();
            if (type == typeof(uint))
                return br.ReadUInt32();
            if (type == typeof(ulong))
                return br.ReadUInt64();

            if (type == typeof(float))
                return br.ReadSingle();
            if (type == typeof(double))
                return br.ReadDouble();
            if (type == typeof(decimal))
                return br.ReadDecimal();

            if (type == typeof(bool))
                return br.ReadBoolean();
            if (type == typeof(char))
                return br.ReadChar();
            if (type == typeof(string))
                return br.ReadString();

            throw new ArgumentOutOfRangeException($"Invalid type {type.Name} for extension method BinaryReader.Read().");
        }
    }
}
