using Mozo.Fwob.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SystemFieldInfo = System.Reflection.FieldInfo;

namespace Mozo.Fwob;

// Dynamically generate functions using expression trees for reading/writing a frame/key from/to an on-disk FWOB file.
public partial class FwobFile<TFrame, TKey>
{
    /// <summary>
    /// Writes a frame using the passed-in <see cref="BinaryWriter"/>.
    /// WriteFrame method does not set the position of the underlying stream before writing a frame.
    /// </summary>
    public static readonly Action<BinaryWriter, TFrame> WriteFrame = GenerateFrameWriter();

    /// <summary>
    /// Reads a frame and returns it using the passed-in <see cref="BinaryReader"/>.
    /// ReadFrame method does not set the position of the underlying stream before reading a frame.
    /// </summary>
    public static readonly Func<BinaryReader, TFrame> ReadFrame = GenerateFrameReader();

    /// <summary>
    /// Reads the key of the frame at the passed-in position using the passed-in <see cref="BinaryReader"/>.
    /// ReadKey method sets the position of the underlying stream before reading a key.
    /// </summary>
    public static readonly Func<BinaryReader, long, TKey> ReadKey = GenerateKeyReader();

    /// <summary>
    /// Dynamically generate a function that reads the key of the frame at the given stream position.
    /// function: TKey ReadKey(BinaryReader br, long framePos)
    /// </summary>
    /// <returns></returns>
    private static Func<BinaryReader, long, TKey> GenerateKeyReader()
    {
        ParameterExpression br = Expression.Parameter(typeof(BinaryReader), nameof(br)); // (BinaryReader br
        ParameterExpression pos = Expression.Parameter(typeof(long), nameof(pos)); // , long pos)

        Expression posExpr = pos;

        int keyOffset = _FrameInfo.KeyFieldOffset;
        if (keyOffset > 0)
        {
            ConstantExpression offsetConst = Expression.Constant(keyOffset, typeof(int)); // offset
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

        MethodInfo? readMethod = typeof(BinaryReader).GetMethod($"Read{typeof(TKey).Name}");
        Debug.Assert(readMethod != null);
        MethodCallExpression readCall = Expression.Call(br, readMethod); // br.ReadTKey()

        BlockExpression blockExpr = Expression.Block(seekCall, readCall); // { br.BaseStream.Seek(...); return br.ReadTKey(); }

        var lambda = Expression.Lambda<Func<BinaryReader, long, TKey>>(blockExpr, br, pos); // (BinaryReader br, long pos) => { ... }

        return lambda.Compile();
    }

    /// <summary>
    /// Dynamically generate a  a function that reads a frame at the current stream position.
    /// function: TFrame ReadFrame(BinaryReader br)
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private static Func<BinaryReader, TFrame> GenerateFrameReader()
    {
        ParameterExpression br = Expression.Parameter(typeof(BinaryReader), nameof(br));

        List<Expression> expressions = new();
        ParameterExpression frame = Expression.Variable(typeof(TFrame), nameof(frame));
        BinaryExpression newFrameExp = Expression.Assign(frame, Expression.New(typeof(TFrame))); // var frame = new TFrame()
        expressions.Add(newFrameExp);

        IEnumerable<SystemFieldInfo> fields = typeof(TFrame)
            .GetFields()
            .Where(o => o.GetCustomAttribute<IgnoreAttribute>(false) == null);

        foreach (SystemFieldInfo fieldInfo in fields)
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

    /// <summary>
    /// Dynamically generate a function that writes the given frame to the current stream position.
    /// function: void WriteFrame(BinaryWriter bw, TFrame frame)
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private static Action<BinaryWriter, TFrame> GenerateFrameWriter()
    {
        ParameterExpression bw = Expression.Parameter(typeof(BinaryWriter), nameof(bw));
        ParameterExpression frame = Expression.Parameter(typeof(TFrame), nameof(frame));

        List<Expression> writeExpressions = new(), ifExpressions = new();

        IEnumerable<SystemFieldInfo> fields = typeof(TFrame)
            .GetFields()
            .Where(o => o.GetCustomAttribute<IgnoreAttribute>(false) == null);

        foreach (SystemFieldInfo fieldInfo in fields)
        {
            int length = fieldInfo.GetCustomAttributes(typeof(LengthAttribute), false)
                .Cast<LengthAttribute>()
                .FirstOrDefault()
                ?.Length ?? 0;

            Type fieldType = fieldInfo.FieldType;

            Expression valueParam;
            MemberExpression fieldExp = Expression.Field(frame, fieldInfo); // frame.{field}

            if (length > 0) // Only string field can have the Length attribute
            {
                // [Expression] if (frame.{field} != null && frame.{field} > length) throw new StringTooLongException("...");
                PropertyInfo? lengthPropType = typeof(string).GetProperty(nameof(string.Length));

                Debug.Assert(lengthPropType != null);
                MemberExpression lengthProp = Expression.Property(fieldExp, lengthPropType); // frame.{field}.Length
                BinaryExpression notNull = Expression.NotEqual(fieldExp, Expression.Constant(null)); // frame.{field} != null
                BinaryExpression greaterThan = Expression.GreaterThan(lengthProp, Expression.Constant(length)); // ... > length
                ConstantExpression exceptionFieldName = Expression.Constant(fieldInfo.Name); // {field.Name}
                ConstructorInfo? exceptionCtor = typeof(StringTooLongException).GetConstructor(new[] { typeof(string), typeof(string), typeof(int) });

                Debug.Assert(exceptionCtor != null);
                NewExpression exceptionExp = Expression.New(exceptionCtor, exceptionFieldName, fieldExp, lengthProp); // new StringTooLongException(...)
                ConditionalExpression ifExp = Expression.IfThen(Expression.AndAlso(notNull, greaterThan), Expression.Throw(exceptionExp)); // if (...) throw ...
                ifExpressions.Add(ifExp);

                // [Expression] (frame.{field} ?? string.Empty).PadRight(length).ToCharArray()
                MethodInfo? padRightMethod = typeof(string).GetMethod(nameof(string.PadRight), new[] { typeof(int) });

                Debug.Assert(padRightMethod != null);
                BinaryExpression coalescing = Expression.Coalesce(fieldExp, Expression.Constant(string.Empty)); // frame.{field} ?? string.Empty
                valueParam = Expression.Call(coalescing, padRightMethod, Expression.Constant(length)); // {...}.PadRight(length)

                MethodInfo? toCharArrayMethod = typeof(string).GetMethod(nameof(string.ToCharArray), Array.Empty<Type>());

                Debug.Assert(toCharArrayMethod != null);
                valueParam = Expression.Call(valueParam, toCharArrayMethod); // {valueParam}.ToCharArray()

                fieldType = typeof(char[]);
            }
            else
            {
                // [Expression] frame.{field}
                valueParam = fieldExp;
            }

            MethodInfo writeMethod = typeof(BinaryWriter).GetMethod(nameof(BinaryWriter.Write), new[] { fieldType })
                ?? throw new FieldTypeNotSupportedException(fieldInfo.Name, fieldType);

            // [Expression] bw.Write({valueParam});
            MethodCallExpression assignExp = Expression.Call(bw, writeMethod, valueParam);
            writeExpressions.Add(assignExp);
        }

        BlockExpression blockExpr = Expression.Block(ifExpressions.Concat(writeExpressions));

        var lambda = Expression.Lambda<Action<BinaryWriter, TFrame>>(blockExpr, bw, frame);

        return lambda.Compile();
    }
}
