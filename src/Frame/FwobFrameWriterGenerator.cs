using Mozo.Fwob.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Mozo.Fwob.Frame;

internal static class FwobFrameWriterGenerator<TFrame>
{
    /// <summary>
    /// Generate a function that writes the given frame to the current stream position
    /// Generated function: void WriteFrame(BinaryWriter bw, TFrame frame)
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    internal static Action<BinaryWriter, TFrame> GenerateFrameWriter()
    {
        ParameterExpression bw = Expression.Parameter(typeof(BinaryWriter), nameof(bw));
        ParameterExpression frame = Expression.Parameter(typeof(TFrame), nameof(frame));

        List<Expression> writeExpressions = new(), ifExpressions = new();

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
