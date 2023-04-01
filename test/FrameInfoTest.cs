#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozo.Fwob.Abstraction;
using Mozo.Fwob.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;

namespace Mozo.Fwob.UnitTest;

[TestClass]
public class FrameInfoTest
{
    public class KeyOnlyTick
    {
        public int Key;
    }

    public class TypedTick
    {
        public ulong ULong;
        public uint UInt;
        public int Int;
        public float Float;
        public byte b;
        public double Value;
        public sbyte sb;
        public short sh;
        public ushort ush;
        public long VeryLong;
        [Length(5)]
        public string? Str;
        [StringTableIndex]
        public int Idx;
        [StringTableIndex]
        public long Index;
        public decimal Dec;
    }

    public class TypedTickWithKey
    {
        [Key]
        public ulong ULong;
        public uint UInt;
        public int Int;
        public float Float;
        public byte b;
        public double Value;
        public sbyte sb;
        public short sh;
        public ushort ush;
        public long VeryLong;
        [Length(5)]
        public string? Str;
        [StringTableIndex]
        public int Idx;
        [StringTableIndex]
        public long Index;
        public decimal Dec;
    }

    public class TypedTickFloat
    {
        public ulong ULong;
        public uint UInt;
        public int Int;
        public float Float;
        public byte b;
        public double Value;
        public sbyte sb;
        public short sh;
        public ushort ush;
        public long VeryLong;
        [Length(5)]
        public string? Str;
        [StringTableIndex]
        public int Idx;
        [StringTableIndex]
        public long Index;
        public decimal Dec;
    }

    public class FloatWithKeyTick
    {
        public ulong ULong;
        public uint UInt;
        public int Int;
        [Key]
        public float Float;
        public byte b;
        public double Value;
        public sbyte sb;
        public short sh;
        public ushort ush;
        public long VeryLong;
        [Length(5)]
        public string? Str;
        [StringTableIndex]
        public int Idx;
        [StringTableIndex]
        public long Index;
        public decimal Dec;
    }

