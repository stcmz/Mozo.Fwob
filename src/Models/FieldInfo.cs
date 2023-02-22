using Mozo.Fwob.Exceptions;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Mozo.Fwob.Models;

public class FieldInfo
{
    public bool IsKey { get; internal set; }

    public int FieldLength { get; private set; }

    public FieldType FieldType { get; private set; }

    public string FieldName { get; private set; } = string.Empty;

    public static FieldInfo? FromSystem(System.Reflection.FieldInfo field)
    {
        // Use assertions to detect develop time issues (bugs), while use exceptions to detect runtime issues (external errors).
        FieldInfo fieldInfo = new();

        if (field.Name.Length > FwobLimits.MaxFieldNameLength)
            throw new FieldNameTooLongException(field.Name, field.Name.Length);

        fieldInfo.FieldName = field.Name;

        Type type = field.FieldType;

        fieldInfo.IsKey = field.GetCustomAttribute<KeyAttribute>(false) != null;
        bool isIgnored = field.GetCustomAttribute<IgnoreAttribute>(false) != null;

        // Skip ignored fields
        if (isIgnored)
        {
            if (fieldInfo.IsKey)
                throw new KeyIgnoredException(field.Name);
            return null;
        }

        bool isIndex = field.GetCustomAttribute<StringTableIndexAttribute>(false) != null;
        LengthAttribute? lengthAttr = field.GetCustomAttribute<LengthAttribute>(false);

        if (type.IsPrimitive)
        {
            if (lengthAttr != null)
                throw new FieldLengthNotAllowedException(field.Name, type);

            if (type == typeof(sbyte) || type == typeof(short) || type == typeof(int) || type == typeof(long))
                fieldInfo.FieldType = FieldType.SignedInteger;
            else if (type == typeof(byte) || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong))
                fieldInfo.FieldType = FieldType.UnsignedInteger;
            else if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                fieldInfo.FieldType = FieldType.FloatingPoint;
            else
                throw new FieldTypeNotSupportedException(field.Name, type);

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

            if (lengthAttr == null)
                throw new FieldLengthUndefinedException(field.Name);

            if (lengthAttr.Length <= 0 || lengthAttr.Length > byte.MaxValue)
                throw new FieldLengthOutOfRangeException(field.Name, lengthAttr.Length);

            fieldInfo.FieldLength = (byte)lengthAttr.Length;
        }
        else
        {
            throw new FieldTypeNotSupportedException(field.Name, type);
        }

        return fieldInfo;
    }
}
