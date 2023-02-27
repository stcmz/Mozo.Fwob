#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozo.Fwob.Exceptions;
using Mozo.Fwob.Models;
using System;
using System.Linq;

namespace Mozo.Fwob.UnitTest;

[TestClass]
public class InMemoryFwobFileTest
{
    private class Tick
    {
        public int Time;
        public double Value;
    }


    [TestMethod]
    public void TestBasicNewObject()
    {
        var file = new InMemoryFwobFile<Tick, int>("HelloFwob");
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
        Assert.ThrowsException<ArgumentNullException>(() => file.Title = null);
        Assert.ThrowsException<ArgumentException>(() => file.Title = "");
        Assert.ThrowsException<TitleTooLongException>(() => file.Title = "0123456789abcdefg");
    }

    [TestMethod]
    public void TestStringTableNoRandomAccess()
    {
        var file = new InMemoryFwobFile<Tick, int>("HelloFwob");
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

        // adding after clearing
        Assert.IsTrue(file.AppendString("hello2") == 0);
        Assert.IsTrue(file.StringCount == 1);
        Assert.IsTrue(file.GetString(0) == "hello2");
        Assert.IsTrue(file.GetIndex("mystr") == -1);
        Assert.IsTrue(file.GetIndex("hello2") == 0);
        Assert.IsTrue(file.StringCount == 1);
    }

    [TestMethod]
    public void TestFramesBasic()
    {
        var file = new InMemoryFwobFile<Tick, int>("HelloFwob");
        // no frame
        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.IsTrue(file.FrameCount == 0);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrame(0));
        Assert.IsNull(file.GetFrame(-1));
        Assert.IsNull(file.GetFrame(1));
        Assert.IsFalse(file.GetFrames(0).Any());
        Assert.IsFalse(file.GetFrames(0, 20).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());

        // adding the first
        var tick = new Tick { Time = 12, Value = 99.88 };

        Assert.IsTrue(file.AppendFrames(tick) == 1);
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