    private static void ValidateFrameInfo<TFrame, TKey>(AbstractFwobFile<TFrame, TKey> file, int keyIndex)
        where TFrame : class, new()
        where TKey : struct, IComparable<TKey>
    {
        Assert.AreEqual(75, file.FrameInfo.FrameLength);
        Assert.AreEqual(0x24430100212011ul, file.FrameInfo.FieldTypes);
        Assert.AreEqual(14, file.FrameInfo.Fields.Count);

        for (int i = 0; i < 14; i++)
        {
            Assert.AreEqual(file.FrameInfo.Fields[i].IsKey, (keyIndex == i));
        }
        Assert.AreEqual(keyIndex, file.FrameInfo.KeyFieldIndex);

        Assert.AreEqual(8, file.FrameInfo.Fields[0].FieldLength);
        Assert.AreEqual(4, file.FrameInfo.Fields[1].FieldLength);
        Assert.AreEqual(4, file.FrameInfo.Fields[2].FieldLength);
        Assert.AreEqual(4, file.FrameInfo.Fields[3].FieldLength);
        Assert.AreEqual(1, file.FrameInfo.Fields[4].FieldLength);
        Assert.AreEqual(8, file.FrameInfo.Fields[5].FieldLength);
        Assert.AreEqual(1, file.FrameInfo.Fields[6].FieldLength);
        Assert.AreEqual(2, file.FrameInfo.Fields[7].FieldLength);
        Assert.AreEqual(2, file.FrameInfo.Fields[8].FieldLength);
        Assert.AreEqual(8, file.FrameInfo.Fields[9].FieldLength);
        Assert.AreEqual(5, file.FrameInfo.Fields[10].FieldLength);
        Assert.AreEqual(4, file.FrameInfo.Fields[11].FieldLength);
        Assert.AreEqual(8, file.FrameInfo.Fields[12].FieldLength);
        Assert.AreEqual(16, file.FrameInfo.Fields[13].FieldLength);

        Assert.AreEqual("ULong", file.FrameInfo.Fields[0].FieldName);
        Assert.AreEqual("UInt", file.FrameInfo.Fields[1].FieldName);
        Assert.AreEqual("Int", file.FrameInfo.Fields[2].FieldName);
        Assert.AreEqual("Float", file.FrameInfo.Fields[3].FieldName);
        Assert.AreEqual("b", file.FrameInfo.Fields[4].FieldName);
        Assert.AreEqual("Value", file.FrameInfo.Fields[5].FieldName);
        Assert.AreEqual("sb", file.FrameInfo.Fields[6].FieldName);
        Assert.AreEqual("sh", file.FrameInfo.Fields[7].FieldName);
        Assert.AreEqual("ush", file.FrameInfo.Fields[8].FieldName);
        Assert.AreEqual("VeryLong", file.FrameInfo.Fields[9].FieldName);
        Assert.AreEqual("Str", file.FrameInfo.Fields[10].FieldName);
        Assert.AreEqual("Idx", file.FrameInfo.Fields[11].FieldName);
        Assert.AreEqual("Index", file.FrameInfo.Fields[12].FieldName);
        Assert.AreEqual("Dec", file.FrameInfo.Fields[13].FieldName);

        Assert.AreEqual(FieldType.UnsignedInteger, file.FrameInfo.Fields[0].FieldType);
        Assert.AreEqual(FieldType.UnsignedInteger, file.FrameInfo.Fields[1].FieldType);
        Assert.AreEqual(FieldType.SignedInteger, file.FrameInfo.Fields[2].FieldType);
        Assert.AreEqual(FieldType.FloatingPoint, file.FrameInfo.Fields[3].FieldType);
        Assert.AreEqual(FieldType.UnsignedInteger, file.FrameInfo.Fields[4].FieldType);
        Assert.AreEqual(FieldType.FloatingPoint, file.FrameInfo.Fields[5].FieldType);
        Assert.AreEqual(FieldType.SignedInteger, file.FrameInfo.Fields[6].FieldType);
        Assert.AreEqual(FieldType.SignedInteger, file.FrameInfo.Fields[7].FieldType);
        Assert.AreEqual(FieldType.UnsignedInteger, file.FrameInfo.Fields[8].FieldType);
        Assert.AreEqual(FieldType.SignedInteger, file.FrameInfo.Fields[9].FieldType);
        Assert.AreEqual(FieldType.Utf8String, file.FrameInfo.Fields[10].FieldType);
        Assert.AreEqual(FieldType.StringTableIndex, file.FrameInfo.Fields[11].FieldType);
        Assert.AreEqual(FieldType.StringTableIndex, file.FrameInfo.Fields[12].FieldType);
        Assert.AreEqual(FieldType.FloatingPoint, file.FrameInfo.Fields[13].FieldType);

        Assert.IsNull(file.GetKeyAt(0));
        var frame = new TFrame();
        file.AppendFrames(frame);
        Assert.AreEqual(0, file.GetKeyAt(0)?.CompareTo(AbstractFwobFile<TFrame, TKey>.GetKey(frame)));
    }

    private static void TestSupportedTypes<TFrame, TKey>(int keyIndex)
        where TFrame : class, new()
        where TKey : struct, IComparable<TKey>
    {
        InMemoryFwobFile<TFrame, TKey> memfile = new("TestTypes");
        Assert.AreEqual(typeof(TFrame).Name, memfile.FrameInfo.FrameType);
        Assert.ThrowsException<ArgumentNullException>(() => InMemoryFwobFile<TFrame, TKey>.ValidateFrame(null));
        ValidateFrameInfo(memfile, keyIndex);

        string temp = Path.GetTempFileName();
        using (FwobFile<TFrame, TKey> file = new(temp, "TestTypes"))
        {
            Assert.AreEqual(typeof(TFrame).Name, file.FrameInfo.FrameType);
            Assert.ThrowsException<ArgumentNullException>(() => FwobFile<TFrame, TKey>.ValidateFrame(null));
            ValidateFrameInfo(file, keyIndex);
        }
        File.Delete(temp);
    }

