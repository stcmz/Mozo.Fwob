using Fwob;
using Fwob.Header;
using Fwob.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace FwobUnitTest
{
    [TestClass]
    public class FwobFileTest
    {
        private class Tick : IFrame<int>
        {
            public int Time;
            public double Value;
            [Length(4)]
            public string Str;

            public int Key => Time;

            public override bool Equals(object obj)
            {
                if (obj is Tick that)
                    return this.Time == that.Time
                        && this.Value == that.Value
                        && (this.Str == that.Str || this.Str == null && that.Str == "" || this.Str == "" && that.Str == null);
                return false;
            }
        }

        private string _tempPath = null;
        private FwobFile<Tick, int> file = null;
        private readonly Tick tick = new Tick { Time = 12, Value = 99.88 };
        private readonly Tick tick2 = new Tick { Time = 12, Value = 44456.0111 };
        private readonly Tick tick3 = new Tick { Time = 12, Value = 1234.56 };
        private readonly Tick tick4 = new Tick { Time = 13, Value = 44456.0111 };
        private readonly Tick tick5 = new Tick { Time = 100, Value = 77234.56 };

        [TestInitialize]
        public void Initialize()
        {
            _tempPath = Path.GetTempFileName();
            file = FwobFile<Tick, int>.CreateNewFile(_tempPath, "HelloFwob", FileMode.Create);
        }

        [TestCleanup]
        public void CleanUp()
        {
            file.Dispose();
            File.Delete(_tempPath);
        }

        #region Header testing
        [TestMethod]
        public void TestTitle()
        {
            Assert.IsTrue(file.Title == "HelloFwob");

            file.Title = "abcde";
            Assert.AreEqual(file.Title, "abcde");
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            Assert.AreEqual(file.Title, "abcde");

            Assert.ThrowsException<ArgumentException>(() => file.Title = "");
            Assert.AreEqual(file.Title, "abcde");
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            Assert.AreEqual(file.Title, "abcde");

            file.Title = "0123456789abcdef";
            Assert.AreEqual(file.Title, "0123456789abcdef");
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            Assert.AreEqual(file.Title, "0123456789abcdef");

            Assert.ThrowsException<ArgumentNullException>(() => file.Title = null);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.Title = "0123456789abcdefg");
            Assert.AreEqual(file.Title, "0123456789abcdef");
        }
        #endregion

        #region StringTable testing

        [TestMethod]
        public void TestWritingBasicFile()
        {
            Assert.IsNull(file.FirstFrame);
            Assert.IsNull(file.LastFrame);
            Assert.IsTrue(file.StringCount == 0);
            Assert.IsTrue(file.FrameCount == 0);
            Assert.IsTrue(file.FrameInfo.FrameType == "Tick");
            Assert.IsTrue(file.FrameInfo.FrameLength == 16);
            Assert.IsTrue(file.FrameInfo.FieldTypes == 0x320);
            Assert.IsTrue(file.FrameInfo.Fields.Count == 3);
            Assert.IsTrue(file.FrameInfo.Fields[0].FieldLength == 4);
            Assert.IsTrue(file.FrameInfo.Fields[1].FieldLength == 8);
            Assert.IsTrue(file.FrameInfo.Fields[2].FieldLength == 4);
            Assert.IsTrue(file.FrameInfo.Fields[0].FieldName == "Time");
            Assert.IsTrue(file.FrameInfo.Fields[1].FieldName == "Value");
            Assert.IsTrue(file.FrameInfo.Fields[2].FieldName == "Str");
            Assert.IsTrue(file.FrameInfo.Fields[0].FieldType == FieldType.SignedInteger);
            Assert.IsTrue(file.FrameInfo.Fields[1].FieldType == FieldType.FloatingPoint);
            Assert.IsTrue(file.FrameInfo.Fields[2].FieldType == FieldType.Utf8String);
            file.LoadStringTable();
            Assert.IsTrue(file.Strings.Count == 0);
            Assert.IsTrue(file.Title == "HelloFwob");
        }

        [TestMethod]
        public void TestReadingBasicFile()
        {
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            TestWritingBasicFile();
        }

        [TestMethod]
        public void TestWritingStringTable()
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

        private void ValidateStringTableData()
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
            Assert.IsTrue(file.AppendString("test_string3") == 2);
            Assert.IsTrue(file.StringCount == 3);
        }

        [TestMethod]
        public void TestClearingStringTable()
        {
            file.ClearStrings();
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
            Assert.IsTrue(file.GetIndex("mystr") == -1);
            Assert.IsTrue(file.StringCount == 0);

            TestWritingStringTable();
            file.ClearStrings();
            Assert.AreEqual(new FileInfo(_tempPath).Length, FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
            Assert.IsTrue(file.GetIndex("mystr") == -1);
            Assert.IsTrue(file.StringCount == 0);

            // adding after clearing
            Assert.IsTrue(file.AppendString("hello2") == 0);
            Assert.IsTrue(file.StringCount == 1);
            Assert.IsTrue(file.GetString(0) == "hello2");
            Assert.IsTrue(file.GetIndex("mystr") == -1);
            Assert.IsTrue(file.GetIndex("hello2") == 0);
            Assert.IsTrue(file.StringCount == 1);
        }

        [TestMethod]
        public void TestReadingStringTable()
        {
            TestWritingStringTable();
            ValidateStringTableData();
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            ValidateStringTableData();
            TestClearingStringTable();
        }

        [TestMethod]
        public void TestReadingStringTableCached()
        {
            file.LoadStringTable();
            Assert.IsTrue(file.Strings.Count == 0);
            TestWritingStringTable();
            ValidateStringTableData();
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            file.LoadStringTable();
            Assert.IsTrue(file.Strings.Count == 3);
            ValidateStringTableData();
            TestClearingStringTable();
            Assert.IsTrue(file.Strings.Count == 1);
        }

        [TestMethod]
        public void TestCachingStringTable()
        {
            file.LoadStringTable();
            Assert.IsTrue(file.Strings.Count == 0);
            TestWritingStringTable();
            Assert.IsTrue(file.Strings.Count == 3);
            Assert.AreEqual(file.Strings[0], "mystr");
            Assert.AreEqual(file.Strings[1], "hello2");
            Assert.AreEqual(file.Strings[2], "test_string3");
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            file.LoadStringTable();
            Assert.IsTrue(file.Strings.Count == 3);
            Assert.AreEqual(file.Strings[0], "mystr");
            Assert.AreEqual(file.Strings[1], "hello2");
            Assert.AreEqual(file.Strings[2], "test_string3");
            TestClearingStringTable();
            Assert.IsTrue(file.Strings.Count == 1);
            Assert.AreEqual(file.Strings[0], "hello2");
        }

        [TestMethod]
        public void TestStringTableLimit()
        {
            Assert.ThrowsException<ArgumentNullException>(() => file.AppendString(null));

            for (int i = 0; i < 262; i++) // (2048 - 214) / 7 = 262
                Assert.IsTrue(file.AppendString($"abc{i:d3}") == i);
            Assert.ThrowsException<InternalBufferOverflowException>(() => file.AppendString("x"));

            for (int i = 0; i < 262; i++)
            {
                Assert.AreEqual(file.GetIndex($"abc{i:d3}"), i);
                Assert.AreEqual(file.GetString(i), $"abc{i:d3}");
            }

            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);

            for (int i = 0; i < 262; i++)
            {
                Assert.AreEqual(file.GetIndex($"abc{i:d3}"), i);
                Assert.AreEqual(file.GetString(i), $"abc{i:d3}");
            }
            Assert.ThrowsException<InternalBufferOverflowException>(() => file.AppendString(""));
        }

        #endregion

        #region Frame data testing
        [TestMethod]
        public void TestNoFrame()
        {
            Assert.IsNull(file.FirstFrame);
            Assert.IsNull(file.LastFrame);
            Assert.IsTrue(file.FrameCount == 0);
            Assert.IsNotNull(file.FrameInfo);

            Assert.IsNull(file.GetFrame(0));
            Assert.IsNull(file.GetFrame(-1));
            Assert.IsNull(file.GetFrame(1));
            Assert.IsFalse(file.GetFrames(0).Any());
            Assert.IsFalse(file.GetFrames(0, 12).Any());
            Assert.IsFalse(file.GetFramesAfter(0).Any());
            Assert.IsFalse(file.GetFramesBefore(0).Any());
        }

        private void AddOneFrame()
        {
            Assert.IsTrue(file.AppendFrames(tick) == 1);
        }

        private void ValidateOneFrame()
        {
            Assert.AreEqual(file.FirstFrame, tick);
            Assert.AreEqual(file.LastFrame, tick);
            Assert.IsTrue(file.FrameCount == 1);
            Assert.IsNotNull(file.FrameInfo);

            Assert.IsNull(file.GetFrame(-1));
            Assert.AreEqual(file.GetFrame(0), tick);
            Assert.IsNull(file.GetFrame(1));
            Assert.IsNull(file.GetFrame(12));
            Assert.IsFalse(file.GetFrames(-1).Any());
            Assert.IsTrue(file.GetFrames(12).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 20).Count() == 1);
            Assert.IsTrue(file.GetFrames(11, 20).Count() == 1);
            Assert.IsTrue(file.GetFrames(12, 20).Count() == 1);
            Assert.IsTrue(file.GetFrames(13, 20).Count() == 0);
            Assert.IsTrue(file.GetFrames(-1, 13).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 12).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 11).Count() == 0);
            Assert.IsTrue(file.GetFramesAfter(-1).Count() == 1);
            Assert.IsTrue(file.GetFramesAfter(11).Count() == 1);
            Assert.IsTrue(file.GetFramesAfter(12).Count() == 1);
            Assert.IsTrue(file.GetFramesAfter(13).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(0).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(11).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(12).Count() == 1);
            Assert.IsTrue(file.GetFramesBefore(13).Count() == 1);
        }

        [TestMethod]
        public void TestOneFrame()
        {
            AddOneFrame();
            ValidateOneFrame();
        }

        [TestMethod]
        public void TestClearingFrame()
        {
            Assert.AreEqual(new FileInfo(_tempPath).Length, FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength);
            file.ClearFrames();
            TestNoFrame();
            file.Dispose();
            Assert.AreEqual(new FileInfo(_tempPath).Length, FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength);
            file = new FwobFile<Tick, int>(_tempPath);
            TestNoFrame();
        }

        [TestMethod]
        public void TestReadingOneFrame()
        {
            TestNoFrame();
            AddOneFrame();
            ValidateOneFrame();
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            ValidateOneFrame();
            file.ClearFrames();
            TestNoFrame();
        }

        [TestMethod]
        public void TestFramesSameKey()
        {
            AddFramesSameKey();
            ValidateFramesSameKey();
        }

        private void AddFramesSameKey()
        {
            Assert.IsTrue(file.AppendFrames(tick, tick2, tick3) == 3);
        }

        private void ValidateFramesSameKey()
        {
            Assert.AreEqual(file.FirstFrame, tick);
            Assert.AreEqual(file.LastFrame, tick3);
            Assert.IsTrue(file.FrameCount == 3);
            Assert.IsNotNull(file.FrameInfo);

            Assert.IsNull(file.GetFrame(-1));
            Assert.AreEqual(file.GetFrame(0), tick);
            Assert.AreEqual(file.GetFrame(1), tick2);
            Assert.AreEqual(file.GetFrame(2), tick3);
            Assert.IsNull(file.GetFrame(3));
            Assert.IsNull(file.GetFrame(12));
            Assert.IsFalse(file.GetFrames(-1).Any());
            Assert.IsTrue(file.GetFrames(12).Count() == 3);
            Assert.IsTrue(file.GetFrames(-1, 20).Count() == 3);
            Assert.IsTrue(file.GetFrames(11, 20).Count() == 3);
            Assert.IsTrue(file.GetFrames(12, 20).Count() == 3);
            Assert.IsTrue(file.GetFrames(13, 20).Count() == 0);
            Assert.IsTrue(file.GetFrames(-1, 13).Count() == 3);
            Assert.IsTrue(file.GetFrames(-1, 12).Count() == 3);
            Assert.IsTrue(file.GetFrames(-1, 11).Count() == 0);
            Assert.IsTrue(file.GetFramesAfter(-1).Count() == 3);
            Assert.IsTrue(file.GetFramesAfter(11).Count() == 3);
            Assert.IsTrue(file.GetFramesAfter(12).Count() == 3);
            Assert.IsTrue(file.GetFramesAfter(13).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(0).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(11).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(12).Count() == 3);
            Assert.IsTrue(file.GetFramesBefore(13).Count() == 3);
        }

        [TestMethod]
        public void TestReadingFramesSameKey()
        {
            TestNoFrame();
            AddFramesSameKey();
            ValidateFramesSameKey();
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            ValidateFramesSameKey();
            file.ClearFrames();
            TestNoFrame();
        }

        private void AddFramesMultiKeys()
        {
            Assert.IsTrue(file.AppendFrames(tick, tick4, tick5) == 3);
        }

        private void ValidateFramesMultiKeys()
        {
            Assert.AreEqual(file.FirstFrame, tick);
            Assert.AreEqual(file.LastFrame, tick5);
            Assert.IsTrue(file.FrameCount == 3);
            Assert.IsNotNull(file.FrameInfo);

            Assert.IsNull(file.GetFrame(-1));
            Assert.AreEqual(file.GetFrame(0), tick);
            Assert.AreEqual(file.GetFrame(1), tick4);
            Assert.AreEqual(file.GetFrame(2), tick5);
            Assert.IsNull(file.GetFrame(3));
            Assert.IsNull(file.GetFrame(12));
            Assert.IsFalse(file.GetFrames(-1).Any());
            Assert.IsTrue(file.GetFrames(12).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 20).Count() == 2);
            Assert.IsTrue(file.GetFrames(11, 20).Count() == 2);
            Assert.IsTrue(file.GetFrames(12, 20).Count() == 2);
            Assert.IsTrue(file.GetFrames(13, 20).Count() == 1);
            Assert.IsTrue(file.GetFrames(14, 20).Count() == 0);
            Assert.IsTrue(file.GetFrames(-1, 14).Count() == 2);
            Assert.IsTrue(file.GetFrames(-1, 13).Count() == 2);
            Assert.IsTrue(file.GetFrames(-1, 12).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 11).Count() == 0);
            Assert.IsTrue(file.GetFramesAfter(-1).Count() == 3);
            Assert.IsTrue(file.GetFramesAfter(11).Count() == 3);
            Assert.IsTrue(file.GetFramesAfter(12).Count() == 3);
            Assert.IsTrue(file.GetFramesAfter(13).Count() == 2);
            Assert.IsTrue(file.GetFramesAfter(14).Count() == 1);
            Assert.IsTrue(file.GetFramesAfter(100).Count() == 1);
            Assert.IsTrue(file.GetFramesAfter(101).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(0).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(11).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(12).Count() == 1);
            Assert.IsTrue(file.GetFramesBefore(13).Count() == 2);
            Assert.IsTrue(file.GetFramesBefore(14).Count() == 2);
            Assert.IsTrue(file.GetFramesBefore(99).Count() == 2);
            Assert.IsTrue(file.GetFramesBefore(100).Count() == 3);
            Assert.IsTrue(file.GetFramesBefore(101).Count() == 3);
        }

        [TestMethod]
        public void TestFramesMultiKeys()
        {
            AddFramesMultiKeys();
            ValidateFramesMultiKeys();
        }

        [TestMethod]
        public void TestReadingFramesMultiKeys()
        {
            TestNoFrame();
            AddFramesMultiKeys();
            ValidateFramesMultiKeys();
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            ValidateFramesMultiKeys();
            file.ClearFrames();
            TestNoFrame();
        }

        private void AddFramesPartially()
        {
            Assert.ThrowsException<InvalidDataException>(() => file.AppendFrames(tick3, tick5, tick4));
        }

        private void ValidateFramesPartially()
        {
            Assert.AreEqual(file.FirstFrame, tick3);
            Assert.AreEqual(file.LastFrame, tick5);
            Assert.IsTrue(file.FrameCount == 2);
            Assert.IsNotNull(file.FrameInfo);

            Assert.IsNull(file.GetFrame(-1));
            Assert.AreEqual(file.GetFrame(0), tick3);
            Assert.AreEqual(file.GetFrame(1), tick5);
            Assert.IsNull(file.GetFrame(2));
            Assert.IsNull(file.GetFrame(12));
            Assert.IsFalse(file.GetFrames(-1).Any());
            Assert.IsTrue(file.GetFrames(12).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 20).Count() == 1);
            Assert.IsTrue(file.GetFrames(11, 20).Count() == 1);
            Assert.IsTrue(file.GetFrames(12, 20).Count() == 1);
            Assert.IsTrue(file.GetFrames(13, 20).Count() == 0);
            Assert.IsTrue(file.GetFrames(14, 100).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 13).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 12).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 11).Count() == 0);
            Assert.IsTrue(file.GetFramesAfter(-1).Count() == 2);
            Assert.IsTrue(file.GetFramesAfter(11).Count() == 2);
            Assert.IsTrue(file.GetFramesAfter(12).Count() == 2);
            Assert.IsTrue(file.GetFramesAfter(13).Count() == 1);
            Assert.IsTrue(file.GetFramesAfter(100).Count() == 1);
            Assert.IsTrue(file.GetFramesAfter(101).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(0).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(11).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(12).Count() == 1);
            Assert.IsTrue(file.GetFramesBefore(13).Count() == 1);
            Assert.IsTrue(file.GetFramesBefore(99).Count() == 1);
            Assert.IsTrue(file.GetFramesBefore(100).Count() == 2);
            Assert.IsTrue(file.GetFramesBefore(101).Count() == 2);

            Assert.ThrowsException<InvalidDataException>(() => file.AppendFrames(tick4));
            Assert.AreEqual(file.FirstFrame, tick3);
            Assert.AreEqual(file.LastFrame, tick5);
            Assert.IsTrue(file.FrameCount == 2);
            Assert.IsNotNull(file.FrameInfo);

            Assert.IsNull(file.GetFrame(-1));
            Assert.AreEqual(file.GetFrame(0), tick3);
            Assert.AreEqual(file.GetFrame(1), tick5);
            Assert.IsNull(file.GetFrame(2));
            Assert.IsNull(file.GetFrame(12));
            Assert.IsFalse(file.GetFrames(-1).Any());
            Assert.IsTrue(file.GetFrames(12).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 20).Count() == 1);
            Assert.IsTrue(file.GetFrames(11, 20).Count() == 1);
            Assert.IsTrue(file.GetFrames(12, 20).Count() == 1);
            Assert.IsTrue(file.GetFrames(13, 20).Count() == 0);
            Assert.IsTrue(file.GetFrames(14, 100).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 13).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 12).Count() == 1);
            Assert.IsTrue(file.GetFrames(-1, 11).Count() == 0);
            Assert.IsTrue(file.GetFramesAfter(-1).Count() == 2);
            Assert.IsTrue(file.GetFramesAfter(11).Count() == 2);
            Assert.IsTrue(file.GetFramesAfter(12).Count() == 2);
            Assert.IsTrue(file.GetFramesAfter(13).Count() == 1);
            Assert.IsTrue(file.GetFramesAfter(100).Count() == 1);
            Assert.IsTrue(file.GetFramesAfter(101).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(0).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(11).Count() == 0);
            Assert.IsTrue(file.GetFramesBefore(12).Count() == 1);
            Assert.IsTrue(file.GetFramesBefore(13).Count() == 1);
            Assert.IsTrue(file.GetFramesBefore(99).Count() == 1);
            Assert.IsTrue(file.GetFramesBefore(100).Count() == 2);
            Assert.IsTrue(file.GetFramesBefore(101).Count() == 2);
        }

        [TestMethod]
        public void TestFramesPartially()
        {
            AddFramesPartially();
            ValidateFramesPartially();
        }

        [TestMethod]
        public void TestFramesStringField()
        {
            var t = new Tick { Time = 987, Value = 123.456, Str = null };
            Assert.IsTrue(file.AppendFrames(t) == 1);
            Assert.AreEqual(file.GetFrame(0), t);
            Assert.AreEqual(file.LastFrame, t);
            Assert.IsTrue(file.FrameCount == 1);
            t.Str = "";
            Assert.IsTrue(file.AppendFrames(t) == 1);
            Assert.AreEqual(file.GetFrame(1), t);
            Assert.AreEqual(file.LastFrame, t);
            Assert.IsTrue(file.FrameCount == 2);
            t.Str = "a";
            Assert.IsTrue(file.AppendFrames(t) == 1);
            Assert.AreEqual(file.GetFrame(2), t);
            Assert.AreEqual(file.LastFrame, t);
            Assert.IsTrue(file.FrameCount == 3);
            t.Str = "abcd";
            Assert.IsTrue(file.AppendFrames(t) == 1);
            Assert.AreEqual(file.GetFrame(3), t);
            Assert.AreEqual(file.LastFrame, t);
            Assert.IsTrue(file.FrameCount == 4);
            t.Str = "abcde";
            Assert.ThrowsException<InvalidDataException>(() => file.AppendFrames(tick));
            Assert.IsTrue(file.FrameCount == 4);

            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);

            Assert.IsTrue(file.FrameCount == 4);
            t.Str = null;
            Assert.AreEqual(file.GetFrame(0), t);
            t.Str = "";
            Assert.AreEqual(file.GetFrame(1), t);
            t.Str = "a";
            Assert.AreEqual(file.GetFrame(2), t);
            t.Str = "abcd";
            Assert.AreEqual(file.GetFrame(3), t);
        }

        [TestMethod]
        public void TestReadingFramesPartially()
        {
            TestNoFrame();
            AddFramesPartially();
            ValidateFramesPartially();
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            ValidateFramesPartially();
            file.ClearFrames();
            TestNoFrame();
        }

        [TestMethod]
        public void TestFramesTransactional()
        {
            Assert.ThrowsException<InvalidDataException>(() => file.AppendFramesTx(tick3, tick5, tick4));
            TestNoFrame();
            AddOneFrame();
            Assert.ThrowsException<InvalidDataException>(() => file.AppendFramesTx(tick5, tick4));
            ValidateOneFrame();
            Assert.IsTrue(file.AppendFrames(tick5) == 1);
            Assert.IsTrue(file.FrameCount == 2);
            Assert.AreEqual(file.FirstFrame, tick);
            Assert.AreEqual(file.LastFrame, tick5);
            Assert.IsNotNull(file.FrameInfo);

            Assert.ThrowsException<InvalidDataException>(() => file.AppendFramesTx(tick2));
            Assert.ThrowsException<InvalidDataException>(() => file.AppendFramesTx(tick3));
            Assert.ThrowsException<InvalidDataException>(() => file.AppendFramesTx(tick4));
            Assert.IsTrue(file.FrameCount == 2);
            Assert.AreEqual(file.FirstFrame, tick);
            Assert.AreEqual(file.LastFrame, tick5);
            Assert.IsNotNull(file.FrameInfo);
        }

        [TestMethod]
        public void TestReadingFramesTransactional()
        {
            Assert.ThrowsException<InvalidDataException>(() => file.AppendFramesTx(tick3, tick5, tick4));
            TestNoFrame();
            AddOneFrame();
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);

            Assert.ThrowsException<InvalidDataException>(() => file.AppendFramesTx(tick5, tick4));
            ValidateOneFrame();
            Assert.IsTrue(file.AppendFrames(tick5) == 1);
            Assert.IsTrue(file.FrameCount == 2);
            Assert.AreEqual(file.FirstFrame, tick);
            Assert.AreEqual(file.LastFrame, tick5);
            Assert.IsNotNull(file.FrameInfo);

            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            Assert.ThrowsException<InvalidDataException>(() => file.AppendFramesTx(tick2));
            Assert.ThrowsException<InvalidDataException>(() => file.AppendFramesTx(tick3));
            Assert.ThrowsException<InvalidDataException>(() => file.AppendFramesTx(tick4));
            Assert.IsTrue(file.FrameCount == 2);
            Assert.AreEqual(file.FirstFrame, tick);
            Assert.AreEqual(file.LastFrame, tick5);
            Assert.IsNotNull(file.FrameInfo);
        }
        #endregion

        #region Mixed and performance testing
        [TestMethod]
        public void TestMixingStringTableAndFrames()
        {
            TestWritingStringTable();
            AddOneFrame();
            ValidateStringTableData();
            ValidateOneFrame();
            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            ValidateOneFrame();
            ValidateStringTableData();
        }

        [TestMethod]
        public void TestStringTableAndFramesPerformance()
        {
            Assert.ThrowsException<ArgumentNullException>(() => file.AppendString(null));

            for (int i = 0; i < 262; i++) // (2048 - 214) / 7 = 262
            {
                Assert.IsTrue(file.AppendString($"abc{i:d3}") == i);
                Assert.IsTrue(file.AppendFrames(
                    new Tick { Time = i, Value = i * 3.14 },
                    new Tick { Time = i, Value = i * 0.61 },
                    new Tick { Time = i, Value = i * 6.22 },
                    new Tick { Time = i, Value = i * 2.71 },
                    new Tick { Time = i, Value = i * 9.99 }) == 5);
                Assert.IsTrue(file.FrameCount == 5 * i + 5);
                Assert.AreEqual(file.FirstFrame, new Tick { Time = 0, Value = 0 });
                Assert.AreEqual(file.LastFrame, new Tick { Time = i, Value = i * 9.99 });
            }

            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            Assert.ThrowsException<InternalBufferOverflowException>(() => file.AppendString("x"));
            Assert.AreEqual(file.FrameCount, 5 * 262);

            for (int i = 0; i < 262; i++)
            {
                Assert.AreEqual(file.GetIndex($"abc{i:d3}"), i);
                Assert.IsTrue(file.AppendFrames(
                    new Tick { Time = 1000 + i, Value = -i * 3.14 },
                    new Tick { Time = 1000 + i, Value = -i * 0.61 },
                    new Tick { Time = 1000 + i, Value = -i * 6.22 },
                    new Tick { Time = 1000 + i, Value = -i * 2.71 },
                    new Tick { Time = 1000 + i, Value = -i * 9.99 },
                    new Tick { Time = 1000 + i, Value = -i * 8.11 }) == 6);
                Assert.IsTrue(file.FrameCount == 5 * 262 + 6 * i + 6);
                Assert.AreEqual(file.FirstFrame, new Tick { Time = 0, Value = 0 });
                Assert.AreEqual(file.LastFrame, new Tick { Time = 1000 + i, Value = -i * 8.11 });
            }

            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            Assert.ThrowsException<InternalBufferOverflowException>(() => file.AppendString("x"));
            Assert.AreEqual(file.FrameCount, 11 * 262);

            for (int i = 0; i < 262; i++)
            {
                Assert.AreEqual(file.GetString(i), $"abc{i:d3}");
                Assert.IsTrue(file.AppendFrames(
                    new Tick { Time = 2000 + i, Value = i * 3.14 },
                    new Tick { Time = 2000 + i, Value = i * 0.61 },
                    new Tick { Time = 2000 + i, Value = i * 6.22 },
                    new Tick { Time = 2000 + i, Value = i * 2.71 },
                    new Tick { Time = 2000 + i, Value = i * 9.99 },
                    new Tick { Time = 2000 + i, Value = i * 8.11 },
                    new Tick { Time = 2000 + i, Value = i * 1.56 }) == 7);
                Assert.IsTrue(file.FrameCount == 11 * 262 + 7 * i + 7);
                Assert.AreEqual(file.FirstFrame, new Tick { Time = 0, Value = 0 });
                Assert.AreEqual(file.LastFrame, new Tick { Time = 2000 + i, Value = i * 1.56 });
            }

            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            Assert.ThrowsException<InternalBufferOverflowException>(() => file.AppendString("x"));
            Assert.AreEqual(file.FrameCount, 18 * 262);

            for (int i = 0; i < 262; i++)
            {
                Assert.AreEqual(file.GetIndex($"abc{i:d3}"), i);
                Assert.AreEqual(file.GetFrames(i).Count(), 5);
                Assert.AreEqual(file.GetFrames(1000 + i).Count(), 6);
                Assert.AreEqual(file.GetFrames(2000 + i).Count(), 7);
                Assert.AreEqual(file.GetFramesAfter(i).Count(), 262 * 18 - 5 * i);
                Assert.AreEqual(file.GetFramesAfter(1000 + i).Count(), 262 * 13 - 6 * i);
                Assert.AreEqual(file.GetFramesAfter(2000 + i).Count(), 262 * 7 - 7 * i);
                Assert.AreEqual(file.GetFramesBefore(i).Count(), 5 * i + 5);
                Assert.AreEqual(file.GetFramesBefore(1000 + i).Count(), 262 * 5 + 6 * i + 6);
                Assert.AreEqual(file.GetFramesBefore(2000 + i).Count(), 262 * 11 + 7 * i + 7);
                Assert.AreEqual(file.GetFrames(i, 1000 + i).Count(), 262 * 5 + i + 6);
                Assert.AreEqual(file.GetFrames(1000 + i, 2000 + i).Count(), 262 * 6 + i + 7);
                Assert.AreEqual(file.GetFrame(i * 5), new Tick { Time = i, Value = i * 3.14 });
                Assert.AreEqual(file.GetFrame(262 * 5 + i * 6 + 1), new Tick { Time = 1000 + i, Value = -i * 0.61 });
                Assert.AreEqual(file.GetFrame(262 * 11 + i * 7 + 2), new Tick { Time = 2000 + i, Value = i * 6.22 });
                Assert.AreEqual(file.GetString(i), $"abc{i:d3}");
            }

            file.Dispose();
            file = new FwobFile<Tick, int>(_tempPath);
            Assert.ThrowsException<InternalBufferOverflowException>(() => file.AppendString(""));
            Assert.AreEqual(file.FrameCount, 18 * 262);
        }
        #endregion

        #region
        [TestMethod]
        public void TestFileSplit()
        {
            file.Dispose();
            Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Split(null));
            Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Split(_tempPath));
            Assert.ThrowsException<FrameNotFoundException>(() => FwobFile<Tick, int>.Split(_tempPath, 1, 2));
            file = new FwobFile<Tick, int>(_tempPath);

            for (int i = 0; i < 485; i++)
                Assert.IsTrue(file.AppendString(i.ToString()) == i);
            Assert.IsTrue(
                10000 == file.AppendFrames(
                    Enumerable.Range(0, 10000)
                        .Select(i => new Tick { Time = i, Value = i * 3.14, Str = i.ToString() })));
            Assert.IsTrue(file.FrameCount == 10000);
            Assert.AreEqual(file.FirstFrame, new Tick { Time = 0, Value = 0, Str = "0" });
            Assert.AreEqual(file.LastFrame, new Tick { Time = 9999, Value = 9999 * 3.14, Str = "9999" });
            file.Dispose();

            Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Split(_tempPath, null));
            Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Split(_tempPath, 1, 0));
            Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Split(_tempPath, 1, 1));
            Assert.ThrowsException<FileNotFoundException>(() => FwobFile<Tick, int>.Split(_tempPath + ".nonexistence", 1, 2));
            Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Split(_tempPath, -1));
            Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Split(_tempPath, 10000));

            int baseLength = FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength;

            FwobFile<Tick, int>.Split(_tempPath, 100);
            string path0 = Path.ChangeExtension(_tempPath, ".part0.fwob");
            string path1 = Path.ChangeExtension(_tempPath, ".part1.fwob");
            Assert.IsTrue(File.Exists(path0));
            Assert.IsTrue(File.Exists(path1));
            Assert.IsTrue(new FileInfo(path0).Length == baseLength + 100 * 16);
            Assert.IsTrue(new FileInfo(path1).Length == baseLength + 9900 * 16);
            using (var ff = new FwobFile<Tick, int>(path0))
            {
                Assert.IsTrue(ff.FrameCount == 100);
                Assert.AreEqual(ff.FirstFrame, new Tick { Time = 0, Value = 0, Str = "0" });
                Assert.AreEqual(ff.LastFrame, new Tick { Time = 99, Value = 99 * 3.14, Str = "99" });
                ff.LoadStringTable();
                Assert.IsTrue(ff.Strings.Count == 485);
                for (int i = 0; i < 485; i++)
                    Assert.IsTrue(ff.Strings[i] == i.ToString());
            }
            using (var ff = new FwobFile<Tick, int>(path1))
            {
                Assert.IsTrue(ff.FrameCount == 9900);
                Assert.AreEqual(ff.FirstFrame, new Tick { Time = 100, Value = 100 * 3.14, Str = "100" });
                Assert.AreEqual(ff.LastFrame, new Tick { Time = 9999, Value = 9999 * 3.14, Str = "9999" });
                ff.LoadStringTable();
                Assert.IsTrue(ff.Strings.Count == 485);
                for (int i = 0; i < 485; i++)
                    Assert.IsTrue(ff.Strings[i] == i.ToString());
            }

            FwobFile<Tick, int>.Split(_tempPath, 1, 9999);
            string path2 = Path.ChangeExtension(_tempPath, ".part2.fwob");
            Assert.IsTrue(File.Exists(path0));
            Assert.IsTrue(File.Exists(path1));
            Assert.IsTrue(File.Exists(path2));
            Assert.IsTrue(new FileInfo(path0).Length == baseLength + 1 * 16);
            Assert.IsTrue(new FileInfo(path1).Length == baseLength + 9998 * 16);
            Assert.IsTrue(new FileInfo(path2).Length == baseLength + 1 * 16);
            using (var ff = new FwobFile<Tick, int>(path0))
            {
                ff.LoadStringTable();
                Assert.IsTrue(ff.Strings.Count == 485);
                for (int i = 0; i < 485; i++)
                    Assert.IsTrue(ff.Strings[i] == i.ToString());
                Assert.IsTrue(ff.FrameCount == 1);
                Assert.AreEqual(ff.FirstFrame, new Tick { Time = 0, Value = 0, Str = "0" });
                Assert.AreEqual(ff.LastFrame, new Tick { Time = 0, Value = 0, Str = "0" });
            }
            using (var ff = new FwobFile<Tick, int>(path1))
            {
                ff.LoadStringTable();
                Assert.IsTrue(ff.Strings.Count == 485);
                for (int i = 0; i < 485; i++)
                    Assert.IsTrue(ff.Strings[i] == i.ToString());
                Assert.IsTrue(ff.FrameCount == 9998);
                Assert.AreEqual(ff.FirstFrame, new Tick { Time = 1, Value = 3.14, Str = "1" });
                Assert.AreEqual(ff.LastFrame, new Tick { Time = 9998, Value = 9998 * 3.14, Str = "9998" });
            }
            using (var ff = new FwobFile<Tick, int>(path2))
            {
                ff.LoadStringTable();
                Assert.IsTrue(ff.Strings.Count == 485);
                for (int i = 0; i < 485; i++)
                    Assert.IsTrue(ff.Strings[i] == i.ToString());
                Assert.IsTrue(ff.FrameCount == 1);
                Assert.AreEqual(ff.FirstFrame, new Tick { Time = 9999, Value = 9999 * 3.14, Str = "9999" });
                Assert.AreEqual(ff.LastFrame, new Tick { Time = 9999, Value = 9999 * 3.14, Str = "9999" });
            }

            File.Delete(path0);
            File.Delete(path1);
            File.Delete(path2);
        }

        [TestMethod]
        public void TestFileConcat()
        {
            file.Dispose();
            Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Concat(null));
            Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Concat(_tempPath, null));
            Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Concat(_tempPath));
            Assert.ThrowsException<FileNotFoundException>(() => FwobFile<Tick, int>.Concat(_tempPath, _tempPath + ".nonexistence"));

            var path0 = _tempPath + ".part0.fwob";
            var path1 = _tempPath + ".part1.fwob";
            var path2 = _tempPath + ".part2.fwob";
            Assert.ThrowsException<FileNotFoundException>(() => FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2));

            // test empty files
            var file0 = FwobFile<Tick, int>.CreateNewFile(path0, "test"); file0.Dispose();
            var file1 = FwobFile<Tick, int>.CreateNewFile(path1, "test"); file1.Dispose();
            var file2 = FwobFile<Tick, int>.CreateNewFile(path2, "test2"); file2.Dispose();

            Assert.ThrowsException<FrameNotFoundException>(() => FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2));

            // test file title consistence
            file0 = FwobFile<Tick, int>.CreateNewFile(path0, "test", FileMode.Create);
            file1 = FwobFile<Tick, int>.CreateNewFile(path1, "test", FileMode.Create);
            file2 = FwobFile<Tick, int>.CreateNewFile(path2, "test2", FileMode.Create);

            file0.AppendFrames(tick); file0.Dispose();
            file1.AppendFrames(tick); file1.Dispose();
            file2.AppendFrames(tick); file2.Dispose();

            Assert.ThrowsException<FileTitleException>(() => FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2));

            file0 = FwobFile<Tick, int>.CreateNewFile(path0, "test", FileMode.Create);
            file1 = FwobFile<Tick, int>.CreateNewFile(path1, "test", FileMode.Create);
            file2 = FwobFile<Tick, int>.CreateNewFile(path2, "test", FileMode.Create);

            file0.AppendFrames(tick5); file0.Dispose();
            file1.AppendFrames(tick4); file1.Dispose();
            file2.AppendFrames(tick3); file2.Dispose();

            Assert.ThrowsException<KeyOrderingException>(() => FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2));

            file0 = FwobFile<Tick, int>.CreateNewFile(path0, "test", FileMode.Create);
            file1 = FwobFile<Tick, int>.CreateNewFile(path1, "test", FileMode.Create);
            file2 = FwobFile<Tick, int>.CreateNewFile(path2, "test", FileMode.Create);

            file0.AppendString("str0"); file0.AppendFrames(tick2); file0.Dispose();
            file1.AppendString("str0"); file1.AppendString("str1"); file1.AppendFrames(tick3); file1.Dispose();
            file2.AppendString("str1"); file2.AppendFrames(tick4); file2.Dispose();

            Assert.ThrowsException<StringTableException>(() => FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2));

            FwobFile<Tick, int>.Concat(_tempPath, path0);
            Assert.AreEqual(Convert.ToBase64String(File.ReadAllBytes(_tempPath)), Convert.ToBase64String(File.ReadAllBytes(path0)));

            file0 = FwobFile<Tick, int>.CreateNewFile(path0, "test", FileMode.Create);
            file1 = FwobFile<Tick, int>.CreateNewFile(path1, "test", FileMode.Create);
            file2 = FwobFile<Tick, int>.CreateNewFile(path2, "test", FileMode.Create);

            for (int i = 0; i < 48; i++)
                Assert.IsTrue(file0.AppendString(i.ToString()) == i);
            for (int i = 0; i < 485; i++)
                Assert.IsTrue(file1.AppendString(i.ToString()) == i);
            for (int i = 0; i < 85; i++)
                Assert.IsTrue(file2.AppendString(i.ToString()) == i);

            Assert.IsTrue(
                10000 == file0.AppendFrames(
                    Enumerable.Range(0, 10000)
                        .Select(i => new Tick { Time = i, Value = i * 3.14, Str = i.ToString() })));
            Assert.IsTrue(
                7777 == file1.AppendFrames(
                    Enumerable.Range(0, 7777)
                        .Select(i => new Tick { Time = 10000 + i, Value = i * 2.14, Str = i.ToString() })));
            Assert.IsTrue(
                8888 == file2.AppendFrames(
                    Enumerable.Range(0, 8888)
                        .Select(i => new Tick { Time = 20000 + i, Value = i * 4.14, Str = i.ToString() })));

            file0.Dispose();
            file1.Dispose();
            file2.Dispose();

            FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2);

            Assert.IsTrue(new FileInfo(_tempPath).Length == FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength + 26665 * 16);

            file = new FwobFile<Tick, int>(_tempPath);
            Assert.IsTrue(file.FrameCount == 26665);
            Assert.IsTrue(file.StringCount == 485);
            Assert.IsTrue(file.Title == "test");

            file.LoadStringTable();
            for (int i = 0; i < 485; i++)
                Assert.IsTrue(file.Strings[i] == i.ToString());
            file.UnloadStringTable();

            int k = 0;
            foreach (var frame in file.GetFramesAfter(0))
            {
                if (k < 10000)
                    Assert.AreEqual(frame, new Tick { Time = k, Value = k * 3.14, Str = k.ToString() });
                else if (k < 17777)
                    Assert.AreEqual(frame, new Tick { Time = k, Value = (k - 10000) * 2.14, Str = (k - 10000).ToString() });
                else
                    Assert.AreEqual(frame, new Tick { Time = k + 2223, Value = (k - 17777) * 4.14, Str = (k - 17777).ToString() });
                ++k;
            }

            file.Dispose();

            File.Delete(_tempPath + ".part0.fwob");
            File.Delete(_tempPath + ".part1.fwob");
            File.Delete(_tempPath + ".part2.fwob");
        }
        #endregion
    }
}