        // clearing
        file.ClearFrames();
        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.IsTrue(file.FrameCount == 0);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrame(0));
        Assert.IsNull(file.GetFrame(-1));
        Assert.IsNull(file.GetFrame(1));
        Assert.IsFalse(file.GetFrames(0).Any());
        Assert.IsFalse(file.GetFrames(0, 20).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());
    }

    [TestMethod]
    public void TestFramesSameKey()
    {
        var file = new InMemoryFwobFile<Tick, int>("HelloFwob");
        // adding the first
        var tick = new Tick { Time = 12, Value = 99.88 };
        var tick2 = new Tick { Time = 12, Value = 44456.0111 };
        var tick3 = new Tick { Time = 12, Value = 1234.56 };

        Assert.IsTrue(file.AppendFrames(tick, tick2, tick3) == 3);
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

        // clearing
        file.ClearFrames();
        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.IsTrue(file.FrameCount == 0);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrame(0));
        Assert.IsNull(file.GetFrame(-1));
        Assert.IsNull(file.GetFrame(1));
        Assert.IsFalse(file.GetFrames(12).Any());
        Assert.IsFalse(file.GetFrames(0, 20).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());
    }

    [TestMethod]
    public void TestFramesMultiKeys()
    {
        var file = new InMemoryFwobFile<Tick, int>("HelloFwob");
        // adding the first
        var tick = new Tick { Time = 12, Value = 99.88 };
        var tick2 = new Tick { Time = 13, Value = 44456.0111 };
        var tick3 = new Tick { Time = 100, Value = 1234.56 };

        Assert.IsTrue(file.AppendFrames(tick, tick2, tick3) == 3);
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

        // clearing
        file.ClearFrames();
        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.IsTrue(file.FrameCount == 0);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrame(0));
        Assert.IsNull(file.GetFrame(-1));
        Assert.IsNull(file.GetFrame(1));
        Assert.IsFalse(file.GetFrames(12).Any());
        Assert.IsFalse(file.GetFrames(0, 20).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());
    }

    [TestMethod]
    public void TestFramesExceptionsAndMore()
    {
        var file = new InMemoryFwobFile<Tick, int>("HelloFwob");
        // adding the first
        var tick = new Tick { Time = 12, Value = 99.88 };
        var tick2 = new Tick { Time = 13, Value = 44456.0111 };
        var tick3 = new Tick { Time = 100, Value = 1234.56 };

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFrames(tick, tick3, tick2));

        // only two frames
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick3);
        Assert.IsTrue(file.FrameCount == 2);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrame(-1));
        Assert.AreEqual(file.GetFrame(0), tick);
        Assert.AreEqual(file.GetFrame(1), tick3);
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

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFrames(tick2));
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick3);
        Assert.IsTrue(file.FrameCount == 2);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrame(-1));
        Assert.AreEqual(file.GetFrame(0), tick);
        Assert.AreEqual(file.GetFrame(1), tick3);
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

        // clearing
        file.ClearFrames();
        Assert.IsTrue(file.AppendFrames(tick2) == 1);
        Assert.AreEqual(file.FirstFrame, tick2);
        Assert.AreEqual(file.LastFrame, tick2);
        Assert.IsTrue(file.FrameCount == 1);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrame(-1));
        Assert.AreEqual(file.GetFrame(0), tick2);
        Assert.IsNull(file.GetFrame(1));
        Assert.IsNull(file.GetFrame(12));
        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.IsTrue(file.GetFrames(13).Count() == 1);
        Assert.IsTrue(file.GetFrames(-1, 20).Count() == 1);
        Assert.IsTrue(file.GetFrames(12, 20).Count() == 1);
        Assert.IsTrue(file.GetFrames(13, 20).Count() == 1);
        Assert.IsTrue(file.GetFrames(14, 20).Count() == 0);
        Assert.IsTrue(file.GetFrames(-1, 14).Count() == 1);
        Assert.IsTrue(file.GetFrames(-1, 13).Count() == 1);
        Assert.IsTrue(file.GetFrames(-1, 12).Count() == 0);
        Assert.IsTrue(file.GetFramesAfter(-1).Count() == 1);
        Assert.IsTrue(file.GetFramesAfter(12).Count() == 1);
        Assert.IsTrue(file.GetFramesAfter(13).Count() == 1);
        Assert.IsTrue(file.GetFramesAfter(14).Count() == 0);
        Assert.IsTrue(file.GetFramesBefore(0).Count() == 0);
        Assert.IsTrue(file.GetFramesBefore(12).Count() == 0);
        Assert.IsTrue(file.GetFramesBefore(13).Count() == 1);
        Assert.IsTrue(file.GetFramesBefore(14).Count() == 1);
    }

    [TestMethod]
    public void TestFramesExceptionsAndMoreTransactional()
    {
        var file = new InMemoryFwobFile<Tick, int>("HelloFwob");
        // adding the first
        var tick = new Tick { Time = 12, Value = 99.88 };
        var tick2 = new Tick { Time = 13, Value = 44456.0111 };
        var tick3 = new Tick { Time = 100, Value = 1234.56 };

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick, tick3, tick2));

        // no frame
        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.IsTrue(file.FrameCount == 0);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrame(0));
        Assert.IsNull(file.GetFrame(-1));
        Assert.IsNull(file.GetFrame(1));
        Assert.IsFalse(file.GetFrames(0).Any());
        Assert.IsFalse(file.GetFrames(0, 20).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());

        Assert.IsTrue(file.AppendFrames(tick) == 1);
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick3, tick2));
        Assert.IsTrue(file.AppendFrames(tick3) == 1);
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick2));

        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick3);
        Assert.IsTrue(file.FrameCount == 2);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrame(-1));
        Assert.AreEqual(file.GetFrame(0), tick);
        Assert.AreEqual(file.GetFrame(1), tick3);
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

        // clearing
        file.ClearFrames();
        Assert.IsTrue(file.AppendFrames(tick2) == 1);
        Assert.AreEqual(file.FirstFrame, tick2);
        Assert.AreEqual(file.LastFrame, tick2);
        Assert.IsTrue(file.FrameCount == 1);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrame(-1));
        Assert.AreEqual(file.GetFrame(0), tick2);
        Assert.IsNull(file.GetFrame(1));
        Assert.IsNull(file.GetFrame(13));
        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.IsTrue(file.GetFrames(13).Count() == 1);
        Assert.IsTrue(file.GetFrames(-1, 20).Count() == 1);
        Assert.IsTrue(file.GetFrames(12, 20).Count() == 1);
        Assert.IsTrue(file.GetFrames(13, 20).Count() == 1);
        Assert.IsTrue(file.GetFrames(14, 20).Count() == 0);
        Assert.IsTrue(file.GetFrames(-1, 14).Count() == 1);
        Assert.IsTrue(file.GetFrames(-1, 13).Count() == 1);
        Assert.IsTrue(file.GetFrames(-1, 12).Count() == 0);
        Assert.IsTrue(file.GetFramesAfter(-1).Count() == 1);
        Assert.IsTrue(file.GetFramesAfter(12).Count() == 1);
        Assert.IsTrue(file.GetFramesAfter(13).Count() == 1);
        Assert.IsTrue(file.GetFramesAfter(14).Count() == 0);
        Assert.IsTrue(file.GetFramesBefore(0).Count() == 0);
        Assert.IsTrue(file.GetFramesBefore(12).Count() == 0);
        Assert.IsTrue(file.GetFramesBefore(13).Count() == 1);
        Assert.IsTrue(file.GetFramesBefore(14).Count() == 1);
    }

    [TestMethod]
    public void TestFrameDeletion()
    {
        var file = new InMemoryFwobFile<Tick, int>("HelloFwob");
        // adding the first
        var tick = new Tick { Time = 12, Value = 99.88 };
        var tick2 = new Tick { Time = 13, Value = 44456.0111 };
        var tick3 = new Tick { Time = 14, Value = 44456.0111 };
        var tick4 = new Tick { Time = 15, Value = 44456.0111 };
        var tick5 = new Tick { Time = 100, Value = 1234.56 };

        Assert.IsTrue(9 == file.AppendFrames(tick, tick, tick2, tick2, tick3, tick4, tick4, tick5, tick5));
        Assert.IsTrue(file.FrameCount == 9);
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick5);

        Assert.IsTrue(0 == file.DeleteFramesAfter(101));
        Assert.IsTrue(file.FrameCount == 9);
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick5);

        Assert.IsTrue(2 == file.DeleteFramesAfter(100));
        Assert.IsTrue(file.FrameCount == 7);
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick4);

        Assert.IsTrue(5 == file.DeleteFramesAfter(13));
        Assert.IsTrue(file.FrameCount == 2);
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick);

        Assert.IsTrue(2 == file.DeleteFramesAfter(0));
        Assert.IsTrue(file.FrameCount == 0);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.IsTrue(9 == file.AppendFrames(tick, tick, tick2, tick2, tick3, tick4, tick4, tick5, tick5));
        Assert.IsTrue(file.FrameCount == 9);
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick5);

        Assert.IsTrue(0 == file.DeleteFramesBefore(11));
        Assert.IsTrue(file.FrameCount == 9);
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick5);

        Assert.IsTrue(2 == file.DeleteFramesBefore(12));
        Assert.IsTrue(file.FrameCount == 7);
        Assert.AreEqual(file.FirstFrame, tick2);
        Assert.AreEqual(file.LastFrame, tick5);

        Assert.IsTrue(5 == file.DeleteFramesBefore(99));
        Assert.IsTrue(file.FrameCount == 2);
        Assert.AreEqual(file.FirstFrame, tick5);
        Assert.AreEqual(file.LastFrame, tick5);

        Assert.IsTrue(2 == file.DeleteFramesAfter(100));
        Assert.IsTrue(file.FrameCount == 0);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);
    }
}
