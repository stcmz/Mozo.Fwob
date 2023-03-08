using Mozo.Fwob.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using SystemFieldInfo = System.Reflection.FieldInfo;

namespace Mozo.Fwob.Abstraction;

/// <summary>
/// Describes the information of a frame type
/// </summary>
public sealed class FrameInfo
{
    public IReadOnlyList<FieldInfo> Fields { get; }

    public ulong FieldTypes { get; }

    public int FrameLength { get; }

    public string FrameType { get; }

    public int KeyFieldIndex { get; }

    public int KeyFieldOffset { get; }

    public SystemFieldInfo SystemKeyFieldInfo { get; }

    public FrameInfo(Type frameType, Type keyType)
    {
        if (frameType.Name.Length > Limits.MaxFrameTypeLength)
            throw new FrameTypeNameTooLongException(frameType.Name, frameType.Name.Length);

        SystemFieldInfo[] systemFieldInfos = frameType.GetFields();

        if (systemFieldInfos.Length == 0)
            throw new NoFieldsException(frameType);

        int length = 0, fieldCount = 0;
        ulong fieldTypes = 0;

        int keyIndex = -1, firstKeyTypeIndex = -1;
        int keyOffset = 0, firstKeyTypeOffset = 0;
        SystemFieldInfo? systemKeyFieldInfo = null, systemFirstKeyTypeFieldInfo = null;

        List<FieldInfo> fieldInfoList = new();

        foreach (SystemFieldInfo systemFieldInfo in systemFieldInfos)
        {
            var fieldInfo = new FieldInfo(systemFieldInfo);

            // Skip ignored fields
            if (fieldInfo.IsIgnored)
                continue;

            if (systemFieldInfo.FieldType == keyType && firstKeyTypeIndex == -1)
            {
                firstKeyTypeIndex = fieldCount;
                firstKeyTypeOffset = length;
                systemFirstKeyTypeFieldInfo = systemFieldInfo;
            }

            if (fieldInfo.IsKey)
            {
                if (keyIndex != -1)
                    throw new AmbiguousKeyException(frameType, fieldInfo.FieldName, fieldInfoList[keyIndex].FieldName);
                if (systemFieldInfo.FieldType != keyType)
                    throw new KeyTypeMismatchException(frameType, systemFieldInfo.Name, systemFieldInfo.FieldType);

                keyIndex = fieldCount;
                keyOffset = length;
                systemKeyFieldInfo = systemFieldInfo;
            }

            length += fieldInfo.FieldLength;

            Debug.Assert((ulong)fieldInfo.FieldType < 16);
            fieldTypes |= (ulong)fieldInfo.FieldType << (fieldCount << 2);

            fieldInfoList.Add(fieldInfo);
            fieldCount++;
        }

        if (fieldInfoList.Count == 0)
            throw new NoFieldsException(frameType);

        if (fieldInfoList.Count > Limits.MaxFields)
            throw new TooManyFieldsException(frameType, fieldInfoList.Count);

        if (firstKeyTypeIndex == -1)
            throw new KeyUndefinedException(frameType);

        // Fallback to the first field of the key type if no [Key] is defined
        if (keyIndex == -1)
        {
            keyIndex = firstKeyTypeIndex;
            keyOffset = firstKeyTypeOffset;
            systemKeyFieldInfo = systemFirstKeyTypeFieldInfo;

            fieldInfoList[keyIndex].SetAsKey();
        }

        Debug.Assert(systemKeyFieldInfo != null);

        Fields = fieldInfoList;
        FrameLength = length;
        FrameType = frameType.Name;
        FieldTypes = fieldTypes;

        KeyFieldIndex = keyIndex;
        KeyFieldOffset = keyOffset;
        SystemKeyFieldInfo = systemKeyFieldInfo!;
    }

    public static FrameInfo FromTypes<TFrame, TKey>()
    {
        return new(typeof(TFrame), typeof(TKey));
    }
}