    private static void ValidateFrameInfoKeyOnly(AbstractFwobFile<KeyOnlyTick, int> file)
    {
        Assert.AreEqual(4, file.FrameInfo.FrameLength);
        Assert.AreEqual(0ul, file.FrameInfo.FieldTypes);
        Assert.AreEqual(1, file.FrameInfo.Fields.Count);

        Assert.IsTrue(file.FrameInfo.Fields[0].IsKey);
        Assert.AreEqual(0, file.FrameInfo.KeyFieldIndex);

        Assert.AreEqual(4, file.FrameInfo.Fields[0].FieldLength);

        Assert.AreEqual("Key", file.FrameInfo.Fields[0].FieldName);

        Assert.AreEqual(FieldType.SignedInteger, file.FrameInfo.Fields[0].FieldType);

        Assert.IsNull(file.GetKeyAt(0));
        var frame = new KeyOnlyTick();
        file.AppendFrames(frame);
        Assert.AreEqual(0, file.GetKeyAt(0)?.CompareTo(AbstractFwobFile<KeyOnlyTick, int>.GetKey(frame)));
    }

    [TestMethod]
    public void TestKeyOnlyTypes()
    {
        Assert.ThrowsException<ArgumentNullException>(() => AbstractFwobFile<KeyOnlyTick, int>.ValidateFrame(null));

        InMemoryFwobFile<KeyOnlyTick, int> memfile = new("TestTypes");
        Assert.AreEqual(typeof(KeyOnlyTick).Name, memfile.FrameInfo.FrameType);
        ValidateFrameInfoKeyOnly(memfile);

        string temp = Path.GetTempFileName();
        using (FwobFile<KeyOnlyTick, int> file = new(temp, "TestTypes"))
        {
            Assert.AreEqual(typeof(KeyOnlyTick).Name, file.FrameInfo.FrameType);
            ValidateFrameInfoKeyOnly(file);
        }
        File.Delete(temp);
    }

    [TestMethod]
    public void TestSupportedTypes()
    {
        TestSupportedTypes<TypedTick, ulong>(0);
    }

    [TestMethod]
    public void TestSupportedTypesWithKey()
    {
        TestSupportedTypes<TypedTickWithKey, ulong>(0);
    }

    [TestMethod]
    public void TestSupportedTypesFloat()
    {
        TestSupportedTypes<TypedTickFloat, float>(3);
    }

    [TestMethod]
    public void TestSupportedTypesFloatWithKey()
    {
        TestSupportedTypes<FloatWithKeyTick, float>(3);
    }

    public class IgnoredTick
    {
        public ulong ULong;
        [Ignore]
        public uint UInt;
        public int Int;
        [Ignore]
        public float Float;
        public byte b;
        [Ignore]
        public double Value;
        public sbyte sb;
        [Ignore]
        public short sh;
        public ushort ush;
        [Ignore]
        public long VeryLong;
        [Length(5)]
        public string? Str;
        [Ignore]
        [StringTableIndex]
        public int Idx;
        [StringTableIndex]
        public long Index;
        public decimal Dec;
    }

    public class IgnoredTickKey
    {
        public ulong ULong;
        [Ignore]
        public uint UInt;
        [Key]
        public int Int;
        [Ignore]
        public float Float;
        public byte b;
        [Ignore]
        public double Value;
        public sbyte sb;
        [Ignore]
        public short sh;
        public ushort ush;
        [Ignore]
        public long VeryLong;
        [Length(5)]
        public string? Str;
        [Ignore]
        [StringTableIndex]
        public int Idx;
        [StringTableIndex]
        public long Index;
        public decimal Dec;
    }

