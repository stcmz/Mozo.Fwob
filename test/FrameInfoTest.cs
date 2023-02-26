using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozo.Fwob.Exceptions;
using Mozo.Fwob.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Mozo.Fwob.UnitTest;

[TestClass]
public class FrameInfoTest
{
    public class TypedTick : IFrame<ulong>
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
    }

    public class TypedTickWithKey : IFrame<ulong>
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
    }

    public class TypedTickFloat : IFrame<float>
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
    }

    public class FloatWithKeyTick : IFrame<float>
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
    }

    private void ValidateFrameInfo<TFrame, TKey>(AbstractFwobFile<TFrame, TKey> file, int keyIndex)
        where TFrame : class, IFrame<TKey>, new()
        where TKey : struct, IComparable<TKey>
    {
        Assert.IsTrue(file.FrameInfo.FrameLength == 59);
        Assert.IsTrue(file.FrameInfo.FieldTypes == 0x4430100212011ul);
        Assert.IsTrue(file.FrameInfo.Fields.Count == 13);

        for (int i = 0; i < 13; i++)
        {
            Assert.AreEqual(file.FrameInfo.Fields[i].IsKey, (keyIndex == i));
        }
        Assert.AreEqual(file.FrameInfo.KeyIndex, keyIndex);

        Assert.IsTrue(file.FrameInfo.Fields[0].FieldLength == 8);
        Assert.IsTrue(file.FrameInfo.Fields[1].FieldLength == 4);
        Assert.IsTrue(file.FrameInfo.Fields[2].FieldLength == 4);
        Assert.IsTrue(file.FrameInfo.Fields[3].FieldLength == 4);
        Assert.IsTrue(file.FrameInfo.Fields[4].FieldLength == 1);
        Assert.IsTrue(file.FrameInfo.Fields[5].FieldLength == 8);
        Assert.IsTrue(file.FrameInfo.Fields[6].FieldLength == 1);
        Assert.IsTrue(file.FrameInfo.Fields[7].FieldLength == 2);
        Assert.IsTrue(file.FrameInfo.Fields[8].FieldLength == 2);
        Assert.IsTrue(file.FrameInfo.Fields[9].FieldLength == 8);
        Assert.IsTrue(file.FrameInfo.Fields[10].FieldLength == 5);
        Assert.IsTrue(file.FrameInfo.Fields[11].FieldLength == 4);
        Assert.IsTrue(file.FrameInfo.Fields[12].FieldLength == 8);

        Assert.IsTrue(file.FrameInfo.Fields[0].FieldName == "ULong");
        Assert.IsTrue(file.FrameInfo.Fields[1].FieldName == "UInt");
        Assert.IsTrue(file.FrameInfo.Fields[2].FieldName == "Int");
        Assert.IsTrue(file.FrameInfo.Fields[3].FieldName == "Float");
        Assert.IsTrue(file.FrameInfo.Fields[4].FieldName == "b");
        Assert.IsTrue(file.FrameInfo.Fields[5].FieldName == "Value");
        Assert.IsTrue(file.FrameInfo.Fields[6].FieldName == "sb");
        Assert.IsTrue(file.FrameInfo.Fields[7].FieldName == "sh");
        Assert.IsTrue(file.FrameInfo.Fields[8].FieldName == "ush");
        Assert.IsTrue(file.FrameInfo.Fields[9].FieldName == "VeryLong");
        Assert.IsTrue(file.FrameInfo.Fields[10].FieldName == "Str");
        Assert.IsTrue(file.FrameInfo.Fields[11].FieldName == "Idx");
        Assert.IsTrue(file.FrameInfo.Fields[12].FieldName == "Index");

        Assert.IsTrue(file.FrameInfo.Fields[0].FieldType == FieldType.UnsignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[1].FieldType == FieldType.UnsignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[2].FieldType == FieldType.SignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[3].FieldType == FieldType.FloatingPoint);
        Assert.IsTrue(file.FrameInfo.Fields[4].FieldType == FieldType.UnsignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[5].FieldType == FieldType.FloatingPoint);
        Assert.IsTrue(file.FrameInfo.Fields[6].FieldType == FieldType.SignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[7].FieldType == FieldType.SignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[8].FieldType == FieldType.UnsignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[9].FieldType == FieldType.SignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[10].FieldType == FieldType.Utf8String);
        Assert.IsTrue(file.FrameInfo.Fields[11].FieldType == FieldType.StringTableIndex);
        Assert.IsTrue(file.FrameInfo.Fields[12].FieldType == FieldType.StringTableIndex);
    }

    [TestMethod]
    public void TestSupportedTypesInMemory()
    {
        var file = new InMemoryFwobFile<TypedTick, ulong>("TestTypes");
        Assert.IsTrue(file.FrameInfo.FrameType == "TypedTick");
        ValidateFrameInfo(file, 0);
    }

    [TestMethod]
    public void TestSupportedTypesInFile()
    {
        string temp = Path.GetTempFileName();
        using (var file = FwobFile<TypedTick, ulong>.CreateNew(temp, "TestTypes", FileMode.Open))
        {
            Assert.IsTrue(file.FrameInfo.FrameType == "TypedTick");
            ValidateFrameInfo(file, 0);
        }
        File.Delete(temp);
    }

    [TestMethod]
    public void TestSupportedTypesWithKeyInMemory()
    {
        var file = new InMemoryFwobFile<TypedTickWithKey, ulong>("TestTypes");
        Assert.IsTrue(file.FrameInfo.FrameType == "TypedTickWithKey");
        ValidateFrameInfo(file, 0);
    }

    [TestMethod]
    public void TestSupportedTypesWithKeyInFile()
    {
        string temp = Path.GetTempFileName();
        using (var file = FwobFile<TypedTickWithKey, ulong>.CreateNew(temp, "TestTypes", FileMode.Open))
        {
            Assert.IsTrue(file.FrameInfo.FrameType == "TypedTickWithKey");
            ValidateFrameInfo(file, 0);
        }
        File.Delete(temp);
    }

    [TestMethod]
    public void TestSupportedTypesFloatInMemory()
    {
        var file = new InMemoryFwobFile<TypedTickFloat, float>("TestTypes");
        Assert.IsTrue(file.FrameInfo.FrameType == "TypedTickFloat");
        ValidateFrameInfo(file, 3);
    }

    [TestMethod]
    public void TestSupportedTypesFloatInFile()
    {
        string temp = Path.GetTempFileName();
        using (var file = FwobFile<TypedTickFloat, float>.CreateNew(temp, "TestTypes", FileMode.Open))
        {
            Assert.IsTrue(file.FrameInfo.FrameType == "TypedTickFloat");
            ValidateFrameInfo(file, 3);
        }
        File.Delete(temp);
    }

    [TestMethod]
    public void TestSupportedTypesFloatWithKeyInMemory()
    {
        var file = new InMemoryFwobFile<FloatWithKeyTick, float>("TestTypes");
        Assert.IsTrue(file.FrameInfo.FrameType == "FloatWithKeyTick");
        ValidateFrameInfo(file, 3);
    }

    [TestMethod]
    public void TestSupportedTypesFloatWithKeyInFile()
    {
        string temp = Path.GetTempFileName();
        using (var file = FwobFile<FloatWithKeyTick, float>.CreateNew(temp, "TestTypes", FileMode.Open))
        {
            Assert.IsTrue(file.FrameInfo.FrameType == "FloatWithKeyTick");
            ValidateFrameInfo(file, 3);
        }
        File.Delete(temp);
    }

    public class IgnoredTick : IFrame<ushort>
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
    }

    public class IgnoredTickKey : IFrame<int>
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
    }

    private void ValidateFrameInfoIgnored<TFrame, TKey>(AbstractFwobFile<TFrame, TKey> file, int keyIndex)
        where TFrame : class, IFrame<TKey>, new()
        where TKey : struct, IComparable<TKey>
    {
        Assert.IsTrue(file.FrameInfo.FrameLength == 29);
        Assert.IsTrue(file.FrameInfo.FieldTypes == 0x4310101ul);
        Assert.IsTrue(file.FrameInfo.Fields.Count == 7);

        for (int i = 0; i < 7; i++)
        {
            Assert.AreEqual(file.FrameInfo.Fields[i].IsKey, (keyIndex == i));
        }
        Assert.AreEqual(file.FrameInfo.KeyIndex, keyIndex);

        Assert.IsTrue(file.FrameInfo.Fields[0].FieldLength == 8);
        Assert.IsTrue(file.FrameInfo.Fields[1].FieldLength == 4);
        Assert.IsTrue(file.FrameInfo.Fields[2].FieldLength == 1);
        Assert.IsTrue(file.FrameInfo.Fields[3].FieldLength == 1);
        Assert.IsTrue(file.FrameInfo.Fields[4].FieldLength == 2);
        Assert.IsTrue(file.FrameInfo.Fields[5].FieldLength == 5);
        Assert.IsTrue(file.FrameInfo.Fields[6].FieldLength == 8);

        Assert.IsTrue(file.FrameInfo.Fields[0].FieldName == "ULong");
        Assert.IsTrue(file.FrameInfo.Fields[1].FieldName == "Int");
        Assert.IsTrue(file.FrameInfo.Fields[2].FieldName == "b");
        Assert.IsTrue(file.FrameInfo.Fields[3].FieldName == "sb");
        Assert.IsTrue(file.FrameInfo.Fields[4].FieldName == "ush");
        Assert.IsTrue(file.FrameInfo.Fields[5].FieldName == "Str");
        Assert.IsTrue(file.FrameInfo.Fields[6].FieldName == "Index");

        Assert.IsTrue(file.FrameInfo.Fields[0].FieldType == FieldType.UnsignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[1].FieldType == FieldType.SignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[2].FieldType == FieldType.UnsignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[3].FieldType == FieldType.SignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[4].FieldType == FieldType.UnsignedInteger);
        Assert.IsTrue(file.FrameInfo.Fields[5].FieldType == FieldType.Utf8String);
        Assert.IsTrue(file.FrameInfo.Fields[6].FieldType == FieldType.StringTableIndex);
    }

    [TestMethod]
    public void TestSupportedTypesIgnoredInMemory()
    {
        var file = new InMemoryFwobFile<IgnoredTick, ushort>("TestTypes");
        Assert.IsTrue(file.FrameInfo.FrameType == "IgnoredTick");
        ValidateFrameInfoIgnored(file, 4);
    }

    [TestMethod]
    public void TestSupportedTypesIgnoredInFile()
    {
        string temp = Path.GetTempFileName();
        using (var file = FwobFile<IgnoredTick, ushort>.CreateNew(temp, "TestTypes", FileMode.Open))
        {
            Assert.IsTrue(file.FrameInfo.FrameType == "IgnoredTick");
            ValidateFrameInfoIgnored(file, 4);
        }
        File.Delete(temp);
    }

    [TestMethod]
    public void TestSupportedTypesIgnoredWithKeyInMemory()
    {
        var file = new InMemoryFwobFile<IgnoredTickKey, int>("TestTypes");
        Assert.IsTrue(file.FrameInfo.FrameType == "IgnoredTickKey");
        ValidateFrameInfoIgnored(file, 1);
    }

    [TestMethod]
    public void TestSupportedTypesIgnoredWithKeyInFile()
    {
        string temp = Path.GetTempFileName();
        using (var file = FwobFile<IgnoredTickKey, int>.CreateNew(temp, "TestTypes", FileMode.Open))
        {
            Assert.IsTrue(file.FrameInfo.FrameType == "IgnoredTickKey");
            ValidateFrameInfoIgnored(file, 1);
        }
        File.Delete(temp);
    }

    public class IgnoredKeyTick : IFrame<int>
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
        where TFrame : class, IFrame<TKey>, new()
        where TKey : struct, IComparable<TKey>
    {
        Assert.ThrowsException<TException>(() => new InMemoryFwobFile<TFrame, TKey>("TestTypes"));

        string temp = Path.GetTempFileName();
        Assert.ThrowsException<TException>(() =>
        {
            using (var file = FwobFile<TFrame, TKey>.CreateNew(temp, "TestTypes", FileMode.Open)) { }
        });
        File.Delete(temp);
    }

    [TestMethod]
    public void TestIgnoredKey()
    {
        TestExceptionThrown<KeyIgnoredException, IgnoredKeyTick, int>();
    }

    public class MultiKeysTick : IFrame<int>
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

    public class KeyTypeMismatchT : IFrame<uint>
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

    public class KeyUndefinedTick : IFrame<uint>
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

    public class LongFieldNameT : IFrame<uint>
    {
        public int ShortInt;
        public int VeryLongFieldName;
    }

    [TestMethod]
    public void TestLongFieldName()
    {
        TestExceptionThrown<FieldNameTooLongException, LongFieldNameT, uint>();
    }

    public class NoFieldsTick : IFrame<uint>
    {
        [Ignore]
        public int Int1;
        [Ignore]
        public int Int2;
        [Ignore]
        public int Int3;
    }

    [TestMethod]
    public void TestNoFields()
    {
        TestExceptionThrown<NoFieldsException, NoFieldsTick, uint>();
    }

    public class TooManyFieldsT : IFrame<int>
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

    public class LenOutOfRangeT : IFrame<int>
    {
        public int Int0;
        [Length(256)]
        public string? Str;
    }

    public class LenOutOfRangeT2 : IFrame<int>
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

    public class LengthUndefinedT : IFrame<int>
    {
        public int Int0;
        public string? Str;
    }

    [TestMethod]
    public void TestLengthUndefined()
    {
        TestExceptionThrown<FieldLengthUndefinedException, LengthUndefinedT, int>();
    }

    public class LenNotAllowedT : IFrame<int>
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

    public class FrameTypeNameTooLongTick : IFrame<int>
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

    public class ArrayFieldT : IFrame<int>
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public byte[]? Bytes;
    }

    public class SubclassFieldT : IFrame<int>
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public class Subclass { }
        public Subclass? Sub;
    }

    public class ListFieldT : IFrame<int>
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public List<int>? List;
    }

    public class TupleFieldT : IFrame<int>
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public (int, int)? List;
    }

    public class NullableFieldT : IFrame<int>
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public int? Int1;
    }

    [TestMethod]
    public void TestFieldTypeNotSupported()
    {
        TestExceptionThrown<FieldTypeNotSupportedException, ArrayFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, SubclassFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, ListFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, TupleFieldT, int>();
        TestExceptionThrown<FieldTypeNotSupportedException, NullableFieldT, int>();
    }

    public class Tick1 : IFrame<int>
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public int Int1;
    }

    public class Tick2 : IFrame<int>
    {
        public int Int0;
        [Length(3)]
        public string? Str;
        public int Int1;
    }

    [TestMethod]
    public void TestFrameTypeMismatch()
    {
        string temp = Path.GetTempFileName();
        Assert.ThrowsException<FrameTypeMismatchException>(() =>
        {
            using (var file = FwobFile<Tick1, int>.CreateNew(temp, "TestTypes", FileMode.Open)) { }
            using (var file = new FwobFile<Tick2, int>(temp)) { }
        });
        File.Delete(temp);
    }

    public class UnsupportedTick : IFrame<ulong>
    {
        public ulong ULong;
        [Length(3)] // invalid
        public uint UInt;
        [StringTableIndex] // not supported
        public double Value;
        public string? Str; // Length missing
        public byte[]? Bytes; // not supported
    }

    [TestMethod]
    public void TestUnsupportedTypes()
    {
        //Assert.ThrowsException<Exception>(() => new InMemoryFwobFile<UnsupportedTick, ulong>("MyFwob"));
    }
}
