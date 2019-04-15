using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Fwob.Extensions
{
    public static class BinaryWriterExtensions
    {
        public static void Write(this BinaryWriter bw, object val)
        {
            var type = val.GetType();
            if (type == typeof(sbyte))
                bw.Write((sbyte)val);
            else if (type == typeof(byte))
                bw.Write((byte)val);

            else if (type == typeof(short))
                bw.Write((short)val);
            else if (type == typeof(int))
                bw.Write((int)val);
            else if (type == typeof(long))
                bw.Write((long)val);

            else if (type == typeof(ushort))
                bw.Write((ushort)val);
            else if (type == typeof(uint))
                bw.Write((uint)val);
            else if (type == typeof(ulong))
                bw.Write((ulong)val);

            else if (type == typeof(float))
                bw.Write((float)val);
            else if (type == typeof(double))
                bw.Write((double)val);
            else if (type == typeof(decimal))
                bw.Write((decimal)val);

            else if (type == typeof(bool))
                bw.Write((bool)val);
            else if (type == typeof(char))
                bw.Write((char)val);
            else if (type == typeof(string))
                bw.Write((string)val);

            else
                throw new ArgumentOutOfRangeException($"Invalid type {type.Name} for extension method BinaryReader.Read().");
        }
    }
}