    private static void ValidateFrameInfoIgnored<TFrame, TKey>(AbstractFwobFile<TFrame, TKey> file, int keyIndex)
        where TFrame : class, new()
        where TKey : struct, IComparable<TKey>
    {
        Assert.AreEqual(45, file.FrameInfo.FrameLength);
        Assert.AreEqual(0x24310101ul, file.FrameInfo.FieldTypes);
        Assert.AreEqual(8, file.FrameInfo.Fields.Count);

        for (int i = 0; i < 8; i++)
        {
            Assert.AreEqual((keyIndex == i), file.FrameInfo.Fields[i].IsKey);
        }
        Assert.AreEqual(keyIndex, file.FrameInfo.KeyFieldIndex);

        Assert.AreEqual(8, file.FrameInfo.Fields[0].FieldLength);
        Assert.AreEqual(4, file.FrameInfo.Fields[1].FieldLength);
        Assert.AreEqual(1, file.FrameInfo.Fields[2].FieldLength);
        Assert.AreEqual(1, file.FrameInfo.Fields[3].FieldLength);
        Assert.AreEqual(2, file.FrameInfo.Fields[4].FieldLength);
        Assert.AreEqual(5, file.FrameInfo.Fields[5].FieldLength);
        Assert.AreEqual(8, file.FrameInfo.Fields[6].FieldLength);
        Assert.AreEqual(16, file.FrameInfo.Fields[7].FieldLength);

        Assert.AreEqual("ULong", file.FrameInfo.Fields[0].FieldName);
        Assert.AreEqual("Int", file.FrameInfo.Fields[1].FieldName);
        Assert.AreEqual("b", file.FrameInfo.Fields[2].FieldName);
        Assert.AreEqual("sb", file.FrameInfo.Fields[3].FieldName);
        Assert.AreEqual("ush", file.FrameInfo.Fields[4].FieldName);
        Assert.AreEqual("Str", file.FrameInfo.Fields[5].FieldName);
        Assert.AreEqual("Index", file.FrameInfo.Fields[6].FieldName);
        Assert.AreEqual("Dec", file.FrameInfo.Fields[7].FieldName);

        Assert.AreEqual(FieldType.UnsignedInteger, file.FrameInfo.Fields[0].FieldType);
        Assert.AreEqual(FieldType.SignedInteger, file.FrameInfo.Fields[1].FieldType);
        Assert.AreEqual(FieldType.UnsignedInteger, file.FrameInfo.Fields[2].FieldType);
        Assert.AreEqual(FieldType.SignedInteger, file.FrameInfo.Fields[3].FieldType);
        Assert.AreEqual(FieldType.UnsignedInteger, file.FrameInfo.Fields[4].FieldType);
        Assert.AreEqual(FieldType.Utf8String, file.FrameInfo.Fields[5].FieldType);
        Assert.AreEqual(FieldType.StringTableIndex, file.FrameInfo.Fields[6].FieldType);
        Assert.AreEqual(FieldType.FloatingPoint, file.FrameInfo.Fields[7].FieldType);

        Assert.IsNull(file.GetKeyAt(0));
        var frame = new TFrame();
        file.AppendFrames(frame);
        Assert.AreEqual(0, file.GetKeyAt(0)?.CompareTo(AbstractFwobFile<TFrame, TKey>.GetKey(frame)));
    }

    private static void TestSupportedTypesIgnored<TFrame, TKey>(int keyIndex)
        where TFrame : class, new()
        where TKey : struct, IComparable<TKey>
    {
        InMemoryFwobFile<TFrame, TKey> memfile = new("TestTypes");

        Assert.AreEqual(typeof(TFrame).Name, memfile.FrameInfo.FrameType);
        Assert.ThrowsException<ArgumentNullException>(() => InMemoryFwobFile<TFrame, TKey>.ValidateFrame(null));
        ValidateFrameInfoIgnored(memfile, keyIndex);

        string temp = Path.GetTempFileName();
        using (FwobFile<TFrame, TKey> file = new(temp, "TestTypes"))
        {
            Assert.AreEqual(typeof(TFrame).Name, file.FrameInfo.FrameType);
            Assert.ThrowsException<ArgumentNullException>(() => FwobFile<TFrame, TKey>.ValidateFrame(null));
            ValidateFrameInfoIgnored(file, keyIndex);
        }
        File.Delete(temp);
    }

