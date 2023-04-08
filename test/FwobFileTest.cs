#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozo.Fwob.Abstraction;
using Mozo.Fwob.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Mozo.Fwob.UnitTest.FrameValidators;
using static Mozo.Fwob.UnitTest.StringTableValidators;

namespace Mozo.Fwob.UnitTest;

[TestClass]
public class FwobFileTest
{
    [TestMethod]
    public void TestFileFormatIntegrity()
    {
        string temp = Path.GetTempFileName();
        CorruptedFileHeaderException exception1 = Assert.ThrowsException<CorruptedFileHeaderException>(() =>
        {
            byte[] bytes = Enumerable.Range(0, 255).Select(o => (byte)o).ToArray();
            File.WriteAllBytes(temp, bytes);
            using FwobFile<Tick, int> file = new(temp);
        });
        Assert.AreEqual(temp, exception1.FileName);
        File.Delete(temp);

        CorruptedFileLengthException exception2 = Assert.ThrowsException<CorruptedFileLengthException>(() =>
        {
            using (FwobFile<Tick, int> file = new(temp, "TestTick"))
            {
                file.AppendFrames(tick12a, tick12b, tick12c, tick13, tick100);
            }
            File.AppendAllText(temp, "A");
            using FwobFile<Tick, int> file2 = new(temp);
        });
        Assert.AreEqual(temp, exception2.FileName);
        Assert.AreEqual(2048 + 16 * 5 + 1, exception2.ActualLength);
        Assert.AreEqual(2048 + 16 * 5, exception2.FileLength);
        File.Delete(temp);

        CorruptedFileLengthException exception3 = Assert.ThrowsException<CorruptedFileLengthException>(() =>
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
        Assert.AreEqual(temp, exception3.FileName);
        Assert.AreEqual(2048 + 16 * 5, exception3.ActualLength);
        Assert.AreEqual(2048 + 16 * 4294967295L, exception3.FileLength);
        File.Delete(temp);

        CorruptedStringTableLengthException exception4 = Assert.ThrowsException<CorruptedStringTableLengthException>(() =>
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
        Assert.AreEqual(temp, exception4.FileName);
        Assert.AreEqual(106, exception4.ActualLength);
        Assert.AreEqual(22, exception4.StringTableLength);
        File.Delete(temp);

        CorruptedStringTableLengthException exception5 = Assert.ThrowsException<CorruptedStringTableLengthException>(() =>
        {
            using (FwobFile<Tick, int> file = new(temp, "TestTick"))
            {
                file.AppendFrames(tick12a, tick12b, tick12c, tick13, tick100);
                file.AppendString("asdf");
                file.AppendString("asdf");
                file.AppendString("asdf2");
                file.AppendString("asdf3");
            }
            using (FileStream file2 = File.Open(temp, FileMode.Open, FileAccess.ReadWrite))
            {
                file2.Seek(162, SeekOrigin.Begin);
                file2.Write(new byte[] { 5 });
            }
            using FwobFile<Tick, int> file3 = new(temp);
            file3.LoadStringTable();
        });
        Assert.AreEqual(temp, exception5.FileName);
        Assert.AreEqual(22, exception5.ActualLength);
        Assert.AreEqual(5, exception5.StringTableLength);
        File.Delete(temp);
    }

    #region Header testing
    [TestMethod]
    public void TestTitle()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        // Opening an existing file
        Assert.AreEqual("HelloFwob", file.Title);

        file.Title = "abcde";
        Assert.AreEqual(file.Title, "abcde");
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(file.Title, "abcde");

        Assert.ThrowsException<ArgumentException>(() => file.Title = "");
        Assert.AreEqual(file.Title, "abcde");
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(file.Title, "abcde");

        file.Title = "0123456789abcdef";
        Assert.AreEqual(file.Title, "0123456789abcdef");
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(file.Title, "0123456789abcdef");

        Assert.ThrowsException<ArgumentNullException>(() => file.Title = null);
        Assert.ThrowsException<ArgumentException>(() => file.Title = "");
        Assert.ThrowsException<TitleTooLongException>(() => file.Title = "0123456789abcdefg");
        Assert.AreEqual(file.Title, "0123456789abcdef");
        file.Dispose();
        File.Delete(tmpPath1);

        // Creating a new file
        string tmpfile2 = Path.GetTempFileName();
        Assert.ThrowsException<ArgumentNullException>(() => new FwobFile<Tick, int>(tmpfile2, null));
        Assert.ThrowsException<ArgumentException>(() => new FwobFile<Tick, int>(tmpfile2, ""));
        Assert.ThrowsException<ArgumentException>(() => new FwobFile<Tick, int>(tmpfile2, "  "));
        Assert.ThrowsException<TitleTooLongException>(() => new FwobFile<Tick, int>(tmpfile2, " 0123456789abcdefg "));
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpfile2, " 0123456789abcdef ");
        Assert.AreEqual("0123456789abcdef", file.Title);
        file.Dispose();
        File.Delete(tmpfile2);
    }
    #endregion

