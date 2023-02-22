using Mozo.Fwob.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Mozo.Fwob.Frame;

/// <summary>
/// This class does not throw exception on schema errors and should be used after <typeparamref name="TFrame"/> has been verified.
/// </summary>
/// <typeparam name="TFrame"></typeparam>
/// <typeparam name="TKey"></typeparam>
internal static class FwobFrameReaderGenerator<TFrame, TKey>
{
    private static (FieldInfo Field, int Offset)? GetKeyField()
    {
        (FieldInfo Field, int Offset)? firstField = null, keyField = null;
        int currOffset = 0;

        foreach (FieldInfo fieldInfo in typeof(TFrame).GetFields())
        {
            bool isKey = fieldInfo.GetCustomAttribute<KeyAttribute>(false) != null;

            // Skip ignored fields
            bool isIgnored = fieldInfo.GetCustomAttribute<IgnoreAttribute>(false) != null;
            if (isIgnored)
            {
                Debug.Assert(!isKey, $"Key {fieldInfo.Name} is ignored");
                continue;
            }

            // Get length of field
            int fieldLength;
            if (fieldInfo.FieldType == typeof(string))
            {
                fieldLength = fieldInfo.GetCustomAttribute<LengthAttribute>(false)!.Length;
            }
            else
            {
                fieldLength = Marshal.SizeOf(fieldInfo.FieldType);
            }

            // Skip type-mismatched fields
            if (fieldInfo.FieldType != typeof(TKey))
            {
                currOffset += fieldLength;
                continue;
            }

            // As a fallback option
            firstField ??= (fieldInfo, currOffset);

            // Skip non-key fields
            if (!isKey)
            {
                currOffset += fieldLength;
                continue;
            }

            // Too many fields are marked as key
            Debug.Assert(keyField == null, $"Multiple fields are annotated as key");

            // Found the key
            keyField = (fieldInfo, currOffset);
        }

        return keyField ?? firstField;
    }

    /// <summary>
    /// Generate a function that gets the key of the given frame
    /// Generated function: TKey GetKey(TFrame frame)
    /// </summary>
    /// <returns></returns>
    /// <exception cref="KeyUndefinedException"></exception>
    internal static Func<TFrame, TKey> GenerateKeyGetter()
    {
        (FieldInfo Field, int Offset)? keyField = GetKeyField();
        Debug.Assert(keyField != null, "No key can be found");

        ParameterExpression frame = Expression.Parameter(typeof(TFrame), nameof(frame)); // (TFrame frame)
        MemberExpression field = Expression.Field(frame, keyField!.Value.Field); // frame.KeyField

        var lambda = Expression.Lambda<Func<TFrame, TKey>>(field, frame); // (TFrame frame) => frame.KeyField

        return lambda.Compile();
    }