    [TestMethod]
    public void TestSupportedTypesIgnored()
    {
        TestSupportedTypesIgnored<IgnoredTick, ushort>(4);
    }

    [TestMethod]
    public void TestSupportedTypesIgnoredWithKey()
    {
        TestSupportedTypesIgnored<IgnoredTickKey, int>(1);
    }

    public class IgnoredKeyTick
    {
        public int Int1;
        [Key]
        public int Int2;
        [Key]
        [Ignore]
        public int Int3;
    }

    private static void TestExceptionThrown<TException, TFrame, TKey>()
        where TException : Exception
        where TFrame : class, new()
        where TKey : struct, IComparable<TKey>
    {
        Assert.ThrowsException<TException>(() => new InMemoryFwobFile<TFrame, TKey>("TestTypes"));

        string temp = Path.GetTempFileName();
        Assert.ThrowsException<TException>(() =>
        {
            using FwobFile<TFrame, TKey> file = new(temp, "TestTypes");
        });
        File.Delete(temp);
    }

    [TestMethod]
    public void TestIgnoredKey()
    {
        TestExceptionThrown<KeyIgnoredException, IgnoredKeyTick, int>();
    }

    public class MultiKeysTick
    {
        public int Int1;
        [Key]
        public int Int2;
        [Key]
        public int Int3;
    }

    [TestMethod]
    public void TestMultiKeys()
    {
        TestExceptionThrown<AmbiguousKeyException, MultiKeysTick, int>();
    }

    public class KeyTypeMismatchT
    {
        public int Int1;
        [Key]
        public int Int2;
        public int Int3;
    }

    [TestMethod]
    public void TestKeyTypeMismatch()
    {
        TestExceptionThrown<KeyTypeMismatchException, KeyTypeMismatchT, uint>();
    }

    public class KeyUndefinedTick
    {
        public int Int1;
        public int Int2;
        public int Int3;
    }

    [TestMethod]
    public void TestKeyUndefined()
    {
        TestExceptionThrown<KeyUndefinedException, KeyUndefinedTick, uint>();
    }

    public class LongFieldNameT
    {
        public int ShortInt;
        public int VeryLongFieldName;
    }

    [TestMethod]
    public void TestLongFieldName()
    {
        TestExceptionThrown<FieldNameTooLongException, LongFieldNameT, uint>();
    }

    public class NoFieldsTick1
    {
        [Ignore]
        public int Int1;
        [Ignore]
        public int Int2;
        [Ignore]
        public int Int3;
    }

    public class NoFieldsTick2
    {
    }

    [TestMethod]
    public void TestNoFields()
    {
        TestExceptionThrown<NoFieldsException, NoFieldsTick1, uint>();
        TestExceptionThrown<NoFieldsException, NoFieldsTick2, uint>();
    }

    public class TooManyFieldsT
    {
        public int Int0;
        public int Int1;
        public int Int2;
        public int Int3;
        public int Int4;
        public int Int5;
        public int Int6;
        public int Int7;
        public int Int8;
        public int Int9;
        public int IntA;
        public int IntB;
        public int IntC;
        public int IntD;
        public int IntE;
        public int IntF;
        public int IntG;
    }

    [TestMethod]
    public void TestTooManyFields()
    {
        TestExceptionThrown<TooManyFieldsException, TooManyFieldsT, int>();
    }

