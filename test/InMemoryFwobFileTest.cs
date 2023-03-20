#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozo.Fwob.Abstraction;
using Mozo.Fwob.Exceptions;
using System;
using System.Collections.Generic;
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
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");
        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.AreEqual(0, file.StringCount);
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual("Tick", file.FrameInfo.FrameType);
        Assert.AreEqual(12, file.FrameInfo.FrameLength);
        Assert.AreEqual(0x20ul, file.FrameInfo.FieldTypes);
        Assert.AreEqual(2, file.FrameInfo.Fields.Count);
        Assert.AreEqual(4, file.FrameInfo.Fields[0].FieldLength);
        Assert.AreEqual(8, file.FrameInfo.Fields[1].FieldLength);
        Assert.AreEqual("Time", file.FrameInfo.Fields[0].FieldName);
        Assert.AreEqual("Value", file.FrameInfo.Fields[1].FieldName);
        Assert.AreEqual(FieldType.SignedInteger, file.FrameInfo.Fields[0].FieldType);
        Assert.AreEqual(FieldType.FloatingPoint, file.FrameInfo.Fields[1].FieldType);
        Assert.AreEqual(0, file.Strings.Count);
        Assert.AreEqual("HelloFwob", file.Title);
        Assert.ThrowsException<ArgumentNullException>(() => file.Title = null);
        Assert.ThrowsException<ArgumentException>(() => file.Title = "");
        Assert.ThrowsException<TitleTooLongException>(() => file.Title = "0123456789abcdefg");
        file.Title = "New Title";
        Assert.AreEqual("New Title", file.Title);
    }

    [TestMethod]
    public void TestStringTableNoRandomAccess()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.GetString(0));
        Assert.AreEqual(-1, file.GetIndex("mystr"));
        Assert.AreEqual(0, file.StringCount);
        Assert.AreEqual(0, file.AppendString("mystr"));
        Assert.AreEqual(1, file.StringCount);
        Assert.AreEqual(1, file.AppendString("hello2"));
        Assert.AreEqual(2, file.StringCount);
        Assert.AreEqual(2, file.AppendString("test_string3"));
        Assert.AreEqual(3, file.StringCount);

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

        // duplicate
        Assert.AreEqual(3, file.AppendString("mystr"));
        Assert.AreEqual(4, file.StringCount);
        Assert.AreEqual(4, file.AppendString("hello2"));
        Assert.AreEqual(5, file.StringCount);
        Assert.AreEqual(5, file.AppendString("Hello2"));
        Assert.AreEqual(6, file.StringCount);

        file.DeleteAllStrings();
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
    public void TestFramesBasic()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");
        // no frame
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
        Assert.IsFalse(file.GetFrames(0, 20).Any());
        Assert.IsFalse(file.GetFramesBetween(0, 20).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());
        Assert.IsFalse(file.GetAllFrames().Any());
        Assert.IsFalse(file.GetEnumerator().MoveNext());
        Assert.IsFalse(file.Any());

        // adding the first
        var tick = new Tick { Time = 12, Value = 99.88 };

        Assert.AreEqual(1, file.AppendFrames(tick));
        Assert.IsNull(file.GetKeyAt(-1));
        Assert.AreEqual(12, file.GetKeyAt(0));
        Assert.IsNull(file.GetKeyAt(1));
        Assert.IsNull(file[-1]);
        Assert.AreEqual(tick, file[0]);
        Assert.IsNull(file[1]);
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick);
        Assert.AreEqual(1, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick);
        Assert.IsNull(file.GetFrameAt(1));
        Assert.IsNull(file.GetFrameAt(12));
        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(1, file.GetFrames(12).Count());
        Assert.AreEqual(1, file.GetFrames(12, 1000).Count());
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
        Assert.IsTrue(file.GetEnumerator().MoveNext());
        Assert.AreEqual(1, file.Count());
        Assert.AreEqual(tick, file.First());
        Assert.AreEqual(1, file.AppendFramesTx(tick));
        Assert.AreEqual(3, file.AppendFramesTx(tick, tick, tick));

        // clearing
        file.DeleteAllFrames();
        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.AreEqual(0, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(0));
        Assert.IsNull(file.GetFrameAt(-1));
        Assert.IsNull(file.GetFrameAt(1));
        Assert.IsFalse(file.GetFrames(0).Any());
        Assert.IsFalse(file.GetFrames(0, 20).Any());
        Assert.IsFalse(file.GetFramesBetween(0, 20).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
        Assert.IsFalse(file.GetAllFrames().Any());
        Assert.IsFalse(file.GetEnumerator().MoveNext());
        Assert.IsFalse(file.Any());
    }

    [TestMethod]
    public void TestFramesSameKey()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");
        // adding the first
        var tick = new Tick { Time = 12, Value = 99.88 };
        var tick2 = new Tick { Time = 12, Value = 44456.0111 };
        var tick3 = new Tick { Time = 12, Value = 1234.56 };

        Assert.AreEqual(3, file.AppendFrames(tick, tick2, tick3));
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick3);
        Assert.AreEqual(3, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick);
        Assert.AreEqual(file.GetFrameAt(1), tick2);
        Assert.AreEqual(file.GetFrameAt(2), tick3);
        Assert.IsNull(file.GetFrameAt(3));
        Assert.IsNull(file.GetFrameAt(12));
        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(3, file.GetFrames(12).Count());
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

        // clearing
        file.DeleteAllFrames();
        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.AreEqual(0, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(0));
        Assert.IsNull(file.GetFrameAt(-1));
        Assert.IsNull(file.GetFrameAt(1));
        Assert.IsFalse(file.GetFrames(12).Any());
        Assert.IsFalse(file.GetFramesBetween(0, 20).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
    }

    [TestMethod]
    public void TestFramesMultiKeys()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");
        // adding the first
        var tick = new Tick { Time = 12, Value = 99.88 };
        var tick2 = new Tick { Time = 13, Value = 44456.0111 };
        var tick3 = new Tick { Time = 100, Value = 1234.56 };

        Assert.AreEqual(3, file.AppendFrames(tick, tick2, tick3));
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick3);
        Assert.AreEqual(3, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick);
        Assert.AreEqual(file.GetFrameAt(1), tick2);
        Assert.AreEqual(file.GetFrameAt(2), tick3);
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
        Assert.AreEqual(0, file.GetFramesBefore(0).Count());
        Assert.AreEqual(0, file.GetFramesBefore(11).Count());
        Assert.AreEqual(1, file.GetFramesBefore(12).Count());
        Assert.AreEqual(2, file.GetFramesBefore(13).Count());
        Assert.AreEqual(2, file.GetFramesBefore(14).Count());
        Assert.AreEqual(2, file.GetFramesBefore(99).Count());
        Assert.AreEqual(3, file.GetFramesBefore(100).Count());
        Assert.AreEqual(3, file.GetFramesBefore(101).Count());
        Assert.AreEqual(3, file.GetFramesAfter(-1).Count());
        Assert.AreEqual(3, file.GetFramesAfter(11).Count());
        Assert.AreEqual(3, file.GetFramesAfter(12).Count());
        Assert.AreEqual(2, file.GetFramesAfter(13).Count());
        Assert.AreEqual(1, file.GetFramesAfter(14).Count());
        Assert.AreEqual(1, file.GetFramesAfter(100).Count());
        Assert.AreEqual(0, file.GetFramesAfter(101).Count());

        // clearing
        file.DeleteAllFrames();
        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.AreEqual(0, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(0));
        Assert.IsNull(file.GetFrameAt(-1));
        Assert.IsNull(file.GetFrameAt(1));
        Assert.IsFalse(file.GetFrames(12).Any());
        Assert.IsFalse(file.GetFramesBetween(0, 20).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
    }

    [TestMethod]
    public void TestFramesExceptionsAndMore()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");
        // adding the first
        var tick = new Tick { Time = 12, Value = 99.88 };
        var tick2 = new Tick { Time = 13, Value = 44456.0111 };
        var tick3 = new Tick { Time = 100, Value = 1234.56 };

        Assert.ThrowsException<ArgumentNullException>(() => file.AppendFrames(null));
        Assert.ThrowsException<ArgumentNullException>(() => file.AppendFrames((IEnumerable<Tick>)null));
        Assert.ThrowsException<ArgumentNullException>(() => file.AppendFramesTx(null));
        Assert.ThrowsException<ArgumentNullException>(() => file.AppendFramesTx((IEnumerable<Tick>)null));
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFrames(tick, tick3, tick2));
        Assert.ThrowsException<ArgumentException>(() => file.GetFramesBetween(13, 11).Any());
        Assert.ThrowsException<ArgumentException>(() => file.DeleteFramesBetween(13, 11));

        // only two frames
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick3);
        Assert.AreEqual(2, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick);
        Assert.AreEqual(file.GetFrameAt(1), tick3);
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

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFrames(tick2));
        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick3);
        Assert.AreEqual(2, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick);
        Assert.AreEqual(file.GetFrameAt(1), tick3);
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

        // clearing
        file.DeleteAllFrames();
        Assert.AreEqual(1, file.AppendFrames(tick2));
        Assert.AreEqual(file.FirstFrame, tick2);
        Assert.AreEqual(file.LastFrame, tick2);
        Assert.AreEqual(1, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick2);
        Assert.IsNull(file.GetFrameAt(1));
        Assert.IsNull(file.GetFrameAt(12));
        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(1, file.GetFrames(13).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(12, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(13, 20).Count());
        Assert.AreEqual(0, file.GetFramesBetween(14, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 14).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 13).Count());
        Assert.AreEqual(0, file.GetFramesBetween(-1, 12).Count());
        Assert.AreEqual(1, file.GetFramesAfter(-1).Count());
        Assert.AreEqual(1, file.GetFramesAfter(12).Count());
        Assert.AreEqual(1, file.GetFramesAfter(13).Count());
        Assert.AreEqual(0, file.GetFramesAfter(14).Count());
        Assert.AreEqual(0, file.GetFramesBefore(0).Count());
        Assert.AreEqual(0, file.GetFramesBefore(12).Count());
        Assert.AreEqual(1, file.GetFramesBefore(13).Count());
        Assert.AreEqual(1, file.GetFramesBefore(14).Count());
    }

    [TestMethod]
    public void TestFramesExceptionsAndMoreTransactional()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");
        // adding the first
        var tick = new Tick { Time = 12, Value = 99.88 };
        var tick2 = new Tick { Time = 13, Value = 44456.0111 };
        var tick3 = new Tick { Time = 100, Value = 1234.56 };

        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick, tick3, tick2));

        // no frame
        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.AreEqual(0, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(0));
        Assert.IsNull(file.GetFrameAt(-1));
        Assert.IsNull(file.GetFrameAt(1));
        Assert.IsFalse(file.GetFrames(0).Any());
        Assert.IsFalse(file.GetFramesBetween(0, 20).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());

        Assert.AreEqual(1, file.AppendFrames(tick));
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick3, tick2));
        Assert.AreEqual(1, file.AppendFrames(tick3));
        Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick2));

        Assert.AreEqual(file.FirstFrame, tick);
        Assert.AreEqual(file.LastFrame, tick3);
        Assert.AreEqual(2, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick);
        Assert.AreEqual(file.GetFrameAt(1), tick3);
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

        // clearing
        file.DeleteAllFrames();
        Assert.AreEqual(1, file.AppendFrames(tick2));
        Assert.AreEqual(file.FirstFrame, tick2);
        Assert.AreEqual(file.LastFrame, tick2);
        Assert.AreEqual(1, file.FrameCount);
        Assert.IsNotNull(file.FrameInfo);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(file.GetFrameAt(0), tick2);
        Assert.IsNull(file.GetFrameAt(1));
        Assert.IsNull(file.GetFrameAt(13));
        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(1, file.GetFrames(13).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(12, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(13, 20).Count());
        Assert.AreEqual(0, file.GetFramesBetween(14, 20).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 14).Count());
        Assert.AreEqual(1, file.GetFramesBetween(-1, 13).Count());
        Assert.AreEqual(0, file.GetFramesBetween(-1, 12).Count());
        Assert.AreEqual(1, file.GetFramesAfter(-1).Count());
        Assert.AreEqual(1, file.GetFramesAfter(12).Count());
        Assert.AreEqual(1, file.GetFramesAfter(13).Count());
        Assert.AreEqual(0, file.GetFramesAfter(14).Count());
        Assert.AreEqual(0, file.GetFramesBefore(0).Count());
        Assert.AreEqual(0, file.GetFramesBefore(12).Count());
        Assert.AreEqual(1, file.GetFramesBefore(13).Count());
        Assert.AreEqual(1, file.GetFramesBefore(14).Count());
    }

    [TestMethod]
    public void TestFrameDeletion()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");

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

        Assert.AreEqual(file.DeleteFrames(-100, 12), 2);
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
    }
}
