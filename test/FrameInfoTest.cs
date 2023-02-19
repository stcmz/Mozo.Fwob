using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozo.Fwob.Models;
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

        public ulong Key => ULong;
    }

    private void ValidateFrameInfo(AbstractFwobFile<TypedTick, ulong> file)
    {
        Assert.IsTrue(file.FrameInfo.FrameType == "TypedTick");
        Assert.IsTrue(file.FrameInfo.FrameLength == 59);
        Assert.IsTrue(file.FrameInfo.FieldTypes == 0x4430100212011ul);
        Assert.IsTrue(file.FrameInfo.Fields.Count == 13);

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
        Assert.IsTrue(file.FrameInfo.Fields[11].FieldType == FieldType.StringTableIndex);
    }

    [TestMethod]
    public void TestSupportedTypesInMemory()
    {
        var file = new InMemoryFwobFile<TypedTick, ulong>("TestTypes");
        ValidateFrameInfo(file);
    }

    [TestMethod]
    public void TestSupportedTypesInFile()
    {
        string temp = Path.GetTempFileName();
        using (var file = FwobFile<TypedTick, ulong>.CreateNewFile(temp, "TestTypes", FileMode.Open))
            ValidateFrameInfo(file);
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

        public ulong Key => ULong;
    }

    [TestMethod]
    public void TestUnsupportedTypes()
    {
        //Assert.ThrowsException<Exception>(() => new InMemoryFwobFile<UnsupportedTick, ulong>("MyFwob"));
    }
}