    public class LenOutOfRangeT
    {
        public int Int0;
        [Length(256)]
        public string? Str;
    }

    public class LenOutOfRangeT2
    {
        public int Int0;
        [Length(0)]
        public string? Str;
    }

    [TestMethod]
    public void TestLengthOutOfRange()
    {
        TestExceptionThrown<FieldLengthOutOfRangeException, LenOutOfRangeT, int>();
        TestExceptionThrown<FieldLengthOutOfRangeException, LenOutOfRangeT2, int>();
    }

    public class LengthUndefinedT
    {
        public int Int0;
        public string? Str;
    }

    [TestMethod]
    public void TestLengthUndefined()
    {
        TestExceptionThrown<FieldLengthUndefinedException, LengthUndefinedT, int>();
    }

    public class LenNotAllowedT
    {
        [Length(3)]
        public int Int0;
        [Length(3)]
        public string? Str;
    }

    [TestMethod]
    public void TestLengthNotAllowed()
    {
        TestExceptionThrown<FieldLengthNotAllowedException, LenNotAllowedT, int>();
    }

    public class FrameTypeNameTooLongTick
    {
        public int Int0;
        [Length(3)]
        public string? Str;
    }

    [TestMethod]
    public void TestFrameTypeNameTooLong()
    {
        TestExceptionThrown<FrameTypeNameTooLongException, FrameTypeNameTooLongTick, int>();
    }

