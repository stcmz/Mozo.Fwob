using System;
using System.Collections.Generic;
using System.IO;
using Fwob;
using Fwob.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FwobUnitTest
{
    [TestClass]
    public class FwobFileTest
    {
        class Tick : ISerializableFrame<int>
        {
            public int Time;
            public double Value;

            public int Key => Time;

            public void DeserializeFrame(BinaryReader br)
            {
                Time = br.ReadInt32();
                Value = br.ReadDouble();
            }

            public int DeserializeKey(BinaryReader br)
            {
                return br.ReadInt32();
            }

            public void SerializeFrame(BinaryWriter bw)
            {
                bw.Write(Time);
                bw.Write(Value);
            }

            public void SerializeKey(BinaryWriter bw)
            {
                bw.Write(Time);
            }
        }

        [TestMethod]
        public void TestWritingNewFile()
        {
            var temp = Path.GetTempFileName();
            using (var file = FwobFile<Tick, int>.CreateNewFile(temp, "HelloFwob", FileMode.Create))
            {
                Assert.IsNull(file.FirstFrame);
                Assert.IsNull(file.LastFrame);
                Assert.IsTrue(file.StringCount == 0);
                Assert.IsTrue(file.FrameCount == 0);
                Assert.IsTrue(file.FrameInfo.FrameType == "Tick");
                Assert.IsTrue(file.FrameInfo.FrameLength == 12);
                Assert.IsTrue(file.FrameInfo.FieldTypes == 0x20);
                Assert.IsTrue(file.FrameInfo.Fields.Count == 2);
                Assert.IsTrue(file.FrameInfo.Fields[0].FieldLength == 4);
                Assert.IsTrue(file.FrameInfo.Fields[1].FieldLength == 8);
                Assert.IsTrue(file.FrameInfo.Fields[0].FieldName == "Time");
                Assert.IsTrue(file.FrameInfo.Fields[1].FieldName == "Value");
                Assert.IsTrue(file.FrameInfo.Fields[0].FieldType == FieldType.SignedInteger);
                Assert.IsTrue(file.FrameInfo.Fields[1].FieldType == FieldType.FloatingPoint);
                Assert.IsTrue(file.Strings.Count == 0);
                Assert.IsTrue(file.Title == "HelloFwob");
            }

            File.Delete(temp);
        }

        [TestMethod]
        public void TestReadingNewFile()
        {
            var temp = Path.GetTempFileName();
            using (var file = FwobFile<Tick, int>.CreateNewFile(temp, "HelloFwob", FileMode.Create)) ;

            using (var file = new FwobFile<Tick, int>(temp))
            {
                Assert.IsNull(file.FirstFrame);
                Assert.IsNull(file.LastFrame);
                Assert.IsTrue(file.StringCount == 0);
                Assert.IsTrue(file.FrameCount == 0);
                Assert.IsTrue(file.FrameInfo.FrameType == "Tick");
                Assert.IsTrue(file.FrameInfo.FrameLength == 12);
                Assert.IsTrue(file.FrameInfo.FieldTypes == 0x20);
                Assert.IsTrue(file.FrameInfo.Fields.Count == 2);
                Assert.IsTrue(file.FrameInfo.Fields[0].FieldLength == 4);
                Assert.IsTrue(file.FrameInfo.Fields[1].FieldLength == 8);
                Assert.IsTrue(file.FrameInfo.Fields[0].FieldName == "Time");
                Assert.IsTrue(file.FrameInfo.Fields[1].FieldName == "Value");
                Assert.IsTrue(file.FrameInfo.Fields[0].FieldType == FieldType.SignedInteger);
                Assert.IsTrue(file.FrameInfo.Fields[1].FieldType == FieldType.FloatingPoint);
                Assert.IsTrue(file.Strings.Count == 0);
                Assert.IsTrue(file.Title == "HelloFwob");
            }

            File.Delete(temp);
        }

        [TestMethod]
        public void TestStringTableNoRandomAccess()
        {
            var temp = Path.GetTempFileName();
            using (var file = FwobFile<Tick, int>.CreateNewFile(temp, "HelloFwob", FileMode.Create))
            {
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
                Assert.IsTrue(file.GetIndex("mystr") == -1);
                Assert.IsTrue(file.StringCount == 0);
                Assert.IsTrue(file.AppendString("mystr") == 0);
                Assert.IsTrue(file.StringCount == 1);
                Assert.IsTrue(file.AppendString("hello2") == 1);
                Assert.IsTrue(file.StringCount == 2);
                Assert.IsTrue(file.AppendString("test_string3") == 2);
                Assert.IsTrue(file.StringCount == 3);

                Assert.IsTrue(file.ContainsString("mystr"));
                Assert.IsFalse(file.ContainsString("mystr2"));
                Assert.IsFalse(file.ContainsString("2mystr"));
                Assert.IsFalse(file.ContainsString("2mystr2"));
                Assert.IsFalse(file.ContainsString(""));
                Assert.IsFalse(file.ContainsString("myst"));
                Assert.IsFalse(file.ContainsString("ystr"));
                Assert.IsTrue(file.ContainsString("hello2"));
                Assert.IsTrue(file.ContainsString("test_string3"));
                Assert.IsFalse(file.ContainsString("Test_string3"));
                Assert.IsFalse(file.ContainsString("TEST_STRING3"));
                Assert.IsTrue(file.GetString(0) == "mystr");
                Assert.IsTrue(file.GetString(1) == "hello2");
                Assert.IsTrue(file.GetString(2) == "test_string3");
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(3));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(-1));

                Assert.IsTrue(file.GetIndex("mystr") == 0);
                Assert.IsTrue(file.GetIndex("hello2") == 1);
                Assert.IsTrue(file.GetIndex("test_string3") == 2);
                Assert.ThrowsException<ArgumentNullException>(() => file.GetIndex(null));
                Assert.IsTrue(file.GetIndex("") == -1);
                Assert.IsTrue(file.GetIndex("mystr2") == -1);
                Assert.IsTrue(file.GetIndex("Test_string3") == -1);
                Assert.IsTrue(file.GetIndex("TEST_STRING3") == -1);

                // duplicate
                Assert.IsTrue(file.AppendString("mystr") == 0);
                Assert.IsTrue(file.StringCount == 3);
                Assert.IsTrue(file.AppendString("hello2") == 1);
                Assert.IsTrue(file.StringCount == 3);
                Assert.IsTrue(file.AppendString("Hello2") == 3);
                Assert.IsTrue(file.StringCount == 4);

                file.ClearStrings();
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
                Assert.IsTrue(file.GetIndex("mystr") == -1);
                Assert.IsTrue(file.StringCount == 0);
            }

            File.Delete(temp);
        }

        [TestMethod]
        public void TestReadingStringTableNoRandomAccess()
        {
            var temp = Path.GetTempFileName();
            using (var file = FwobFile<Tick, int>.CreateNewFile(temp, "HelloFwob", FileMode.Create))
            {
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
                Assert.IsTrue(file.GetIndex("mystr") == -1);
                Assert.IsTrue(file.StringCount == 0);
                Assert.IsTrue(file.AppendString("mystr") == 0);
                Assert.IsTrue(file.StringCount == 1);
                Assert.IsTrue(file.AppendString("hello2") == 1);
                Assert.IsTrue(file.StringCount == 2);
                Assert.IsTrue(file.AppendString("test_string3") == 2);
                Assert.IsTrue(file.StringCount == 3);
            }

            using (var file = new FwobFile<Tick, int>(temp))
            {
                Assert.IsTrue(file.ContainsString("mystr"));
                Assert.IsFalse(file.ContainsString("mystr2"));
                Assert.IsFalse(file.ContainsString("2mystr"));
                Assert.IsFalse(file.ContainsString("2mystr2"));
                Assert.IsFalse(file.ContainsString(""));
                Assert.IsFalse(file.ContainsString("myst"));
                Assert.IsFalse(file.ContainsString("ystr"));
                Assert.IsTrue(file.ContainsString("hello2"));
                Assert.IsTrue(file.ContainsString("test_string3"));
                Assert.IsFalse(file.ContainsString("Test_string3"));
                Assert.IsFalse(file.ContainsString("TEST_STRING3"));
                Assert.IsTrue(file.GetString(0) == "mystr");
                Assert.IsTrue(file.GetString(1) == "hello2");
                Assert.IsTrue(file.GetString(2) == "test_string3");
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(3));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(-1));

                Assert.IsTrue(file.GetIndex("mystr") == 0);
                Assert.IsTrue(file.GetIndex("hello2") == 1);
                Assert.IsTrue(file.GetIndex("test_string3") == 2);
                Assert.ThrowsException<ArgumentNullException>(() => file.GetIndex(null));
                Assert.IsTrue(file.GetIndex("") == -1);
                Assert.IsTrue(file.GetIndex("mystr2") == -1);
                Assert.IsTrue(file.GetIndex("Test_string3") == -1);
                Assert.IsTrue(file.GetIndex("TEST_STRING3") == -1);

                // duplicate
                Assert.IsTrue(file.AppendString("mystr") == 0);
                Assert.IsTrue(file.StringCount == 3);
                Assert.IsTrue(file.AppendString("hello2") == 1);
                Assert.IsTrue(file.StringCount == 3);
                Assert.IsTrue(file.AppendString("Hello2") == 3);
                Assert.IsTrue(file.StringCount == 4);

                file.ClearStrings();
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
                Assert.IsTrue(file.GetIndex("mystr") == -1);
                Assert.IsTrue(file.StringCount == 0);
            }

            File.Delete(temp);
        }

        [TestMethod]
        public void TestStringTableRandomAccess()
        {
            var temp = Path.GetTempFileName();
            using (var file = FwobFile<Tick, int>.CreateNewFile(temp, "HelloFwob", FileMode.Create))
            {
                Assert.IsTrue(file.Strings.Count == 0);

                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
                Assert.IsTrue(file.GetIndex("mystr") == -1);
                Assert.IsTrue(file.StringCount == 0);
                Assert.IsTrue(file.Strings.Count == 0);
                Assert.IsTrue(file.AppendString("mystr") == 0);
                Assert.IsTrue(file.StringCount == 1);
                Assert.IsTrue(file.Strings.Count == 1);
                Assert.IsTrue(file.AppendString("hello2") == 1);
                Assert.IsTrue(file.StringCount == 2);
                Assert.IsTrue(file.Strings.Count == 2);
                Assert.IsTrue(file.AppendString("test_string3") == 2);
                Assert.IsTrue(file.StringCount == 3);
                Assert.IsTrue(file.Strings.Count == 3);

                Assert.IsTrue(file.ContainsString("mystr"));
                Assert.IsFalse(file.ContainsString("mystr2"));
                Assert.IsFalse(file.ContainsString("2mystr"));
                Assert.IsFalse(file.ContainsString("2mystr2"));
                Assert.IsFalse(file.ContainsString(""));
                Assert.IsFalse(file.ContainsString("myst"));
                Assert.IsFalse(file.ContainsString("ystr"));
                Assert.IsTrue(file.ContainsString("hello2"));
                Assert.IsTrue(file.ContainsString("test_string3"));
                Assert.IsFalse(file.ContainsString("Test_string3"));
                Assert.IsFalse(file.ContainsString("TEST_STRING3"));
                Assert.IsTrue(file.GetString(0) == "mystr");
                Assert.IsTrue(file.GetString(1) == "hello2");
                Assert.IsTrue(file.GetString(2) == "test_string3");
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(3));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(-1));

                Assert.IsTrue(file.GetIndex("mystr") == 0);
                Assert.IsTrue(file.GetIndex("hello2") == 1);
                Assert.IsTrue(file.GetIndex("test_string3") == 2);
                Assert.ThrowsException<ArgumentNullException>(() => file.GetIndex(null));
                Assert.IsTrue(file.GetIndex("") == -1);
                Assert.IsTrue(file.GetIndex("mystr2") == -1);
                Assert.IsTrue(file.GetIndex("Test_string3") == -1);
                Assert.IsTrue(file.GetIndex("TEST_STRING3") == -1);

                // duplicate
                Assert.IsTrue(file.AppendString("mystr") == 0);
                Assert.IsTrue(file.StringCount == 3);
                Assert.IsTrue(file.AppendString("hello2") == 1);
                Assert.IsTrue(file.StringCount == 3);
                Assert.IsTrue(file.AppendString("Hello2") == 3);
                Assert.IsTrue(file.StringCount == 4);

                file.ClearStrings();
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
                Assert.IsTrue(file.GetIndex("mystr") == -1);
                Assert.IsTrue(file.StringCount == 0);
                Assert.IsTrue(file.Strings.Count == 0);
            }

            File.Delete(temp);
        }

        [TestMethod]
        public void TestReadingStringTableRandomAccess()
        {
            var temp = Path.GetTempFileName();
            using (var file = FwobFile<Tick, int>.CreateNewFile(temp, "HelloFwob", FileMode.Create))
            {
                Assert.IsTrue(file.Strings.Count == 0);

                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
                Assert.IsTrue(file.GetIndex("mystr") == -1);
                Assert.IsTrue(file.StringCount == 0);
                Assert.IsTrue(file.Strings.Count == 0);
                Assert.IsTrue(file.AppendString("mystr") == 0);
                Assert.IsTrue(file.StringCount == 1);
                Assert.IsTrue(file.Strings.Count == 1);
                Assert.IsTrue(file.AppendString("hello2") == 1);
                Assert.IsTrue(file.StringCount == 2);
                Assert.IsTrue(file.Strings.Count == 2);
                Assert.IsTrue(file.AppendString("test_string3") == 2);
                Assert.IsTrue(file.StringCount == 3);
                Assert.IsTrue(file.Strings.Count == 3);
            }

            using (var file = new FwobFile<Tick, int>(temp))
            {
                Assert.IsTrue(file.ContainsString("mystr"));
                Assert.IsFalse(file.ContainsString("mystr2"));
                Assert.IsFalse(file.ContainsString("2mystr"));
                Assert.IsFalse(file.ContainsString("2mystr2"));
                Assert.IsFalse(file.ContainsString(""));
                Assert.IsFalse(file.ContainsString("myst"));
                Assert.IsFalse(file.ContainsString("ystr"));
                Assert.IsTrue(file.ContainsString("hello2"));
                Assert.IsTrue(file.ContainsString("test_string3"));
                Assert.IsFalse(file.ContainsString("Test_string3"));
                Assert.IsFalse(file.ContainsString("TEST_STRING3"));
                Assert.IsTrue(file.GetString(0) == "mystr");
                Assert.IsTrue(file.GetString(1) == "hello2");
                Assert.IsTrue(file.GetString(2) == "test_string3");
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(3));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(-1));

                Assert.IsTrue(file.GetIndex("mystr") == 0);
                Assert.IsTrue(file.GetIndex("hello2") == 1);
                Assert.IsTrue(file.GetIndex("test_string3") == 2);
                Assert.ThrowsException<ArgumentNullException>(() => file.GetIndex(null));
                Assert.IsTrue(file.GetIndex("") == -1);
                Assert.IsTrue(file.GetIndex("mystr2") == -1);
                Assert.IsTrue(file.GetIndex("Test_string3") == -1);
                Assert.IsTrue(file.GetIndex("TEST_STRING3") == -1);

                // duplicate
                Assert.IsTrue(file.AppendString("mystr") == 0);
                Assert.IsTrue(file.StringCount == 3);
                Assert.IsTrue(file.AppendString("hello2") == 1);
                Assert.IsTrue(file.StringCount == 3);
                Assert.IsTrue(file.AppendString("Hello2") == 3);
                Assert.IsTrue(file.StringCount == 4);

                file.ClearStrings();
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
                Assert.IsTrue(file.GetIndex("mystr") == -1);
                Assert.IsTrue(file.StringCount == 0);
                Assert.IsTrue(file.Strings.Count == 0);
            }

            File.Delete(temp);
        }
    }
}
