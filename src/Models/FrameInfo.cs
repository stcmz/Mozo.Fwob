using Mozo.Fwob.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Mozo.Fwob.Models;

public class FrameInfo
{
    public IReadOnlyList<FieldInfo> Fields { get; private set; } = new List<FieldInfo>();

    public ulong FieldTypes { get; private set; }

    public int FrameLength { get; private set; }

    public string? FrameType { get; private set; }

    public int KeyIndex { get; private set; }

    public static FrameInfo FromSystem(Type frameType, Type keyType)
    {
        if (frameType.Name.Length > FwobLimits.MaxFrameTypeLength)
            throw new FrameTypeNameTooLongException(frameType.Name, frameType.Name.Length);

        System.Reflection.FieldInfo[] fields = frameType.GetFields();

        if (fields.Length == 0)
            throw new NoFieldsException(frameType);

        int length = 0, fieldCount = 0, keyIndex = -1, firstKeyTypeIndex = -1;
        ulong fieldTypes = 0;

        List<FieldInfo> fis = new();

        foreach (System.Reflection.FieldInfo field in fields)
        {
            var fi = FieldInfo.FromSystem(field);
            if (fi == null)
                continue;

            if (field.FieldType == keyType && firstKeyTypeIndex == -1)
                firstKeyTypeIndex = fieldCount;

            if (fi.IsKey)
            {
                if (keyIndex != -1)
                    throw new AmbiguousKeyException(frameType, fi.FieldName, fis[keyIndex].FieldName);
                if (field.FieldType != keyType)
                    throw new KeyTypeMismatchException(frameType, field.Name, field.FieldType);
                keyIndex = fieldCount;
            }

            length += fi.FieldLength;

            Debug.Assert((ulong)fi.FieldType < 16);
            fieldTypes |= (ulong)fi.FieldType << (fieldCount << 2);

            fis.Add(fi);
            fieldCount++;
        }

        if (fis.Count == 0)
            throw new NoFieldsException(frameType);

        if (fis.Count > FwobLimits.MaxFields)
            throw new TooManyFieldsException(frameType, fis.Count);

        if (firstKeyTypeIndex == -1)
            throw new KeyUndefinedException(frameType);

        if (keyIndex == -1)
        {
            keyIndex = firstKeyTypeIndex;
            fis[keyIndex].IsKey = true;
        }

        return new FrameInfo
        {
            Fields = fis,
            FrameLength = length,
            FrameType = frameType.Name,
            FieldTypes = fieldTypes,
            KeyIndex = keyIndex,
        };
    }

    public static FrameInfo FromSystem<TFrame, TKey>()
    {
        return FromSystem(typeof(TFrame), typeof(TKey));
    }
}
