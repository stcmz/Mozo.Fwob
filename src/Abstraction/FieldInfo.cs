using Mozo.Fwob.Exceptions;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using SystemFieldInfo = System.Reflection.FieldInfo;

namespace Mozo.Fwob.Abstraction;

/// <summary>
/// Describes the information of a field in a frame type
/// </summary>
public sealed class FieldInfo
{
    public bool IsIgnored { get; }

    public bool IsKey { get; private set; }

    public int FieldLength { get; }

    public FieldType FieldType { get; }

    public string FieldName { get; }

    public FieldInfo(SystemFieldInfo fieldInfo)
    {
        // Use assertions to detect develop time issues (bugs), while use exceptions to detect runtime issues (external errors).
        if (fieldInfo.Name.Length > Limits.MaxFieldNameLength)
            throw new FieldNameTooLongException(fieldInfo.Name, fieldInfo.Name.Length);

        FieldName = fieldInfo.Name;

        Type type = fieldInfo.FieldType;

        IsKey = fieldInfo.GetCustomAttribute<KeyAttribute>(false) != null;
        IsIgnored = fieldInfo.GetCustomAttribute<IgnoreAttribute>(false) != null;

        // Skip ignored fields
        if (IsIgnored)
        {
            if (IsKey)
                throw new KeyIgnoredException(fieldInfo.Name);
            return;
        }

        bool isIndex = fieldInfo.GetCustomAttribute<StringTableIndexAttribute>(false) != null;
        LengthAttribute? lengthAttr = fieldInfo.GetCustomAttribute<LengthAttribute>(false);

        // As a structured builtin type, the IsPrimitive property of decimal is false
        if (type.IsPrimitive || type == typeof(decimal))
        {
            if (lengthAttr != null)
                throw new FieldLengthNotAllowedException(fieldInfo.Name, type);

            if (type == typeof(sbyte) || type == typeof(short) || type == typeof(int) || type == typeof(long))
                FieldType = FieldType.SignedInteger;
            else if (type == typeof(byte) || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong))
                FieldType = FieldType.UnsignedInteger;
            else if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                FieldType = FieldType.FloatingPoint;
            else
                throw new FieldTypeNotSupportedException(fieldInfo.Name, type);

            if (isIndex)
            {
                Debug.Assert(FieldType != FieldType.FloatingPoint, $"Index on field {fieldInfo.Name} of type {type} is not supported.");
                FieldType = FieldType.StringTableIndex;
            }

            // Size of any primitive type is less than byte.MaxValue (255).
            FieldLength = (byte)Marshal.SizeOf(type);
        }
        else if (type == typeof(string))
        {
            Debug.Assert(!isIndex, $"Index on field {fieldInfo.Name} of type {type} is not supported.");
            FieldType = FieldType.Utf8String;

            if (lengthAttr == null)
                throw new FieldLengthUndefinedException(fieldInfo.Name);

            if (lengthAttr.Length <= 0 || lengthAttr.Length > byte.MaxValue)
                throw new FieldLengthOutOfRangeException(fieldInfo.Name, lengthAttr.Length);

            FieldLength = (byte)lengthAttr.Length;
        }
        else
        {
            throw new FieldTypeNotSupportedException(fieldInfo.Name, type);
        }
    }

    public void SetAsKey()
    {
        IsKey = true;
    }
}
