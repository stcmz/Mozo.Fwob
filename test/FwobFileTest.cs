#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozo.Fwob.Abstraction;
using Mozo.Fwob.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mozo.Fwob.UnitTest;

[TestClass]
public class FwobFileTest
{
    private class Tick
    {
        public int Time;
        public double Value;
        [Length(4)]
        public string? Str;

        public override bool Equals(object? obj)
        {
            return GetHashCode() == obj?.GetHashCode();
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Time, Value, Str ?? string.Empty);
        }
    }

    private string? _tempPath = null;
    private FwobFile<Tick, int>? file = null;
    private readonly Tick tick12a = new() { Time = 12, Value = 99.88 };
    private readonly Tick tick12b = new() { Time = 12, Value = 44456.0111 };
    private readonly Tick tick12c = new() { Time = 12, Value = 1234.56 };
    private readonly Tick tick13 = new() { Time = 13, Value = 44456.0111 };
    private readonly Tick tick100 = new() { Time = 100, Value = 77234.56 };

    [TestInitialize]
    public void Initialize()
    {
        _tempPath = Path.GetTempFileName();
        file = new FwobFile<Tick, int>(_tempPath, "HelloFwob", FileMode.Create);
    }

    [TestCleanup]
    public void CleanUp()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        file.Dispose();
        File.Delete(_tempPath);
    }

    [TestMethod]
    public void TestFileFormatIntegrity()
    {
        string temp = Path.GetTempFileName();
        Assert.ThrowsException<CorruptedFileHeaderException>(() =>
        {
            byte[] bytes = Enumerable.Range(0, 255).Select(o => (byte)o).ToArray();
            File.WriteAllBytes(temp, bytes);
            using FwobFile<Tick, int> file = new(temp);
        });
        File.Delete(temp);

        Assert.ThrowsException<CorruptedFileLengthException>(() =>
        {
            using (FwobFile<Tick, int> file = new(temp, "TestTick"))
            {
                file.AppendFrames(tick12a, tick12b, tick12c, tick13, tick100);
            }
            File.AppendAllText(temp, "A");
            using FwobFile<Tick, int> file2 = new(temp);
        });
        File.Delete(temp);

        Assert.ThrowsException<CorruptedFileLengthException>(() =>
        {
            using (FwobFile<Tick, int> file = new(temp, "TestTick"))
            {
                file.AppendFrames(tick12a, tick12b, tick12c, tick13, tick100);
            }
            using (FileStream file2 = File.Open(temp, FileMode.Open, FileAccess.ReadWrite))
            {
                file2.Seek(170, SeekOrigin.Begin);
                file2.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
            }
            using FwobFile<Tick, int> file3 = new(temp);
        });
        File.Delete(temp);

        Assert.ThrowsException<CorruptedStringTableLengthException>(() =>
        {
            long stPos;
            using (FwobFile<Tick, int> file = new(temp, "TestTick"))
            {
                file.AppendFrames(tick12a, tick12b, tick12c, tick13, tick100);
                file.AppendString("asdf");
                file.AppendString("asdf");
                file.AppendString("asdf2");
                file.AppendString("asdf3");
                stPos = file.Header.StringTablePosition;
            }
            using (FileStream file2 = File.Open(temp, FileMode.Open, FileAccess.ReadWrite))
            {
                file2.Seek(stPos, SeekOrigin.Begin);
                file2.Write(new byte[] { 5 });
            }
            using FwobFile<Tick, int> file3 = new(temp);
            file3.LoadStringTable();
        });
        File.Delete(temp);
    }

    #region Header testing
    [TestMethod]
    public void TestTitle()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        // Opening an existing file
        Assert.AreEqual("HelloFwob", file.Title);

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
        Assert.ThrowsException<ArgumentException>(() => file.Title = "");
        Assert.ThrowsException<TitleTooLongException>(() => file.Title = "0123456789abcdefg");
        Assert.AreEqual(file.Title, "0123456789abcdef");

        // Creating a new file
        string tmpfile = Path.GetTempFileName();
        Assert.ThrowsException<ArgumentNullException>(() => new FwobFile<Tick, int>(tmpfile, null));
        Assert.ThrowsException<ArgumentException>(() => new FwobFile<Tick, int>(tmpfile, ""));
        Assert.ThrowsException<ArgumentException>(() => new FwobFile<Tick, int>(tmpfile, "  "));
        Assert.ThrowsException<TitleTooLongException>(() => new FwobFile<Tick, int>(tmpfile, " 0123456789abcdefg "));
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpfile, " 0123456789abcdef ");
        Assert.AreEqual("0123456789abcdef", file.Title);
        file.Dispose();
        File.Delete(tmpfile);
    }
    #endregion

    #region StringTable testing

    [TestMethod]
    public void TestWritingBasicFile()
    {
        Assert.IsNotNull(file);

        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.AreEqual(0, file.StringCount);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual("Tick", file.FrameInfo.FrameType);
        Assert.AreEqual(16, file.FrameInfo.FrameLength);
        Assert.AreEqual(0x320ul, file.FrameInfo.FieldTypes);
        Assert.AreEqual(3, file.FrameInfo.Fields.Count);
        Assert.AreEqual(4, file.FrameInfo.Fields[0].FieldLength);
        Assert.AreEqual(8, file.FrameInfo.Fields[1].FieldLength);
        Assert.AreEqual(4, file.FrameInfo.Fields[2].FieldLength);
        Assert.AreEqual("Time", file.FrameInfo.Fields[0].FieldName);
        Assert.AreEqual("Value", file.FrameInfo.Fields[1].FieldName);
        Assert.AreEqual("Str", file.FrameInfo.Fields[2].FieldName);
        Assert.AreEqual(FieldType.SignedInteger, file.FrameInfo.Fields[0].FieldType);
        Assert.AreEqual(FieldType.FloatingPoint, file.FrameInfo.Fields[1].FieldType);
        Assert.AreEqual(FieldType.Utf8String, file.FrameInfo.Fields[2].FieldType);
        file.LoadStringTable();
        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(0, file.Strings.Count);
        Assert.AreEqual("HelloFwob", file.Title);
    }

    [TestMethod]
    public void TestReadingBasicFile()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        TestWritingBasicFile();
    }

    [TestMethod]
    public void TestWritingStringTable()
    {
        Assert.IsNotNull(file);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
        Assert.AreEqual(-1, file.GetIndex("mystr"));
        Assert.AreEqual(0, file.StringCount);
        Assert.AreEqual(0, file.AppendString("mystr"));
        Assert.AreEqual(1, file.StringCount);
        Assert.AreEqual(1, file.AppendString("hello2"));
        Assert.AreEqual(2, file.StringCount);
        Assert.AreEqual(2, file.AppendString("test_string3"));
        Assert.AreEqual(3, file.StringCount);
    }

    private void ValidateStringTableData()
    {
        Assert.IsNotNull(file);

        Assert.ThrowsException<ArgumentNullException>(() => file.ContainsString(null));
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
        Assert.AreEqual("mystr", file.GetString(0));
        Assert.AreEqual("hello2", file.GetString(1));
        Assert.AreEqual("test_string3", file.GetString(2));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(100));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(3));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(-1));

        Assert.AreEqual(0, file.GetIndex("mystr"));
        Assert.AreEqual(1, file.GetIndex("hello2"));
        Assert.AreEqual(2, file.GetIndex("test_string3"));
        Assert.ThrowsException<ArgumentNullException>(() => file.GetIndex(null));
        Assert.AreEqual(-1, file.GetIndex(""));
        Assert.AreEqual(-1, file.GetIndex("mystr2"));
        Assert.AreEqual(-1, file.GetIndex("Test_string3"));
        Assert.AreEqual(-1, file.GetIndex("TEST_STRING3"));
    }

    [TestMethod]
    public void TestClearingStringTable()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        file.DeleteAllStrings();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
        Assert.AreEqual(-1, file.GetIndex("mystr"));
        Assert.AreEqual(0, file.StringCount);

        TestWritingStringTable();
        file.DeleteAllStrings();
        Assert.AreEqual(new FileInfo(_tempPath).Length, FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
        Assert.AreEqual(-1, file.GetIndex("mystr"));
        Assert.AreEqual(0, file.StringCount);

        // adding after clearing
        Assert.AreEqual(0, file.AppendString("hello2"));
        Assert.AreEqual(1, file.StringCount);
        Assert.AreEqual("hello2", file.GetString(0));
        Assert.AreEqual(-1, file.GetIndex("mystr"));
        Assert.AreEqual(0, file.GetIndex("hello2"));
        Assert.AreEqual(1, file.StringCount);
    }

    [TestMethod]
    public void TestReadingStringTable()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

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
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        file.LoadStringTable();

        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(0, file.Strings.Count);
        TestWritingStringTable();
        ValidateStringTableData();

        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        file.LoadStringTable();

        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(3, file.Strings.Count);
        ValidateStringTableData();
        TestClearingStringTable();
        Assert.AreEqual(1, file.Strings.Count);
    }

    [TestMethod]
    public void TestCachingStringTable()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        file.LoadStringTable();
        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(0, file.Strings.Count);

        TestWritingStringTable();
        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(3, file.Strings.Count);
        Assert.AreEqual(file.Strings[0], "mystr");
        Assert.AreEqual(file.Strings[1], "hello2");
        Assert.AreEqual(file.Strings[2], "test_string3");

        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);

        file.LoadStringTable();
        file.LoadStringTable();
        file.LoadStringTable();
        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(3, file.Strings.Count);
        Assert.AreEqual(file.Strings[0], "mystr");
        Assert.AreEqual(file.Strings[1], "hello2");
        Assert.AreEqual(file.Strings[2], "test_string3");
        TestClearingStringTable();
        Assert.AreEqual(1, file.Strings.Count);
        Assert.AreEqual(file.Strings[0], "hello2");
        file.LoadStringTable();
        file.UnloadStringTable();
    }

    [TestMethod]
    public void TestStringTableLimit()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        Assert.ThrowsException<ArgumentNullException>(() => file.AppendString(null));

        for (int i = 0; i < 262; i++) // (2048 - 214) / 7 = 262
            Assert.AreEqual(i, file.AppendString($"abc{i:d3}"));
        Assert.ThrowsException<StringTableOutOfSpaceException>(() => file.AppendString("x"));

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
        Assert.ThrowsException<StringTableOutOfSpaceException>(() => file.AppendString(""));
    }

    #endregion

    #region Frame data testing
    private void ValidateNoFrame()
    {
        Assert.IsNotNull(file);

        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.AreEqual(0, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetKeyAt(-1));
        Assert.IsNull(file.GetKeyAt(0));
        Assert.IsNull(file.GetKeyAt(1));

        Assert.IsNull(file[-1]);
        Assert.IsNull(file[0]);
        Assert.IsNull(file[1]);

        Assert.IsNull(file.GetFrameAt(0));
        Assert.IsNull(file.GetFrameAt(-1));
        Assert.IsNull(file.GetFrameAt(1));

        Assert.IsFalse(file.GetFrames(0).Any());
        Assert.IsFalse(file.GetFrames(0, 1000).Any());

        Assert.ThrowsException<ArgumentException>(() => file.GetFramesBetween(12, 0).Any());
        Assert.IsFalse(file.GetFramesBetween(0, 12).Any());

        Assert.IsFalse(file.GetFramesBefore(-1000).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());
        Assert.IsFalse(file.GetFramesBefore(1000).Any());

        Assert.IsFalse(file.GetFramesAfter(-1000).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
        Assert.IsFalse(file.GetFramesAfter(1000).Any());

        Assert.IsFalse(file.GetAllFrames().Any());
    }

    [TestMethod]
    public void TestNoFrame()
    {
        ValidateNoFrame();
    }

    private void AddOneFrame()
    {
        Assert.IsNotNull(file);

        Assert.ThrowsException<ArgumentNullException>(() => file.AppendFrames(null));
        Assert.ThrowsException<ArgumentNullException>(() => file.AppendFrames((IEnumerable<Tick>)null));
        Assert.AreEqual(0, file.AppendFrames());
        Assert.AreEqual(1, file.AppendFrames(tick12a));
    }

    private void ValidateOneFrame()
    {
        Assert.IsNotNull(file);

        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick12a, file.LastFrame);
        Assert.AreEqual(1, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetKeyAt(-1));
        Assert.AreEqual(12, file.GetKeyAt(0));
        Assert.IsNull(file.GetKeyAt(1));

        Assert.IsNull(file[-1]);
        Assert.AreEqual(tick12a, file[0]);
        Assert.IsNull(file[1]);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick12a);
        Assert.IsNull(file.GetFrameAt(1));
        Assert.IsNull(file.GetFrameAt(12));

        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(1, file.GetFrames(12).Count());
        Assert.AreEqual(1, file.GetFrames(12, 1000).Count());
        Assert.AreEqual(0, file.GetFrames(-1, 1000).Count());

        Assert.AreEqual(1, file.GetFramesBetween(-1, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(11, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(12, 20).Count());
        Assert.AreEqual(0, file.GetFramesBetween(13, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 13).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 12).Count());
        Assert.AreEqual(0, file.GetFramesBetween(-1, 11).Count());

        Assert.AreEqual(0, file.GetFramesBefore(0).Count());
        Assert.AreEqual(0, file.GetFramesBefore(11).Count());
        Assert.AreEqual(1, file.GetFramesBefore(12).Count());
        Assert.AreEqual(1, file.GetFramesBefore(13).Count());

        Assert.AreEqual(1, file.GetFramesAfter(-1).Count());
        Assert.AreEqual(1, file.GetFramesAfter(11).Count());
        Assert.AreEqual(1, file.GetFramesAfter(12).Count());
        Assert.AreEqual(0, file.GetFramesAfter(13).Count());

        Assert.AreEqual(1, file.GetAllFrames().Count());
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
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        Assert.AreEqual(new FileInfo(_tempPath).Length, FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength);
        file.DeleteAllFrames();
        ValidateNoFrame();
        file.Dispose();
        Assert.AreEqual(new FileInfo(_tempPath).Length, FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength);
        file = new FwobFile<Tick, int>(_tempPath);
        ValidateNoFrame();
    }

    [TestMethod]
    public void TestReadingOneFrame()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        ValidateNoFrame();
        AddOneFrame();
        ValidateOneFrame();
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        ValidateOneFrame();
        file.DeleteAllFrames();
        ValidateNoFrame();
    }

    [TestMethod]
    public void TestFramesSameKey()
    {
        AddFramesSameKey();
        ValidateFramesSameKey();
    }

    private void AddFramesSameKey()
    {
        Assert.IsNotNull(file);

        Assert.AreEqual(3, file.AppendFrames(tick12a, tick12b, tick12c));
    }

    private void ValidateFramesSameKey()
    {
        Assert.IsNotNull(file);

        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick12c);
        Assert.AreEqual(3, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick12a);
        Assert.AreEqual(file.GetFrameAt(1), tick12b);
        Assert.AreEqual(file.GetFrameAt(2), tick12c);
        Assert.IsNull(file.GetFrameAt(3));
        Assert.IsNull(file.GetFrameAt(12));
        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(3, file.GetFrames(12).Count());
        Assert.AreEqual(3, file.GetFrames(-1, 12, 2000).Count());

        Assert.AreEqual(3, file.GetFramesBetween(-1, 20).Count());
        Assert.AreEqual(3, file.GetFramesBetween(11, 20).Count());
        Assert.AreEqual(3, file.GetFramesBetween(12, 20).Count());
        Assert.AreEqual(0, file.GetFramesBetween(13, 20).Count());
        Assert.AreEqual(3, file.GetFramesBetween(-1, 13).Count());
        Assert.AreEqual(3, file.GetFramesBetween(-1, 12).Count());
        Assert.AreEqual(0, file.GetFramesBetween(-1, 11).Count());

        Assert.AreEqual(0, file.GetFramesBefore(0).Count());
        Assert.AreEqual(0, file.GetFramesBefore(11).Count());
        Assert.AreEqual(3, file.GetFramesBefore(12).Count());
        Assert.AreEqual(3, file.GetFramesBefore(13).Count());

        Assert.AreEqual(3, file.GetFramesAfter(-1).Count());
        Assert.AreEqual(3, file.GetFramesAfter(11).Count());
        Assert.AreEqual(3, file.GetFramesAfter(12).Count());
        Assert.AreEqual(0, file.GetFramesAfter(13).Count());
    }

    [TestMethod]
    public void TestReadingFramesSameKey()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        ValidateNoFrame();
        AddFramesSameKey();
        ValidateFramesSameKey();
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        ValidateFramesSameKey();
        file.DeleteAllFrames();
        ValidateNoFrame();
    }

    private void AddFramesMultiKeys()
    {
        Assert.IsNotNull(file);

        Assert.AreEqual(3, file.AppendFrames(tick12a, tick13, tick100));
    }

    private void ValidateFramesMultiKeys()
    {
        Assert.IsNotNull(file);

        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);
        Assert.AreEqual(3, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick12a);
        Assert.AreEqual(file.GetFrameAt(1), tick13);
        Assert.AreEqual(file.GetFrameAt(2), tick100);
        Assert.IsNull(file.GetFrameAt(3));
        Assert.IsNull(file.GetFrameAt(12));
        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(1, file.GetFrames(12).Count());
        Assert.AreEqual(2, file.GetFramesBetween(-1, 20).Count());
        Assert.AreEqual(2, file.GetFramesBetween(11, 20).Count());
        Assert.AreEqual(2, file.GetFramesBetween(12, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(13, 20).Count());
        Assert.AreEqual(0, file.GetFramesBetween(14, 20).Count());
        Assert.AreEqual(2, file.GetFramesBetween(-1, 14).Count());
        Assert.AreEqual(2, file.GetFramesBetween(-1, 13).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 12).Count());
        Assert.AreEqual(0, file.GetFramesBetween(-1, 11).Count());
        Assert.AreEqual(3, file.GetFramesAfter(-1).Count());
        Assert.AreEqual(3, file.GetFramesAfter(11).Count());
        Assert.AreEqual(3, file.GetFramesAfter(12).Count());
        Assert.AreEqual(2, file.GetFramesAfter(13).Count());
        Assert.AreEqual(1, file.GetFramesAfter(14).Count());
        Assert.AreEqual(1, file.GetFramesAfter(100).Count());
        Assert.AreEqual(0, file.GetFramesAfter(101).Count());
        Assert.AreEqual(0, file.GetFramesBefore(0).Count());
        Assert.AreEqual(0, file.GetFramesBefore(11).Count());
        Assert.AreEqual(1, file.GetFramesBefore(12).Count());
        Assert.AreEqual(2, file.GetFramesBefore(13).Count());
        Assert.AreEqual(2, file.GetFramesBefore(14).Count());
        Assert.AreEqual(2, file.GetFramesBefore(99).Count());
        Assert.AreEqual(3, file.GetFramesBefore(100).Count());
        Assert.AreEqual(3, file.GetFramesBefore(101).Count());
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
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        ValidateNoFrame();
        AddFramesMultiKeys();
        ValidateFramesMultiKeys();
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        ValidateFramesMultiKeys();
        file.DeleteAllFrames();
        ValidateNoFrame();
    }

    private void AddFramesPartially()
    {
        Assert.IsNotNull(file);

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFrames(tick12c, tick100, tick13));
    }

    private void ValidateFramesPartially()
    {
        Assert.IsNotNull(file);

        Assert.AreEqual(file.FirstFrame, tick12c);
        Assert.AreEqual(file.LastFrame, tick100);
        Assert.AreEqual(2, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick12c);
        Assert.AreEqual(file.GetFrameAt(1), tick100);
        Assert.IsNull(file.GetFrameAt(2));
        Assert.IsNull(file.GetFrameAt(12));
        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(1, file.GetFrames(12).Count());
        Assert.ThrowsException<ArgumentException>(() => file.GetFramesBetween(20, -1).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(11, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(12, 20).Count());
        Assert.AreEqual(0, file.GetFramesBetween(13, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(14, 100).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 13).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 12).Count());
        Assert.AreEqual(0, file.GetFramesBetween(-1, 11).Count());
        Assert.AreEqual(2, file.GetFramesAfter(-1).Count());
        Assert.AreEqual(2, file.GetFramesAfter(11).Count());
        Assert.AreEqual(2, file.GetFramesAfter(12).Count());
        Assert.AreEqual(1, file.GetFramesAfter(13).Count());
        Assert.AreEqual(1, file.GetFramesAfter(100).Count());
        Assert.AreEqual(0, file.GetFramesAfter(101).Count());
        Assert.AreEqual(0, file.GetFramesBefore(0).Count());
        Assert.AreEqual(0, file.GetFramesBefore(11).Count());
        Assert.AreEqual(1, file.GetFramesBefore(12).Count());
        Assert.AreEqual(1, file.GetFramesBefore(13).Count());
        Assert.AreEqual(1, file.GetFramesBefore(99).Count());
        Assert.AreEqual(2, file.GetFramesBefore(100).Count());
        Assert.AreEqual(2, file.GetFramesBefore(101).Count());

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFrames(tick13));
        Assert.AreEqual(file.FirstFrame, tick12c);
        Assert.AreEqual(file.LastFrame, tick100);
        Assert.AreEqual(2, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick12c);
        Assert.AreEqual(file.GetFrameAt(1), tick100);
        Assert.IsNull(file.GetFrameAt(2));
        Assert.IsNull(file.GetFrameAt(12));
        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(1, file.GetFrames(12).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(11, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(12, 20).Count());
        Assert.AreEqual(0, file.GetFramesBetween(13, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(14, 100).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 13).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 12).Count());
        Assert.AreEqual(0, file.GetFramesBetween(-1, 11).Count());
        Assert.AreEqual(2, file.GetFramesAfter(-1).Count());
        Assert.AreEqual(2, file.GetFramesAfter(11).Count());
        Assert.AreEqual(2, file.GetFramesAfter(12).Count());
        Assert.AreEqual(1, file.GetFramesAfter(13).Count());
        Assert.AreEqual(1, file.GetFramesAfter(100).Count());
        Assert.AreEqual(0, file.GetFramesAfter(101).Count());
        Assert.AreEqual(0, file.GetFramesBefore(0).Count());
        Assert.AreEqual(0, file.GetFramesBefore(11).Count());
        Assert.AreEqual(1, file.GetFramesBefore(12).Count());
        Assert.AreEqual(1, file.GetFramesBefore(13).Count());
        Assert.AreEqual(1, file.GetFramesBefore(99).Count());
        Assert.AreEqual(2, file.GetFramesBefore(100).Count());
        Assert.AreEqual(2, file.GetFramesBefore(101).Count());
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
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        var t = new Tick { Time = 987, Value = 123.456, Str = null };
        Assert.AreEqual(1, file.AppendFrames(t));
        Assert.AreEqual(file.GetFrameAt(0), t);
        Assert.AreEqual(file.LastFrame, t);
        Assert.AreEqual(1, file.FrameCount);
        t.Str = "";
        Assert.AreEqual(1, file.AppendFrames(t));
        Assert.AreEqual(file.GetFrameAt(1), t);
        Assert.AreEqual(file.LastFrame, t);
        Assert.AreEqual(2, file.FrameCount);
        t.Str = "a";
        Assert.AreEqual(1, file.AppendFrames(t));
        Assert.AreEqual(file.GetFrameAt(2), t);
        Assert.AreEqual(file.LastFrame, t);
        Assert.AreEqual(3, file.FrameCount);
        t.Str = "abcd";
        Assert.AreEqual(1, file.AppendFrames(t));
        Assert.AreEqual(file.GetFrameAt(3), t);
        Assert.AreEqual(file.LastFrame, t);
        Assert.AreEqual(4, file.FrameCount);
        t.Str = "abcde";
        Assert.ThrowsException<StringTooLongException>(() => file.AppendFrames(t));
        Assert.IsNull(file.GetFrameAt(4));
        t.Str = "abcd";
        Assert.AreEqual(file.LastFrame, t);
        Assert.AreEqual(4, file.FrameCount);
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFrames(tick12a));
        Assert.AreEqual(4, file.FrameCount);

        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);

        Assert.AreEqual(4, file.FrameCount);
        t.Str = null;
        Assert.AreEqual(file.GetFrameAt(0), t);
        t.Str = "";
        Assert.AreEqual(file.GetFrameAt(1), t);
        t.Str = "a";
        Assert.AreEqual(file.GetFrameAt(2), t);
        t.Str = "abcd";
        Assert.AreEqual(file.GetFrameAt(3), t);
    }

    [TestMethod]
    public void TestReadingFramesPartially()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        ValidateNoFrame();
        AddFramesPartially();
        ValidateFramesPartially();
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        ValidateFramesPartially();
        file.DeleteAllFrames();
        ValidateNoFrame();
    }

    [TestMethod]
    public void TestFramesTransactional()
    {
        Assert.IsNotNull(file);

        Assert.ThrowsException<ArgumentNullException>(() => file.AppendFramesTx(null));
        Assert.ThrowsException<ArgumentNullException>(() => file.AppendFramesTx((IEnumerable<Tick>)null));
        Assert.AreEqual(0, file.AppendFramesTx());

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12c, tick100, tick13));
        ValidateNoFrame();
        AddOneFrame();
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick100, tick13));
        ValidateOneFrame();
        Assert.AreEqual(1, file.AppendFrames(tick100));
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);
        Assert.IsNotNull(file.FrameInfo);

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12b));
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12c));
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick13));
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);
        Assert.IsNotNull(file.FrameInfo);
    }

    [TestMethod]
    public void TestReadingFramesTransactional()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12c, tick100, tick13));
        ValidateNoFrame();
        AddOneFrame();
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick100, tick13));
        ValidateOneFrame();
        Assert.AreEqual(1, file.AppendFrames(tick100));
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);
        Assert.IsNotNull(file.FrameInfo);

        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12b));
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12c));
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick13));
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);
        Assert.IsNotNull(file.FrameInfo);
    }
    #endregion

    #region Mixed and performance testing
    [TestMethod]
    public void TestMixingStringTableAndFrames()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        TestWritingStringTable();
        AddOneFrame();
        ValidateStringTableData();
        ValidateOneFrame();
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        ValidateOneFrame();
        ValidateStringTableData();

        // duplicate
        Assert.AreEqual(3, file.AppendString("mystr"));
        Assert.AreEqual(4, file.StringCount);
        Assert.AreEqual(4, file.AppendString("hello2"));
        Assert.AreEqual(5, file.StringCount);
        Assert.AreEqual(5, file.AppendString("test_string3"));
        Assert.AreEqual(6, file.StringCount);
    }

    [TestMethod]
    public void TestStringTableAndFramesPerformance()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        Assert.ThrowsException<ArgumentNullException>(() => file.AppendString(null));

        for (int i = 0; i < 262; i++) // (2048 - 214) / 7 = 262
        {
            Assert.AreEqual(i, file.AppendString($"abc{i:d3}"));
            Assert.IsTrue(file.AppendFrames(
                new Tick { Time = i, Value = i * 3.14 },
                new Tick { Time = i, Value = i * 0.61 },
                new Tick { Time = i, Value = i * 6.22 },
                new Tick { Time = i, Value = i * 2.71 },
                new Tick { Time = i, Value = i * 9.99 }) == 5);
            Assert.AreEqual(5 * i + 5, file.FrameCount);
            Assert.AreEqual(file.FirstFrame, new Tick { Time = 0, Value = 0 });
            Assert.AreEqual(file.LastFrame, new Tick { Time = i, Value = i * 9.99 });
        }

        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.ThrowsException<StringTableOutOfSpaceException>(() => file.AppendString("x"));
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
            Assert.AreEqual(5 * 262 + 6 * i + 6, file.FrameCount);
            Assert.AreEqual(file.FirstFrame, new Tick { Time = 0, Value = 0 });
            Assert.AreEqual(file.LastFrame, new Tick { Time = 1000 + i, Value = -i * 8.11 });
        }

        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.ThrowsException<StringTableOutOfSpaceException>(() => file.AppendString("x"));
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
            Assert.AreEqual(11 * 262 + 7 * i + 7, file.FrameCount);
            Assert.AreEqual(file.FirstFrame, new Tick { Time = 0, Value = 0 });
            Assert.AreEqual(file.LastFrame, new Tick { Time = 2000 + i, Value = i * 1.56 });
        }

        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.ThrowsException<StringTableOutOfSpaceException>(() => file.AppendString("x"));
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
            Assert.AreEqual(file.GetFramesBetween(i, 1000 + i).Count(), 262 * 5 + i + 6);
            Assert.AreEqual(file.GetFramesBetween(1000 + i, 2000 + i).Count(), 262 * 6 + i + 7);
            Assert.AreEqual(file.GetFrameAt(i * 5), new Tick { Time = i, Value = i * 3.14 });
            Assert.AreEqual(file.GetFrameAt(262 * 5 + i * 6 + 1), new Tick { Time = 1000 + i, Value = -i * 0.61 });
            Assert.AreEqual(file.GetFrameAt(262 * 11 + i * 7 + 2), new Tick { Time = 2000 + i, Value = i * 6.22 });
            Assert.AreEqual(file.GetString(i), $"abc{i:d3}");
        }

        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.ThrowsException<StringTableOutOfSpaceException>(() => file.AppendString(""));
        Assert.AreEqual(file.FrameCount, 18 * 262);
    }
    #endregion

    #region Organizers
    [TestMethod]
    public void TestFileSplit()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);
        string filename = Path.GetFileNameWithoutExtension(_tempPath);
        string outdir = Path.GetDirectoryName(_tempPath)!;

        // Test parameter validators
        file.Dispose();
        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Split(null, null));
        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Split(null, outdir));
        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Split(_tempPath, null));
        Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Split(_tempPath, outdir));
        Assert.ThrowsException<FrameNotFoundException>(() => FwobFile<Tick, int>.Split(_tempPath, outdir, 1, 2));

        file = new FwobFile<Tick, int>(_tempPath);

        for (int i = 0; i < 485; i++)
            Assert.AreEqual(i, file.AppendString(i.ToString()));

        Assert.AreEqual(10000, file.AppendFrames(
                Enumerable.Range(0, 10000)
                    .Select(i => new Tick { Time = i, Value = i * 3.14, Str = i.ToString() })));

        Assert.AreEqual(10000, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, new Tick { Time = 0, Value = 0, Str = "0" });
        Assert.AreEqual(file.LastFrame, new Tick { Time = 9999, Value = 9999 * 3.14, Str = "9999" });
        file.Dispose();

        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Split(_tempPath, outdir, null));
        Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Split(_tempPath, outdir, 1, 0));
        Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Split(_tempPath, outdir, 1, 1));
        Assert.ThrowsException<FileNotFoundException>(() => FwobFile<Tick, int>.Split(_tempPath + ".nonexistence", outdir, 1, 2));

        // Test the number of parts
        string tmpdirname = Path.GetTempFileName();
        File.Delete(tmpdirname);

        foreach (int k in new[] { -10000, -1, 0, 1000, 9999, 10000, 20000 })
        {
            Directory.CreateDirectory(tmpdirname);
            FwobFile<Tick, int>.Split(_tempPath, tmpdirname, new[] { k }, ignoreEmptyParts: false);
            Assert.AreEqual(2, Directory.GetFiles(tmpdirname).Length);
            Directory.Delete(tmpdirname, true);
        }

        foreach (int k in new[] { -10000, -1, 0, 1000, 9999, 10000, 20000 })
        {
            Directory.CreateDirectory(tmpdirname);
            FwobFile<Tick, int>.Split(_tempPath, tmpdirname, new[] { k, k + 1000 }, ignoreEmptyParts: false);
            Assert.AreEqual(3, Directory.GetFiles(tmpdirname).Length);
            Directory.Delete(tmpdirname, true);
        }

        foreach (int k in new[] { -10000, -1, 0, 10000, 20000 })
        {
            Directory.CreateDirectory(tmpdirname);
            FwobFile<Tick, int>.Split(_tempPath, tmpdirname, new[] { k }, ignoreEmptyParts: true);
            string[] names = Directory.GetFiles(tmpdirname);
            Assert.AreEqual(1, names.Length);
            Assert.AreEqual(Convert.ToBase64String(File.ReadAllBytes(_tempPath)), Convert.ToBase64String(File.ReadAllBytes(names[0])));
            Directory.Delete(tmpdirname, true);
        }

        foreach (int k in new[] { -10000, 10000, 20000 })
        {
            Directory.CreateDirectory(tmpdirname);
            FwobFile<Tick, int>.Split(_tempPath, tmpdirname, new[] { k, k + 1000 }, ignoreEmptyParts: true);
            string[] names = Directory.GetFiles(tmpdirname);
            Assert.AreEqual(1, names.Length);
            Assert.AreEqual(Convert.ToBase64String(File.ReadAllBytes(_tempPath)), Convert.ToBase64String(File.ReadAllBytes(names[0])));
            Directory.Delete(tmpdirname, true);
        }

        foreach (int k in new[] { 1000, 9999 })
        {
            Directory.CreateDirectory(tmpdirname);
            FwobFile<Tick, int>.Split(_tempPath, tmpdirname, new[] { k }, ignoreEmptyParts: true);
            Assert.AreEqual(2, Directory.GetFiles(tmpdirname).Length);
            Directory.Delete(tmpdirname, true);
        }

        // Test splitting
        int baseLength = FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength;

        FwobFile<Tick, int>.Split(_tempPath, outdir, 100);
        string path0 = $"{_tempPath}.part0.fwob";
        string path1 = $"{_tempPath}.part1.fwob";
        Assert.IsTrue(File.Exists(path0));
        Assert.IsTrue(File.Exists(path1));
        Assert.AreEqual(baseLength + 100 * 16, new FileInfo(path0).Length);
        Assert.AreEqual(baseLength + 9900 * 16, new FileInfo(path1).Length);

        using (var ff = new FwobFile<Tick, int>(path0))
        {
            Assert.AreEqual(100, ff.FrameCount);
            Assert.AreEqual(ff.FirstFrame, new Tick { Time = 0, Value = 0, Str = "0" });
            Assert.AreEqual(ff.LastFrame, new Tick { Time = 99, Value = 99 * 3.14, Str = "99" });
            ff.LoadStringTable();
            Assert.IsNotNull(ff.Strings);
            Assert.AreEqual(485, ff.Strings.Count);
            for (int i = 0; i < 485; i++)
                Assert.AreEqual(i.ToString(), ff.Strings[i]);
        }
        using (var ff = new FwobFile<Tick, int>(path1))
        {
            Assert.AreEqual(9900, ff.FrameCount);
            Assert.AreEqual(ff.FirstFrame, new Tick { Time = 100, Value = 100 * 3.14, Str = "100" });
            Assert.AreEqual(ff.LastFrame, new Tick { Time = 9999, Value = 9999 * 3.14, Str = "9999" });
            ff.LoadStringTable();
            Assert.IsNotNull(ff.Strings);
            Assert.AreEqual(485, ff.Strings.Count);
            for (int i = 0; i < 485; i++)
                Assert.AreEqual(i.ToString(), ff.Strings[i]);
        }

        File.Delete(path0);
        File.Delete(path1);

        outdir = Path.Combine(outdir, "NewSubDir");
        File.Move(_tempPath, _tempPath + ".fwob");
        path0 = Path.Combine(outdir, $"{Path.GetFileName(_tempPath)}.part0.fwob");
        path1 = Path.Combine(outdir, $"{Path.GetFileName(_tempPath)}.part1.fwob");
        string path2 = Path.Combine(outdir, $"{Path.GetFileName(_tempPath)}.part2.fwob");

        FwobFile<Tick, int>.Split(_tempPath + ".fwob", outdir, 1, 9999);
        Assert.IsTrue(File.Exists(path0));
        Assert.IsTrue(File.Exists(path1));
        Assert.IsTrue(File.Exists(path2));
        Assert.AreEqual(baseLength + 1 * 16, new FileInfo(path0).Length);
        Assert.AreEqual(baseLength + 9998 * 16, new FileInfo(path1).Length);
        Assert.AreEqual(baseLength + 1 * 16, new FileInfo(path2).Length);
        _tempPath += ".fwob";

        using (var ff = new FwobFile<Tick, int>(path0))
        {
            ff.LoadStringTable();
            Assert.IsNotNull(ff.Strings);
            Assert.AreEqual(485, ff.Strings.Count);
            for (int i = 0; i < 485; i++)
                Assert.AreEqual(i.ToString(), ff.Strings[i]);
            Assert.AreEqual(1, ff.FrameCount);
            Assert.AreEqual(ff.FirstFrame, new Tick { Time = 0, Value = 0, Str = "0" });
            Assert.AreEqual(ff.LastFrame, new Tick { Time = 0, Value = 0, Str = "0" });
        }
        using (var ff = new FwobFile<Tick, int>(path1))
        {
            ff.LoadStringTable();
            Assert.IsNotNull(ff.Strings);
            Assert.AreEqual(485, ff.Strings.Count);
            for (int i = 0; i < 485; i++)
                Assert.AreEqual(i.ToString(), ff.Strings[i]);
            Assert.AreEqual(9998, ff.FrameCount);
            Assert.AreEqual(ff.FirstFrame, new Tick { Time = 1, Value = 3.14, Str = "1" });
            Assert.AreEqual(ff.LastFrame, new Tick { Time = 9998, Value = 9998 * 3.14, Str = "9998" });
        }
        using (var ff = new FwobFile<Tick, int>(path2))
        {
            ff.LoadStringTable();
            Assert.IsNotNull(ff.Strings);
            Assert.AreEqual(485, ff.Strings.Count);
            for (int i = 0; i < 485; i++)
                Assert.AreEqual(i.ToString(), ff.Strings[i]);
            Assert.AreEqual(1, ff.FrameCount);
            Assert.AreEqual(ff.FirstFrame, new Tick { Time = 9999, Value = 9999 * 3.14, Str = "9999" });
            Assert.AreEqual(ff.LastFrame, new Tick { Time = 9999, Value = 9999 * 3.14, Str = "9999" });
        }

        File.Delete(path0);
        File.Delete(path1);
        File.Delete(path2);
        Directory.Delete(outdir, true);
    }

    [TestMethod]
    public void TestFileConcat()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        file.Dispose();
        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Concat(null));
        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Concat(_tempPath, null));
        Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Concat(_tempPath));
        Assert.ThrowsException<FileNotFoundException>(() => FwobFile<Tick, int>.Concat(_tempPath, _tempPath + ".nonexistence"));

        string path0 = _tempPath + ".part0.fwob";
        string path1 = _tempPath + ".part1.fwob";
        string path2 = _tempPath + ".part2.fwob";
        Assert.ThrowsException<FileNotFoundException>(() => FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2));

        // test file title consistence
        var file0 = new FwobFile<Tick, int>(path0, "test0"); file0.Dispose();
        var file1 = new FwobFile<Tick, int>(path1, "test"); file1.Dispose();
        var file2 = new FwobFile<Tick, int>(path2, "test"); file2.Dispose();

        Assert.ThrowsException<TitleIncompatibleException>(() => FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2));

        file0 = new FwobFile<Tick, int>(path0, "test", FileMode.Create);
        file1 = new FwobFile<Tick, int>(path1, "test", FileMode.Create);
        file2 = new FwobFile<Tick, int>(path2, "test2", FileMode.Create);

        file0.AppendFrames(tick12a); file0.Dispose();
        file1.AppendFrames(tick12a); file1.Dispose();
        file2.AppendFrames(tick12a); file2.Dispose();

        Assert.ThrowsException<TitleIncompatibleException>(() => FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2));

        // test frame ordering enforcement
        file0 = new FwobFile<Tick, int>(path0, "test", FileMode.Create);
        file1 = new FwobFile<Tick, int>(path1, "test", FileMode.Create);
        file2 = new FwobFile<Tick, int>(path2, "test", FileMode.Create);

        file0.AppendFrames(tick100); file0.Dispose();
        file1.AppendFrames(tick13); file1.Dispose();
        file2.AppendFrames(tick12c); file2.Dispose();

        Assert.ThrowsException<KeyOrderViolationException>(() => FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2));

        // test blank file
        file0 = new FwobFile<Tick, int>(path0, "test", FileMode.Create);
        file1 = new FwobFile<Tick, int>(path1, "test", FileMode.Create);
        file2 = new FwobFile<Tick, int>(path2, "test", FileMode.Create);

        file0.AppendFrames(tick12c); file0.Dispose();
        file1.Dispose();
        file2.AppendFrames(tick100); file2.Dispose();

        FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2);

        // test empty string table
        file0 = new FwobFile<Tick, int>(path0, "test", FileMode.Create);
        file1 = new FwobFile<Tick, int>(path1, "test", FileMode.Create);
        file2 = new FwobFile<Tick, int>(path2, "test", FileMode.Create);

        file0.AppendFrames(tick12c); file0.Dispose();
        file1.AppendFrames(tick13); file1.Dispose();
        file2.AppendFrames(tick100); file2.Dispose();

        FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2);

        // test string table exception
        file0 = new FwobFile<Tick, int>(path0, "test", FileMode.Create);
        file1 = new FwobFile<Tick, int>(path1, "test", FileMode.Create);
        file2 = new FwobFile<Tick, int>(path2, "test", FileMode.Create);

        file0.AppendString("str0"); file0.AppendFrames(tick12b); file0.Dispose();
        file1.AppendString("str0"); file1.AppendString("str1"); file1.AppendFrames(tick12c); file1.Dispose();
        file2.AppendString("str1"); file2.AppendFrames(tick13); file2.Dispose();

        Assert.ThrowsException<StringTableIncompatibleException>(() => FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2));

        FwobFile<Tick, int>.Concat(_tempPath, path0);
        Assert.AreEqual(Convert.ToBase64String(File.ReadAllBytes(path0)), Convert.ToBase64String(File.ReadAllBytes(_tempPath)));

        file0 = new FwobFile<Tick, int>(path0, "test", FileMode.Create);
        file1 = new FwobFile<Tick, int>(path1, "test", FileMode.Create);
        file2 = new FwobFile<Tick, int>(path2, "test", FileMode.Create);

        for (int i = 0; i < 48; i++)
            Assert.AreEqual(i, file0.AppendString(i.ToString()));
        for (int i = 0; i < 485; i++)
            Assert.AreEqual(i, file1.AppendString(i.ToString()));
        for (int i = 0; i < 85; i++)
            Assert.AreEqual(i, file2.AppendString(i.ToString()));

        Assert.AreEqual(10000, file0.AppendFrames(
                Enumerable.Range(0, 10000)
                    .Select(i => new Tick { Time = i, Value = i * 3.14, Str = i.ToString() })));
        Assert.AreEqual(7777, file1.AppendFrames(
                Enumerable.Range(0, 7777)
                    .Select(i => new Tick { Time = 10000 + i, Value = i * 2.14, Str = i.ToString() })));
        Assert.AreEqual(8888, file2.AppendFrames(
                Enumerable.Range(0, 8888)
                    .Select(i => new Tick { Time = 20000 + i, Value = i * 4.14, Str = i.ToString() })));

        file0.Dispose();
        file1.Dispose();
        file2.Dispose();

        FwobFile<Tick, int>.Concat(_tempPath, path0, path1, path2);

        Assert.AreEqual(FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength + 26665 * 16, new FileInfo(_tempPath).Length);

        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(26665, file.FrameCount);
        Assert.AreEqual(485, file.StringCount);
        Assert.AreEqual("test", file.Title);

        file.LoadStringTable();
        Assert.IsNotNull(file.Strings);

        for (int i = 0; i < 485; i++)
            Assert.AreEqual(i.ToString(), file.Strings[i]);
        file.UnloadStringTable();

        int k = 0;
        foreach (Tick frame in file.GetFramesAfter(0))
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

    [TestMethod]
    public void TestFrameDeletion()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        var tick12 = new Tick { Time = 12, Value = 99.88 };
        var tick13 = new Tick { Time = 13, Value = 44456.0111 };
        var tick14 = new Tick { Time = 14, Value = 44456.0111 };
        var tick15 = new Tick { Time = 15, Value = 44456.0111 };
        var tick100 = new Tick { Time = 100, Value = 1234.56 };

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesAfter(101), 0);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesAfter(100), 2);
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick15);

        Assert.AreEqual(file.DeleteFramesAfter(13), 5);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick12);

        Assert.AreEqual(file.DeleteFramesAfter(0), 2);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesAfter(0), 9);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBefore(11), 0);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBefore(12), 2);
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBefore(99), 5);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick100);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesAfter(100), 2);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.DeleteFramesBefore(13), 0);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.DeleteFramesAfter(13), 0);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBefore(100), 9);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.DeleteAllFrames(), 0);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteAllFrames(), 9);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick13), 1);
        Assert.AreEqual(1, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick13);

        Assert.AreEqual(file.DeleteAllFrames(), 1);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.ThrowsException<ArgumentException>(() => file.DeleteFramesBetween(15, 14));
        Assert.AreEqual(file.DeleteFramesBetween(13, 15), 5);
        Assert.AreEqual(4, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBetween(13, 20), 0);
        Assert.AreEqual(4, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBetween(13, 100), 2);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick12);

        Assert.AreEqual(file.DeleteFramesBetween(-100, 12), 2);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.DeleteFramesBetween(-100, 12), 0);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.ThrowsException<ArgumentException>(() => file.DeleteFrames(9, -100, 12));
        Assert.AreEqual(file.DeleteFrames(0, 11, 16, 18, 101, 200), 0);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(2, file.DeleteFrames(-100, 12));
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(-100, 14, 200), 1);
        Assert.AreEqual(6, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(15), 2);
        Assert.AreEqual(4, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(100), 2);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick13);

        Assert.AreEqual(file.DeleteFrames(0, 12, 13, 14, 15, 16, 100, 200), 2);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.DeleteFrames(0, 12, 13, 14, 15, 16, 100, 200), 0);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(0, 11, 12, 13, 17, 19, 100, 200), 6);
        Assert.AreEqual(3, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick14);
        Assert.AreEqual(file.LastFrame, tick15);

        Assert.AreEqual(file.DeleteAllFrames(), 3);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(11), 0);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(12), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(14, 15, 16, 17, 18), 3);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(4, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(100), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick13);

        Assert.AreEqual(file.DeleteFrames(13, 100), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBefore(11), 0);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBefore(12), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBefore(99), 5);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick100);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesAfter(100), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesAfter(101), 0);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesAfter(100), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick15);

        Assert.AreEqual(file.DeleteFramesAfter(13), 5);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick12);

        Assert.AreEqual(file.DeleteFramesAfter(0), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBetween(13, 10000), 7);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick12);

        Assert.AreEqual(file.DeleteFramesBetween(-10000, 10000), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBetween(-10000, 13), 4);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(5, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick14);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBetween(-10000, 10000), 5);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12, tick12, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteAllFrames(), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.DeleteAllFrames(), 0);
        file.Dispose();
        file = new FwobFile<Tick, int>(_tempPath);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);
    }

    [TestMethod]
    public void TestFileClosed()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        file.Close();

        Assert.IsNull(file.FilePath);
        Assert.IsNull(file.Stream);

        Assert.ThrowsException<FileNotOpenedException>(() => file.Title = "ABC");

        // When closed the header is reset to a blank one
        FwobHeader header = file.Header;
        // The schema is not related to the file but the generic type only
        FrameInfo frameInfo = file.FrameInfo;

        // All members of IFrameQueryable<TFrame, TKey> should throw exception
        Assert.ThrowsException<FileNotOpenedException>(() => file.FrameCount);
        Assert.ThrowsException<FileNotOpenedException>(() => file.FirstFrame);
        Assert.ThrowsException<FileNotOpenedException>(() => file.LastFrame);
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFrameAt(0));
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFrames(0).Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFrames(0, 1000).Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFramesBetween(0, 1000).Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFramesBefore(0).Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFramesAfter(0).Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetAllFrames().Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.AppendFrames(tick12a, tick12b));
        Assert.ThrowsException<FileNotOpenedException>(() => file.AppendFrames(new List<Tick> { tick12a }));
        Assert.ThrowsException<FileNotOpenedException>(() => file.AppendFramesTx(tick12a, tick12b));
        Assert.ThrowsException<FileNotOpenedException>(() => file.AppendFramesTx(new List<Tick> { tick12a }));
        Assert.ThrowsException<FileNotOpenedException>(() => file.DeleteFrames(0));
        Assert.ThrowsException<FileNotOpenedException>(() => file.DeleteFrames(0, 1000));
        Assert.ThrowsException<FileNotOpenedException>(() => file.DeleteFramesBetween(0, 1000));
        Assert.ThrowsException<FileNotOpenedException>(() => file.DeleteFramesBefore(0));
        Assert.ThrowsException<FileNotOpenedException>(() => file.DeleteFramesAfter(0));
        Assert.ThrowsException<FileNotOpenedException>(() => file.DeleteAllFrames());

        // All members of IStringTable should throw exception
        Assert.ThrowsException<FileNotOpenedException>(() => file.Strings);
        Assert.ThrowsException<FileNotOpenedException>(() => file.StringCount);
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetString(0));
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetIndex("asdf"));
        Assert.ThrowsException<FileNotOpenedException>(() => file.AppendString("asdf"));
        Assert.ThrowsException<FileNotOpenedException>(() => file.ContainsString("asdf"));
        Assert.ThrowsException<FileNotOpenedException>(() => file.DeleteAllStrings());

        // It should throw in loading but not in unloading or closing
        Assert.ThrowsException<FileNotOpenedException>(file.LoadStringTable);
        file.UnloadStringTable();
        file.Close();
    }

    private void ValidateNonWritableFile()
    {
        Assert.IsNotNull(file);

        Assert.IsNotNull(file.FilePath);
        Assert.IsNotNull(file.Stream);

        Assert.ThrowsException<FileNotWritableException>(() => file.Title = "ABC");

        // The header is readable
        FwobHeader header = file.Header;
        // The schema is not related to the file but the generic type only
        FrameInfo frameInfo = file.FrameInfo;

        // All writing members of IFrameQueryable<TFrame, TKey> should throw exception
        Assert.ThrowsException<FileNotWritableException>(() => file.AppendFrames(tick12a, tick12b));
        Assert.ThrowsException<FileNotWritableException>(() => file.AppendFrames(new List<Tick> { tick12a }));
        Assert.ThrowsException<FileNotWritableException>(() => file.AppendFramesTx(tick12a, tick12b));
        Assert.ThrowsException<FileNotWritableException>(() => file.AppendFramesTx(new List<Tick> { tick12a }));
        Assert.ThrowsException<FileNotWritableException>(() => file.DeleteFrames(0));
        Assert.ThrowsException<FileNotWritableException>(() => file.DeleteFrames(0, 1000));
        Assert.ThrowsException<FileNotWritableException>(() => file.DeleteFramesBetween(0, 1000));
        Assert.ThrowsException<FileNotWritableException>(() => file.DeleteFramesBefore(0));
        Assert.ThrowsException<FileNotWritableException>(() => file.DeleteFramesAfter(0));
        Assert.ThrowsException<FileNotWritableException>(() => file.DeleteAllFrames());

        // All members of IStringTable should throw exception
        file.LoadStringTable();
        Assert.AreEqual("str", file.Strings[0]);
        Assert.AreEqual(1, file.StringCount);
        Assert.AreEqual("str", file.GetString(0));
        Assert.AreEqual(-1, file.GetIndex("asdf"));
        Assert.IsFalse(file.ContainsString("asdf"));
        Assert.ThrowsException<FileNotWritableException>(() => file.AppendString("asdf"));
        Assert.ThrowsException<FileNotWritableException>(() => file.DeleteAllStrings());

        // It should throw in loading but not in unloading or closing
        file.UnloadStringTable();
    }

    private void ValidateNonReadableFile()
    {
        Assert.IsNotNull(file);

        Assert.IsNotNull(file.FilePath);
        Assert.IsNotNull(file.Stream);

        file.Title = "ABC";

        // The header is always readable
        FwobHeader header = file.Header;
        // The schema is not related to the file but the generic type only
        FrameInfo frameInfo = file.FrameInfo;

        // All frame/string reading members of IFrameQueryable<TFrame, TKey> should throw exception
        Assert.AreEqual(0, file.FrameCount);
        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFrameAt(0));
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFrames(0).Any());
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFrames(0, 1000).Any());
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFramesBetween(0, 1000).Any());
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFramesBefore(0).Any());
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFramesAfter(0).Any());
        Assert.ThrowsException<FileNotReadableException>(() => file.GetAllFrames().Any());
        Assert.AreEqual(2, file.AppendFrames(tick12a, tick12b));
        Assert.AreEqual(1, file.AppendFrames(new List<Tick> { tick12b }));
        Assert.AreEqual(2, file.AppendFramesTx(tick12b, tick12c));
        Assert.AreEqual(1, file.AppendFramesTx(new List<Tick> { tick100 }));
        // Deleting frames needs to locate the frames first
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFrames(0));
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFrames(12));
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFrames(0, 1000));
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFrames(-1, 100));
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFramesBetween(0, 1000));
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFramesBefore(0));
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFramesBefore(12));
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFramesBefore(12));
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFramesAfter(1000));
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFramesAfter(100));
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFramesAfter(100));
        Assert.ThrowsException<FileNotReadableException>(() => file.DeleteFramesAfter(-1000));
        Assert.AreEqual(6, file.DeleteAllFrames());
        Assert.AreEqual(0, file.DeleteAllFrames());

        // All members of IStringTable should throw exception
        Assert.ThrowsException<FileNotReadableException>(file.LoadStringTable);
        Assert.AreEqual(0, file.Strings.Count);
        Assert.AreEqual(0, file.StringCount);
        Assert.ThrowsException<FileNotReadableException>(() => file.GetString(0));
        Assert.ThrowsException<FileNotReadableException>(() => file.GetIndex("asdf"));
        Assert.AreEqual(0, file.AppendString("asdf"));
        Assert.ThrowsException<FileNotReadableException>(() => file.ContainsString("asdf"));
        Assert.AreEqual(1, file.AppendString("asdf"));
        Assert.AreEqual(0, file.Strings.Count);
        Assert.AreEqual(2, file.StringCount);
        Assert.ThrowsException<FileNotReadableException>(() => file.ContainsString("asdf"));
        Assert.AreEqual(2, file.DeleteAllStrings());
        Assert.AreEqual(0, file.DeleteAllStrings());

        // It should throw in loading but not in unloading or closing
        file.UnloadStringTable();
    }

    [TestMethod]
    public void TestFileAccess()
    {
        Assert.IsNotNull(file);
        Assert.IsNotNull(_tempPath);

        file.AppendString("str");
        file.Close();

        file = new FwobFile<Tick, int>(_tempPath, FileAccess.Read);
        ValidateNoFrame();
        ValidateNonWritableFile();
        file.Close();

        file = new FwobFile<Tick, int>(_tempPath);
        AddOneFrame();
        file.Close();

        file = new FwobFile<Tick, int>(_tempPath, FileAccess.Read);
        ValidateOneFrame();
        ValidateNonWritableFile();
        file.Close();

        file = new FwobFile<Tick, int>(_tempPath, "Title", access: FileAccess.Write);
        ValidateNonReadableFile();
        file.Close();

        Assert.ThrowsException<FileNotReadableException>(() => new FwobFile<Tick, int>(_tempPath, FileAccess.Write));
        Assert.ThrowsException<FileNotWritableException>(() => new FwobFile<Tick, int>(_tempPath, "Title", access: FileAccess.Read));
    }
}