    public class CharFieldT
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public char Ch;
    }

    public class BoolFieldT
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public bool B;
    }

    public class NintFieldT
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public nint Nint;
    }

    public class NuintFieldT
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public nuint Nuint;
    }

    public class ArrayFieldT
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public byte[]? Bytes;
    }

    public class SubclassFieldT
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public class Subclass { }
        public Subclass? Sub;
    }

    public class ListFieldT
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public List<int>? List;
    }

    public class TupleFieldT
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public (int, int)? List;
    }

    public class NullableFieldT
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public int? Int1;
    }

    [TestMethod]
    public void TestFieldTypeNotSupported()
    {
        TestExceptionThrown<FieldTypeNotSupportedException, CharFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, BoolFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, NintFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, NuintFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, ArrayFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, SubclassFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, ListFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, TupleFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, NullableFieldT, int>();
    }

    public class Tick0a
    {
        public int Int0;
        [Length(4)]
        public string? Str;
        public long Int1;
    }

    public class Tick0b
    {
        public int Int0;
        [Length(4)]
        public string? Str;
        public int Int1;
    }

    public class Tick1
    {
        public int Int0;
        [Length(4)]
        public string? Str;
        public long Int1;
    }

    public class Tick2
    {
        public int Int0;
        [Length(4)]
        public string? Str;
        public long Int1;
    }

    public class Tick3
    {
        public int Int0;
        [Length(4)]
        public string? Str;
        public long Int1;
    }

    public class Tick4
    {
        public int Int0;
        [Length(4)]
        public string? Str;
        public long Int1;
    }

    public class Tick5
    {
        public int Int0;
        [Length(4)]
        public string? Str;
        public long Int1;
    }

    public class Tick6
    {
        public int Int0;
        [Length(4)]
        public string? Str;
        public long Int1;
    }

    public class Tick7
    {
        public int Int0;
        [Length(4)]
        public string? Str;
        public long Int1;
    }

    public class Tick8
    {
        public int Int0;
        [Length(4)]
        public string? Str;
        public long Int1;
    }

    public class Tick9
    {
        public int Int0;
        [Length(4)]
        public string? Str;
        public long Int1;
    }

    public class TickTypes
    {
        // Identical
        public class Tick0a
        {
            public int Int0;
            [Length(4)]
            public string? Str;
            public long Int1;
        }

        // Identical
        public class Tick0b
        {
            public int Int0;
            [Length(4)]
            public string? Str;
            [Key]
            public int Int1;
        }

        // Reordered fields equal length
        public class Tick1
        {
            public int Int0;
            public long Int1;
            [Length(4)]
            public string? Str;
        }

        // Different field type equal length
        public class Tick2
        {
            public int Int0;
            [Length(4)]
            public string? Str;
            public double Int1;
        }

        // Different key field equal length
        public class Tick3
        {
            public float Int0;
            [Length(4)]
            public string? Str;
            public long Int1;
        }

        // Different field name equal length
        public class Tick4
        {
            public int Int0;
            [Length(4)]
            public string? Str;
            public long Int2;
        }

        // Different field length equal total length
        public class Tick5
        {
            public int Int0;
            [Length(8)]
            public string? Str;
            public int Int1;
        }

        // Extra field equal length
        public class Tick6
        {
            public int Int0;
            [Length(4)]
            public string? Str;
            public int Int1;
            public int Int2;
        }

        // Extra field inequal length
        public class Tick7
        {
            public int Int0;
            [Length(4)]
            public string? Str;
            public long Int1;
            public int Int2;
        }

        // Missing field equal length
        public class Tick8
        {
            public int Int0;
            [Length(12)]
            public string? Str;
        }

        // Missing field inequal length
        public class Tick9
        {
            public int Int0;
            [Length(4)]
            public string? Str;
        }
    }

    private static void TestFrameTypeMismatchThrown<TFrame1, TFrame2, TKey1, TKey2, TException>()
        where TFrame1 : class, new()
        where TFrame2 : class, new()
        where TKey1 : struct, IComparable<TKey1>
        where TKey2 : struct, IComparable<TKey2>
        where TException : Exception
    {
        Assert.ThrowsException<ArgumentNullException>(() => AbstractFwobFile<TFrame1, TKey1>.ValidateFrame(null));
        Assert.ThrowsException<ArgumentNullException>(() => AbstractFwobFile<TFrame2, TKey2>.ValidateFrame(null));

        string temp = Path.GetTempFileName();
        Assert.ThrowsException<TException>(() =>
        {
            using (FwobFile<TFrame1, TKey1> file = new(temp, "TestTypes")) { }
            using (FwobFile<TFrame2, TKey2> file = new(temp)) { }
        });
        File.Delete(temp);
    }

    [TestMethod]
    public void TestFrameTypeMismatch()
    {
        // TODO: Should throw exception for redefined key, will resolve in the next version
        //TestFrameTypeMismatchThrown<Tick0a, TickTypes.Tick0a, int, long, FrameTypeMismatchException>();
        //TestFrameTypeMismatchThrown<Tick0b, TickTypes.Tick0b, int, int, FrameTypeMismatchException>();

        TestFrameTypeMismatchThrown<Tick1, Tick2, int, int, FrameTypeMismatchException>();
        TestFrameTypeMismatchThrown<Tick1, TickTypes.Tick1, int, int, FrameTypeMismatchException>();
        TestFrameTypeMismatchThrown<Tick2, TickTypes.Tick2, int, int, FrameTypeMismatchException>();
        TestFrameTypeMismatchThrown<Tick3, TickTypes.Tick3, int, float, FrameTypeMismatchException>();
        TestFrameTypeMismatchThrown<Tick4, TickTypes.Tick4, int, int, FrameTypeMismatchException>();
        TestFrameTypeMismatchThrown<Tick5, TickTypes.Tick5, int, int, FrameTypeMismatchException>();
        TestFrameTypeMismatchThrown<Tick6, TickTypes.Tick6, int, int, FrameTypeMismatchException>();
        TestFrameTypeMismatchThrown<Tick7, TickTypes.Tick7, int, int, FrameTypeMismatchException>();
        TestFrameTypeMismatchThrown<Tick8, TickTypes.Tick8, int, int, FrameTypeMismatchException>();
        TestFrameTypeMismatchThrown<Tick9, TickTypes.Tick9, int, int, FrameTypeMismatchException>();
    }
}

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
