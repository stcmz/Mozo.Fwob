﻿using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mozo.Fwob.Generators;

[Generator]
internal class ExceptionGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        string[] exceptionConfigs =
        {
            /* Schema: Key Related Exceptions in Parsing */
            "KeyUndefined|Type FrameType", // No [Key] annotation is found nor non-ignored TKey-typed field can be found
            "AmbiguousKey|Type FrameType|string FieldName|string OtherFieldName", // More than one [Key] annotations are found
            "KeyIgnored|string FieldName", // An annotated key is found to be also [Ignore] annotated
            "KeyTypeMismatch|Type FrameType|string FieldName|Type FieldType", // A field of a different type from TKey is annotated with [Key]

            /* Schema: Field Related Exceptions in Parsing */
            "NoFields|Type FrameType", // No non-ignored field can be found
            "TooManyFields|Type FrameType|int FieldCount", // Only 16 non-ignored fields can be defined in a frame
            "FieldNameTooLong|string FieldName|int NameLength", // A field name can only contain up to 8 bytes
            "FieldTypeNotSupported|string FieldName|Type FieldType", // A non-ignored field is found of an unsupported type

            /* Schema: Field Length Related Exceptions in Parsing */
            "FieldLengthUndefined|string FieldName", // No [Length] annotation is found for a field that requires it (i.e., string)
            "FieldLengthOutOfRange|string FieldName|int FieldLength", // A [Length] annotation must be 1 to 255 bytes
            "FieldLengthNotAllowed|string FieldName|Type FieldType", // A field of a type that does not support a [Length] annotation (i.e., non string)

            /* Schema: Frame Type Exceptions in Parsing */
            "FrameTypeNameTooLong|string FrameTypeName|int NameLength", // A frame type name can only contain up to 16 bytes

            /* Access: File Access Related Exceptions */
            "FileNotOpened", // The file object does not contain any opened file
            "FileNotReadable", // The file object was opened without FileAccess.Read
            "FileNotWritable", // The file object was opened without FileAccess.Write
            "CorruptedFileHeader|string FileName", // A unrecognized file header that does not in FWOB format
            "FrameTypeMismatch|string FileName|Type FrameType", // A frame type that does not precisely match the file schema
            "CorruptedFileLength|string FileName|long FileLength|long ActualLength", // The file length stored in header differs from actual length in stream
            "CorruptedStringTableLength|string FileName|long StringTableLength|long ActualLength", // The string table length stored in header differs from actual length in stream
            "StringTableOutOfSpace|string FileName|long RequiredLength|long PreservedLength", // The string table has no enough space for adding new strings
            "TitleTooLong|string Title|int TitleLength", // A frame type name can only contain up to 16 bytes
            "TitleIncompatible|string Title|string OtherTitle", // Files being concatenated must have the same title
            "KeyOrderViolation|string FileName", // Frames being added violate the key ordering rule
            "StringTooLong|string FieldName|string StringLiteral|int StringLength", // A string that is too long to fit into the field
            "FrameNotFound|string FileName", // No frame can be found in a file
            "StringTableIncompatible|string FileName|int Index|string StringLiteral|string OtherStringLiteral", // Files being concatenated must have equal string table
        };

        foreach (string exceptionConfig in exceptionConfigs)
        {
            string[] fields = exceptionConfig.Split('|');
            string exceptionName = fields[0];

            // Parse the additional property
            List<(string Type, string MemberName, string ParamName)> props = new();

            foreach (string prop in fields.Skip(1))
            {
                string[] fs = prop.Split(' ');
                props.Add((fs[0], fs[1], $"{char.ToLower(fs[1][0])}{fs[1].Substring(1)}"));
            }

            // Build up code snippets
            string paramList = string.Join(", ", props.Select(o => $"{o.Type} {o.ParamName}"));
            string assignmentList = string.Join(@"
        ", props.Select(o => $"{o.MemberName} = {o.ParamName};"));
            string memberList = string.Join(@"

    ", props.Select(o => $"public {o.Type}? {o.MemberName} {{ get; }}"));

            // Build up the source code
            StringBuilder sb = new();

            sb.Append($@"// <auto-generated/>
#nullable enable

using System;
using System.Runtime.Serialization;

namespace Mozo.Fwob.Exceptions;

[Serializable]
public class {exceptionName}Exception : Exception
{{");

            // Parameterless constructor
            sb.Append($@"
    public {exceptionName}Exception() {{ }}
");

            if (props.Count == 0 || props.Count != 1 || props[0].Type != "string")
            {
                // Constructor with default message
                sb.Append($@"
    public {exceptionName}Exception(string message) : base(message) {{ }}
");
            }

            // Constructor with default message and inner exception
            sb.Append($@"
    public {exceptionName}Exception(string message, Exception inner) : base(message, inner) {{ }}
");

            if (props.Count > 0)
            {
                // Constructor with additional property
                sb.Append($@"
    public {exceptionName}Exception({paramList})
    {{
        {assignmentList}
    }}
");

                // Constructor with additional property and default message
                sb.Append($@"
    public {exceptionName}Exception({paramList}, string message) : base(message)
    {{
        {assignmentList}
    }}
");
            }

            sb.Append($@"
    protected {exceptionName}Exception(
        SerializationInfo info,
        StreamingContext context) : base(info, context) {{ }}");

            if (props.Count > 0)
            {
                // Constructor with additional property and default message
                sb.Append($@"

    {memberList}");
            }

            // Constructor with additional property and default message
            sb.Append($@"
}}
");

            // Add the source code to the compilation
            context.AddSource($"{exceptionName}Exception.g.cs", sb.ToString());
        }
    }

    public void Initialize(GeneratorInitializationContext context)
    {
    }
}
