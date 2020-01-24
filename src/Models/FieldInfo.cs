using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Fwob.Models
{
    public class FieldInfo
    {
        public int FieldLength { get; private set; }

        public FieldType FieldType { get; private set; }

        public string FieldName { get; private set; }

        public static FieldInfo FromSystem(System.Reflection.FieldInfo field)
        {
            // Use assertions to detect develop time issues (bugs), while use exceptions to detect runtime issues (external errors).
            var fieldInfo = new FieldInfo();

            Debug.Assert(field.Name.Length <= FwobLimits.MaxFieldNameLength);
            fieldInfo.FieldName = field.Name;

            var type = field.FieldType;
            bool isIndex = field.GetCustomAttributes(typeof(StringTableIndexAttribute), false).Length > 0;
            var lengthAttr = field.GetCustomAttributes(typeof(LengthAttribute), false).Cast<LengthAttribute>().FirstOrDefault();

            if (type.IsPrimitive)
            {
                Debug.Assert(lengthAttr == null, $"Length on field {field.Name} of type {type} is not supported.");

                if (type == typeof(sbyte) || type == typeof(short) || type == typeof(int) || type == typeof(long))
                    fieldInfo.FieldType = FieldType.SignedInteger;
                else if (type == typeof(byte) || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong))
                    fieldInfo.FieldType = FieldType.UnsignedInteger;
                else if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                    fieldInfo.FieldType = FieldType.FloatingPoint;
                else
                    Debug.Fail($"Field {field.Name} of type {type} is not supported.");

                if (isIndex)
                {
                    Debug.Assert(fieldInfo.FieldType != FieldType.FloatingPoint, $"Index on field {field.Name} of type {type} is not supported.");
                    fieldInfo.FieldType = FieldType.StringTableIndex;
                }

                // Size of any primitive type is less than byte.MaxValue (255).
                fieldInfo.FieldLength = (byte)Marshal.SizeOf(type);
            }
            else if (type == typeof(string))
            {
                Debug.Assert(!isIndex, $"Index on field {field.Name} of type {type} is not supported.");
                fieldInfo.FieldType = FieldType.Utf8String;

                Debug.Assert(lengthAttr != null, $"Field {field.Name} does not have a Length attribute defined.");
                Debug.Assert(lengthAttr.Length > 0 && lengthAttr.Length <= byte.MaxValue, $"Field {field.Name} has an invalid Length {lengthAttr.Length} defined.");
                fieldInfo.FieldLength = (byte)lengthAttr.Length;
            }
            else
            {
                Debug.Fail($"Field {field.Name} of type {type} is not supported.");
            }

            return fieldInfo;
        }
    }
}