    /// <summary>
    /// Generate a function that reads the key of the frame at the given stream position
    /// Generated function: TKey ReadKey(BinaryReader br, long framePos)
    /// </summary>
    /// <returns></returns>
    /// <exception cref="KeyUndefinedException"></exception>
    internal static Func<BinaryReader, long, TKey> GenerateKeyReader()
    {
        (FieldInfo Field, int Offset)? keyField = GetKeyField();
        Debug.Assert(keyField != null, "No key can be found");

        ParameterExpression br = Expression.Parameter(typeof(BinaryReader), nameof(br)); // (BinaryReader br
        ParameterExpression pos = Expression.Parameter(typeof(long), nameof(pos)); // , long pos)

        Expression posExpr = pos;
        if (keyField!.Value.Offset > 0)
        {
            ConstantExpression offsetConst = Expression.Constant(keyField.Value.Offset, typeof(int)); // offset
            UnaryExpression convertedOffset = Expression.Convert(offsetConst, typeof(long)); // (long)offset
            posExpr = Expression.Add(pos, convertedOffset); // pos + (long)offset
        }

        ConstantExpression seekOrigin = Expression.Constant(SeekOrigin.Begin, typeof(SeekOrigin)); // SeekOrigin.Begin

        PropertyInfo? baseStreamProp = typeof(BinaryReader).GetProperty(nameof(BinaryReader.BaseStream));
        Debug.Assert(baseStreamProp != null);
        MemberExpression baseStream = Expression.Property(br, baseStreamProp); // br.BaseStream

        MethodInfo? seekMethod = typeof(Stream).GetMethod(nameof(Stream.Seek));
        Debug.Assert(seekMethod != null);
        MethodCallExpression seekCall = Expression.Call(baseStream, seekMethod, posExpr, seekOrigin); // br.BaseStream.Seek(pos + offset, SeekOrigin.Begin);

        MethodInfo? readMethod = typeof(BinaryReader).GetMethod($"Read{keyField.Value.Field.FieldType.Name}");
        Debug.Assert(readMethod != null);
        MethodCallExpression readCall = Expression.Call(br, readMethod); // br.ReadTKey()

        BlockExpression blockExpr = Expression.Block(seekCall, readCall); // { br.BaseStream.Seek(...); return br.ReadTKey(); }

        var lambda = Expression.Lambda<Func<BinaryReader, long, TKey>>(blockExpr, br, pos); // (BinaryReader br, long pos) => { ... }

        return lambda.Compile();
    }

    /// <summary>
    /// Generate a function that reads a frame at the current stream position
    /// Generated function: TFrame ReadFrame(BinaryReader br)
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    internal static Func<BinaryReader, TFrame> GenerateFrameReader()
    {
        ParameterExpression br = Expression.Parameter(typeof(BinaryReader), nameof(br));

        List<Expression> expressions = new();
        ParameterExpression frame = Expression.Variable(typeof(TFrame), nameof(frame));
        BinaryExpression newFrameExp = Expression.Assign(frame, Expression.New(typeof(TFrame))); // var frame = new TFrame()
        expressions.Add(newFrameExp);

        IEnumerable<FieldInfo> fields = typeof(TFrame)
            .GetFields()
            .Where(o => o.GetCustomAttribute<IgnoreAttribute>(false) == null);

        foreach (FieldInfo fieldInfo in fields)
        {
            int length = fieldInfo.GetCustomAttributes(typeof(LengthAttribute), false)
                .Cast<LengthAttribute>()
                .FirstOrDefault()
                ?.Length ?? 0;

            Type fieldType = fieldInfo.FieldType;

            Expression valueParam;
            if (length > 0) // string
            {
                MethodInfo? readMethod = typeof(BinaryReader).GetMethod(nameof(BinaryReader.ReadChars), new[] { typeof(int) });
                Debug.Assert(readMethod != null);
                valueParam = Expression.Call(br, readMethod, Expression.Constant(length, typeof(int))); // br.ReadChars(length)

                ConstructorInfo? ctor = typeof(string).GetConstructor(new[] { typeof(char[]) });
                MethodInfo? trimEndMethod = typeof(string).GetMethod(nameof(string.TrimEnd), Array.Empty<Type>());
                Debug.Assert(ctor != null);
                Debug.Assert(trimEndMethod != null);
                valueParam = Expression.Call(Expression.New(ctor, valueParam), trimEndMethod); // new string(...).TrimEnd()
            }
            else
            {
                MethodInfo? readMethod = typeof(BinaryReader).GetMethod($"Read{fieldType.Name}");
                valueParam = Expression.Call(br, readMethod!); // br.Read{fieldType}()
            }

            BinaryExpression assignExp = Expression.Assign(Expression.Field(frame, fieldInfo), valueParam); // frame.{field} = ...
            expressions.Add(assignExp);
        }

        expressions.Add(frame); // return frame
        BlockExpression blockExpr = Expression.Block(new[] { frame }, expressions);

        var lambda = Expression.Lambda<Func<BinaryReader, TFrame>>(blockExpr, br);

        return lambda.Compile();
    }
}