    #region StringTable testing

    [TestMethod]
    public void TestWritingBasicFile()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        file.LoadStringTable();
        ValidateFileBasic(file);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestReadingBasicFile()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        file.LoadStringTable();
        ValidateFileBasic(file);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestWritingStringTable()
    {
        string tmpPath1 = Path.GetTempFileName();
        using FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateStringTableWrite(file);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    private static void ValidateStringTableDeletion(IStringTable file, string tmpPath1)
    {
        file.DeleteAllStrings();

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
        Assert.AreEqual(-1, file.GetIndex("mystr"));
        Assert.AreEqual(0, file.StringCount);

        ValidateStringTableWrite(file);
        file.DeleteAllStrings();
        Assert.AreEqual(new FileInfo(tmpPath1).Length, FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength);

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
    public void TestStringTableNoRandomAccess()
    {
        string tmpPath1 = Path.GetTempFileName();
        using FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateStringTableSequential(file);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestClearingStringTable()
    {
        string tmpPath1 = Path.GetTempFileName();
        using FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateStringTableDeletion(file, tmpPath1);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestReadingStringTable()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateStringTableWrite(file);
        ValidateStringTableData(file);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        ValidateStringTableData(file);
        ValidateStringTableDeletion(file, tmpPath1);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestReadingStringTableCached()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        file.LoadStringTable();

        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(0, file.Strings.Count);
        ValidateStringTableWrite(file);
        ValidateStringTableData(file);

        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        file.LoadStringTable();

        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(3, file.Strings.Count);
        ValidateStringTableData(file);
        ValidateStringTableDeletion(file, tmpPath1);
        Assert.AreEqual(1, file.Strings.Count);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestCachingStringTable()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        file.LoadStringTable();
        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(0, file.Strings.Count);

        ValidateStringTableWrite(file);
        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(3, file.Strings.Count);
        Assert.AreEqual(file.Strings[0], "mystr");
        Assert.AreEqual(file.Strings[1], "hello2");
        Assert.AreEqual(file.Strings[2], "test_string3");

        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);

        file.LoadStringTable();
        file.LoadStringTable();
        file.LoadStringTable();
        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(3, file.Strings.Count);
        Assert.AreEqual(file.Strings[0], "mystr");
        Assert.AreEqual(file.Strings[1], "hello2");
        Assert.AreEqual(file.Strings[2], "test_string3");
        ValidateStringTableDeletion(file, tmpPath1);
        Assert.AreEqual(1, file.Strings.Count);
        Assert.AreEqual(file.Strings[0], "hello2");
        file.LoadStringTable();
        file.UnloadStringTable();

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestStringTableLimit()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

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
        file = new FwobFile<Tick, int>(tmpPath1);

        for (int i = 0; i < 262; i++)
        {
            Assert.AreEqual(file.GetIndex($"abc{i:d3}"), i);
            Assert.AreEqual(file.GetString(i), $"abc{i:d3}");
        }
        Assert.ThrowsException<StringTableOutOfSpaceException>(() => file.AppendString(""));

        file.Dispose();
        File.Delete(tmpPath1);
    }

    #endregion

    #region Frame data testing
    [TestMethod]
    public void TestNoFrame()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateNoFrame(file);

        file.Dispose();

        file = new(tmpPath1);
        ValidateNoFrame(file);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestOneFrame()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        AddOneFrame(file);
        ValidateOneFrame(file);

        file.Dispose();

        file = new(tmpPath1);
        ValidateOneFrame(file);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestClearingFrame()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        Assert.AreEqual(new FileInfo(tmpPath1).Length, FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength);
        file.DeleteAllFrames();
        ValidateNoFrame(file, tmpPath1);
        file.Dispose();

        Assert.AreEqual(new FileInfo(tmpPath1).Length, FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength);
        file = new FwobFile<Tick, int>(tmpPath1);
        ValidateNoFrame(file, tmpPath1);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestReadingOneFrame()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateNoFrame(file, tmpPath1);
        AddOneFrame(file);
        ValidateOneFrame(file);
        file.Dispose();

        file = new FwobFile<Tick, int>(tmpPath1);
        ValidateOneFrame(file);
        file.DeleteAllFrames();
        ValidateNoFrame(file, tmpPath1);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestFramesSameKey()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        AddFramesSameKey(file);
        ValidateFramesSameKey(file);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestReadingFramesSameKey()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateNoFrame(file, tmpPath1);
        AddFramesSameKey(file);
        ValidateFramesSameKey(file);
        file.Dispose();

        file = new FwobFile<Tick, int>(tmpPath1);
        ValidateFramesSameKey(file);
        file.DeleteAllFrames();
        ValidateNoFrame(file, tmpPath1);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestFramesMultiKeys()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        AddFramesMultiKeys(file);
        ValidateFramesMultiKeys(file);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestReadingFramesMultiKeys()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateNoFrame(file, tmpPath1);
        AddFramesMultiKeys(file);
        ValidateFramesMultiKeys(file);
        file.Dispose();

        file = new FwobFile<Tick, int>(tmpPath1);
        ValidateFramesMultiKeys(file);
        file.DeleteAllFrames();
        ValidateNoFrame(file, tmpPath1);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestFramesPartially()
    {
        string tmpPath1 = Path.GetTempFileName();
        using FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        AddFramesPartially(file, tmpPath1);
        AddFramesPartially2(file, tmpPath1);
        ValidateFramesPartially(file);

        AddFramesPartially2(file, tmpPath1);
        ValidateFramesPartially(file);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestFramesStringField()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateFrameStringField(file, tmpPath1);

        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);

        ValidateFrameStringField2(file);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestReadingFramesPartially()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateNoFrame(file, tmpPath1);
        AddFramesPartially(file, tmpPath1);
        AddFramesPartially2(file, tmpPath1);
        ValidateFramesPartially(file);

        AddFramesPartially2(file, tmpPath1);
        ValidateFramesPartially(file);
        file.Dispose();

        file = new FwobFile<Tick, int>(tmpPath1);
        ValidateFramesPartially(file);
        file.DeleteAllFrames();
        ValidateNoFrame(file, tmpPath1);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestFramesTransactional()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateFramesAppendingTx(file, tmpPath1);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestReadingFramesTransactional()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12c, tick100, tick13));
        ValidateNoFrame(file, tmpPath1);
        AddOneFrame(file);
        file.Dispose();

        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick100, tick13));
        ValidateOneFrame(file);
        Assert.AreEqual(1, file.AppendFrames(tick100));
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);
        Assert.IsNotNull(file.FrameInfo);
        file.Dispose();

        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12b));
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12c));
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick13));
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);
        Assert.IsNotNull(file.FrameInfo);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestFrameDeletion()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateFrameDeletion(file);

        Assert.AreEqual(file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(11), 0);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(12), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(14, 15, 16, 17, 18), 3);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(4, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFrames(100), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick13);

        Assert.AreEqual(file.DeleteFrames(13, 100), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBefore(11), 0);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBefore(12), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick13);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBefore(99), 5);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick100);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesAfter(100), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesAfter(101), 0);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesAfter(100), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick15);

        Assert.AreEqual(file.DeleteFramesAfter(13), 5);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick12a);

        Assert.AreEqual(file.DeleteFramesAfter(0), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBetween(13, 10000), 7);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick12a);

        Assert.AreEqual(file.DeleteFramesBetween(-10000, 10000), 2);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBetween(-10000, 13), 4);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(5, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick14);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteFramesBetween(-10000, 10000), 5);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, tick12a);
        Assert.AreEqual(file.LastFrame, tick100);

        Assert.AreEqual(file.DeleteAllFrames(), 9);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(file.DeleteAllFrames(), 0);
        file.Dispose();
        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        file.Dispose();
        File.Delete(tmpPath1);
    }
    #endregion

    #region Mixed and performance testing
    [TestMethod]
    public void TestMixingStringTableAndFrames()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        ValidateStringTableWrite(file);
        AddOneFrame(file);
        ValidateStringTableData(file);
        ValidateOneFrame(file);
        file.Dispose();

        file = new FwobFile<Tick, int>(tmpPath1);
        ValidateOneFrame(file);
        ValidateStringTableData(file);

        // duplicate
        Assert.AreEqual(3, file.AppendString("mystr"));
        Assert.AreEqual(4, file.StringCount);
        Assert.AreEqual(4, file.AppendString("hello2"));
        Assert.AreEqual(5, file.StringCount);
        Assert.AreEqual(5, file.AppendString("test_string3"));
        Assert.AreEqual(6, file.StringCount);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestStringTableAndFramesPerformance()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

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

        file = new FwobFile<Tick, int>(tmpPath1);
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

        file = new FwobFile<Tick, int>(tmpPath1);
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

        file = new FwobFile<Tick, int>(tmpPath1);
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

        file = new FwobFile<Tick, int>(tmpPath1);
        Assert.ThrowsException<StringTableOutOfSpaceException>(() => file.AppendString(""));
        Assert.AreEqual(file.FrameCount, 18 * 262);
        file.Dispose();

        File.Delete(tmpPath1);
    }
    #endregion

    #region Organizers
    [TestMethod]
    public void TestFileSplit()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);
        string filename = Path.GetFileNameWithoutExtension(tmpPath1);
        string outDir = Path.GetDirectoryName(tmpPath1)!;

        // Test parameter validators
        file.Dispose();
        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Split(null, null));
        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Split(null, outDir));
        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Split(tmpPath1, null));
        Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Split(tmpPath1, outDir));
        Assert.ThrowsException<FrameNotFoundException>(() => FwobFile<Tick, int>.Split(tmpPath1, outDir, 1, 2));

        file = new FwobFile<Tick, int>(tmpPath1);

        for (int i = 0; i < 485; i++)
            Assert.AreEqual(i, file.AppendString(i.ToString()));

        Assert.AreEqual(10000, file.AppendFrames(
                Enumerable.Range(0, 10000)
                    .Select(i => new Tick { Time = i, Value = i * 3.14, Str = i.ToString() })));

        Assert.AreEqual(10000, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, new Tick { Time = 0, Value = 0, Str = "0" });
        Assert.AreEqual(file.LastFrame, new Tick { Time = 9999, Value = 9999 * 3.14, Str = "9999" });
        file.Dispose();

        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Split(tmpPath1, outDir, null));
        Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Split(tmpPath1, outDir, 1, 0));
        Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Split(tmpPath1, outDir, 1, 1));
        Assert.ThrowsException<FileNotFoundException>(() => FwobFile<Tick, int>.Split(tmpPath1 + ".nonexistence", outDir, 1, 2));

        // Test the number of parts
        string tmpDirName = Path.GetTempFileName();
        File.Delete(tmpDirName);

        foreach (int k in new[] { -10000, -1, 0, 1000, 9999, 10000, 20000 })
        {
            Directory.CreateDirectory(tmpDirName);
            FwobFile<Tick, int>.Split(tmpPath1, tmpDirName, new[] { k }, ignoreEmptyParts: false);
            Assert.AreEqual(2, Directory.GetFiles(tmpDirName).Length);
            Directory.Delete(tmpDirName, true);
        }

        foreach (int k in new[] { -10000, -1, 0, 1000, 9999, 10000, 20000 })
        {
            Directory.CreateDirectory(tmpDirName);
            FwobFile<Tick, int>.Split(tmpPath1, tmpDirName, new[] { k, k + 1000 }, ignoreEmptyParts: false);
            Assert.AreEqual(3, Directory.GetFiles(tmpDirName).Length);
            Directory.Delete(tmpDirName, true);
        }

        foreach (int k in new[] { -10000, -1, 0, 10000, 20000 })
        {
            Directory.CreateDirectory(tmpDirName);
            FwobFile<Tick, int>.Split(tmpPath1, tmpDirName, new[] { k }, ignoreEmptyParts: true);
            string[] names = Directory.GetFiles(tmpDirName);
            Assert.AreEqual(1, names.Length);
            Assert.AreEqual(Convert.ToBase64String(File.ReadAllBytes(tmpPath1)), Convert.ToBase64String(File.ReadAllBytes(names[0])));
            Directory.Delete(tmpDirName, true);
        }

        foreach (int k in new[] { -10000, 10000, 20000 })
        {
            Directory.CreateDirectory(tmpDirName);
            FwobFile<Tick, int>.Split(tmpPath1, tmpDirName, new[] { k, k + 1000 }, ignoreEmptyParts: true);
            string[] names = Directory.GetFiles(tmpDirName);
            Assert.AreEqual(1, names.Length);
            Assert.AreEqual(Convert.ToBase64String(File.ReadAllBytes(tmpPath1)), Convert.ToBase64String(File.ReadAllBytes(names[0])));
            Directory.Delete(tmpDirName, true);
        }

        foreach (int k in new[] { 1000, 9999 })
        {
            Directory.CreateDirectory(tmpDirName);
            FwobFile<Tick, int>.Split(tmpPath1, tmpDirName, new[] { k }, ignoreEmptyParts: true);
            Assert.AreEqual(2, Directory.GetFiles(tmpDirName).Length);
            Directory.Delete(tmpDirName, true);
        }

        // Test splitting
        int baseLength = FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength;

        FwobFile<Tick, int>.Split(tmpPath1, outDir, 100);
        string path0 = $"{tmpPath1}.part0.fwob";
        string path1 = $"{tmpPath1}.part1.fwob";
        Assert.IsTrue(File.Exists(path0));
        Assert.IsTrue(File.Exists(path1));
        Assert.AreEqual(baseLength + 100 * 16, new FileInfo(path0).Length);
        Assert.AreEqual(baseLength + 9900 * 16, new FileInfo(path1).Length);

        using (FwobFile<Tick, int> ff = new(path0))
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
        using (FwobFile<Tick, int> ff = new(path1))
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

        outDir = Path.Combine(outDir, "NewSubDir");
        File.Move(tmpPath1, tmpPath1 + ".fwob");
        path0 = Path.Combine(outDir, $"{Path.GetFileName(tmpPath1)}.part0.fwob");
        path1 = Path.Combine(outDir, $"{Path.GetFileName(tmpPath1)}.part1.fwob");
        string path2 = Path.Combine(outDir, $"{Path.GetFileName(tmpPath1)}.part2.fwob");

        FwobFile<Tick, int>.Split(tmpPath1 + ".fwob", outDir, 1, 9999);
        Assert.IsTrue(File.Exists(path0));
        Assert.IsTrue(File.Exists(path1));
        Assert.IsTrue(File.Exists(path2));
        Assert.AreEqual(baseLength + 1 * 16, new FileInfo(path0).Length);
        Assert.AreEqual(baseLength + 9998 * 16, new FileInfo(path1).Length);
        Assert.AreEqual(baseLength + 1 * 16, new FileInfo(path2).Length);
        tmpPath1 += ".fwob";

        using (FwobFile<Tick, int> ff = new(path0))
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
        using (FwobFile<Tick, int> ff = new(path1))
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
        using (FwobFile<Tick, int> ff = new(path2))
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
        Directory.Delete(outDir, true);

        file.Dispose();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestFileConcat()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        file.Dispose();
        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Concat(null));
        Assert.ThrowsException<ArgumentNullException>(() => FwobFile<Tick, int>.Concat(tmpPath1, null));
        Assert.ThrowsException<ArgumentException>(() => FwobFile<Tick, int>.Concat(tmpPath1));
        Assert.ThrowsException<FileNotFoundException>(() => FwobFile<Tick, int>.Concat(tmpPath1, tmpPath1 + ".nonexistence"));

        string path0 = tmpPath1 + ".part0.fwob";
        string path1 = tmpPath1 + ".part1.fwob";
        string path2 = tmpPath1 + ".part2.fwob";
        Assert.ThrowsException<FileNotFoundException>(() => FwobFile<Tick, int>.Concat(tmpPath1, path0, path1, path2));

        // test file title consistence
        FwobFile<Tick, int> file0 = new(path0, "test0"); file0.Dispose();
        FwobFile<Tick, int> file1 = new(path1, "test"); file1.Dispose();
        FwobFile<Tick, int> file2 = new(path2, "test"); file2.Dispose();

        Assert.ThrowsException<TitleIncompatibleException>(() => FwobFile<Tick, int>.Concat(tmpPath1, path0, path1, path2));

        file0 = new FwobFile<Tick, int>(path0, "test", FileMode.Create);
        file1 = new FwobFile<Tick, int>(path1, "test", FileMode.Create);
        file2 = new FwobFile<Tick, int>(path2, "test2", FileMode.Create);

        file0.AppendFrames(tick12a); file0.Dispose();
        file1.AppendFrames(tick12a); file1.Dispose();
        file2.AppendFrames(tick12a); file2.Dispose();

        Assert.ThrowsException<TitleIncompatibleException>(() => FwobFile<Tick, int>.Concat(tmpPath1, path0, path1, path2));

        // test frame ordering enforcement
        file0 = new FwobFile<Tick, int>(path0, "test", FileMode.Create);
        file1 = new FwobFile<Tick, int>(path1, "test", FileMode.Create);
        file2 = new FwobFile<Tick, int>(path2, "test", FileMode.Create);

        file0.AppendFrames(tick100); file0.Dispose();
        file1.AppendFrames(tick13); file1.Dispose();
        file2.AppendFrames(tick12c); file2.Dispose();

        Assert.ThrowsException<KeyOrderViolationException>(() => FwobFile<Tick, int>.Concat(tmpPath1, path0, path1, path2));

        // test blank file
        file0 = new FwobFile<Tick, int>(path0, "test", FileMode.Create);
        file1 = new FwobFile<Tick, int>(path1, "test", FileMode.Create);
        file2 = new FwobFile<Tick, int>(path2, "test", FileMode.Create);

        file0.AppendFrames(tick12c); file0.Dispose();
        file1.Dispose();
        file2.AppendFrames(tick100); file2.Dispose();

        FwobFile<Tick, int>.Concat(tmpPath1, path0, path1, path2);

        // test empty string table
        file0 = new FwobFile<Tick, int>(path0, "test", FileMode.Create);
        file1 = new FwobFile<Tick, int>(path1, "test", FileMode.Create);
        file2 = new FwobFile<Tick, int>(path2, "test", FileMode.Create);

        file0.AppendFrames(tick12c); file0.Dispose();
        file1.AppendFrames(tick13); file1.Dispose();
        file2.AppendFrames(tick100); file2.Dispose();

        FwobFile<Tick, int>.Concat(tmpPath1, path0, path1, path2);

        // test string table exception
        file0 = new FwobFile<Tick, int>(path0, "test", FileMode.Create);
        file1 = new FwobFile<Tick, int>(path1, "test", FileMode.Create);
        file2 = new FwobFile<Tick, int>(path2, "test", FileMode.Create);

        file0.AppendString("str0"); file0.AppendFrames(tick12b); file0.Dispose();
        file1.AppendString("str0"); file1.AppendString("str1"); file1.AppendFrames(tick12c); file1.Dispose();
        file2.AppendString("str1"); file2.AppendFrames(tick13); file2.Dispose();

        Assert.ThrowsException<StringTableIncompatibleException>(() => FwobFile<Tick, int>.Concat(tmpPath1, path0, path1, path2));

        FwobFile<Tick, int>.Concat(tmpPath1, path0);
        Assert.AreEqual(Convert.ToBase64String(File.ReadAllBytes(path0)), Convert.ToBase64String(File.ReadAllBytes(tmpPath1)));

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

        FwobFile<Tick, int>.Concat(tmpPath1, path0, path1, path2);

        Assert.AreEqual(FwobHeader.HeaderLength + FwobHeader.DefaultStringTablePreservedLength + 26665 * 16, new FileInfo(tmpPath1).Length);

        file = new FwobFile<Tick, int>(tmpPath1);
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

        File.Delete(tmpPath1 + ".part0.fwob");
        File.Delete(tmpPath1 + ".part1.fwob");
        File.Delete(tmpPath1 + ".part2.fwob");

        File.Delete(tmpPath1);
    }
    #endregion

    [TestMethod]
    public void TestFileUnclosed()
    {
        string tmpPath1 = Path.GetTempFileName();
        string tmpPath2 = Path.GetTempFileName();

        FwobFile<Tick, int> file1 = new(tmpPath1, "HelloFwob", FileMode.Create);

        // AppendFrames(params Tick[])
        file1.AppendFrames(tick12a);
        File.Copy(tmpPath1, tmpPath2, true);

        FwobFile<Tick, int> file2 = new(tmpPath2);
        ValidateOneFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // DeleteFrames(params int[])
        file1.DeleteFrames(12);
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        ValidateNoFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // AppendFrames(IEnumerable<Tick>)
        file1.AppendFrames((IEnumerable<Tick>)new[] { tick12a });
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        ValidateOneFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // DeleteFrames(IEnumerable<int>)
        file1.DeleteFrames((IEnumerable<int>)new[] { 12 });
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        ValidateNoFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // AppString(string)
        file1.AppendString("mystr");
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        file2.LoadStringTable();
        Assert.AreEqual(1, file2.StringCount);
        Assert.AreEqual("mystr", file2.GetString(0));
        Assert.IsTrue(file2.ContainsString("mystr"));

        file2.Close();
        File.Delete(tmpPath2);

        // DeleteAllStrings()
        file1.DeleteAllStrings();
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        file2.LoadStringTable();
        Assert.AreEqual(0, file2.StringCount);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => file2.GetString(0));
        Assert.IsFalse(file2.ContainsString("mystr"));

        file2.Close();
        File.Delete(tmpPath2);

        // AppendFramesTx(params Tick[])
        file1.AppendFramesTx(tick12a);
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        ValidateOneFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // DeleteFramesAfter(int)
        file1.DeleteFramesAfter(12);
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        ValidateNoFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // AppendFramesTx(IEnumerable<Tick>)
        file1.AppendFramesTx((IEnumerable<Tick>)new[] { tick12a });
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        ValidateOneFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // DeleteFramesBefore(int)
        file1.DeleteFramesBefore(12);
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        ValidateNoFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // AppendFramesTx(params Tick[])
        file1.AppendFramesTx(tick12a);
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        ValidateOneFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // DeleteFramesBetween(int, int)
        file1.DeleteFramesBetween(12, 12);
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        ValidateNoFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // AppendFramesTx(IEnumerable<Tick>)
        file1.AppendFramesTx((IEnumerable<Tick>)new[] { tick12a });
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        ValidateOneFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // DeleteAllFrames()
        file1.DeleteAllFrames();
        File.Copy(tmpPath1, tmpPath2, true);

        file2 = new(tmpPath2);
        ValidateNoFrame(file2);

        file2.Close();
        File.Delete(tmpPath2);

        // Clean up
        file1.Close();
        File.Delete(tmpPath1);
    }

    [TestMethod]
    public void TestFileClosed()
    {
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

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
        Assert.ThrowsException<FileNotOpenedException>(() => file.LowerBoundOf(0));
        Assert.ThrowsException<FileNotOpenedException>(() => file.UpperBoundOf(0));
        Assert.ThrowsException<FileNotOpenedException>(() => file.EqualRangeOf(0));
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetKeyAt(0));
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFrameAt(0));
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFrames(0).Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFrames(0, 1000).Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFramesBetween(0, 1000).Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFramesBefore(0).Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetFramesAfter(0).Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetAllFrames().Any());
        Assert.ThrowsException<FileNotOpenedException>(() => file.GetEnumerator().MoveNext());
        Assert.ThrowsException<FileNotOpenedException>(() => file.Any());
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

        file.Dispose();
        File.Delete(tmpPath1);
    }

    private static void ValidateNonWritableFile(FwobFile<Tick, int> file)
    {
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

    private static void ValidateNonReadableFile(FwobFile<Tick, int> file)
    {
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
        Assert.ThrowsException<FileNotReadableException>(() => file.LowerBoundOf(0));
        Assert.ThrowsException<FileNotReadableException>(() => file.UpperBoundOf(0));
        Assert.ThrowsException<FileNotReadableException>(() => file.EqualRangeOf(0));
        Assert.ThrowsException<FileNotReadableException>(() => file.GetKeyAt(0));
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFrameAt(0));
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFrames(0).Any());
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFrames(0, 1000).Any());
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFramesBetween(0, 1000).Any());
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFramesBefore(0).Any());
        Assert.ThrowsException<FileNotReadableException>(() => file.GetFramesAfter(0).Any());
        Assert.ThrowsException<FileNotReadableException>(() => file.GetAllFrames().Any());
        Assert.ThrowsException<FileNotReadableException>(() => file.GetEnumerator().MoveNext());
        Assert.ThrowsException<FileNotReadableException>(() => file.Any());
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
        string tmpPath1 = Path.GetTempFileName();
        FwobFile<Tick, int> file = new(tmpPath1, "HelloFwob", FileMode.Create);

        file.AppendString("str");
        file.Close();

        file = new FwobFile<Tick, int>(tmpPath1, FileAccess.Read);
        ValidateNoFrame(file);
        ValidateNonWritableFile(file);
        file.Close();

        file = new FwobFile<Tick, int>(tmpPath1);
        AddOneFrame(file);
        file.Close();

        file = new FwobFile<Tick, int>(tmpPath1, FileAccess.Read);
        ValidateOneFrame(file);
        ValidateNonWritableFile(file);
        file.Close();

        file = new FwobFile<Tick, int>(tmpPath1, "Title", access: FileAccess.Write);
        ValidateNonReadableFile(file);
        file.Close();

        Assert.ThrowsException<FileNotReadableException>(() => new FwobFile<Tick, int>(tmpPath1, FileAccess.Write));
        Assert.ThrowsException<FileNotWritableException>(() => new FwobFile<Tick, int>(tmpPath1, "Title", access: FileAccess.Read));

        file.Dispose();
        File.Delete(tmpPath1);
    }
}

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
