#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozo.Fwob.Abstraction;
using Mozo.Fwob.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mozo.Fwob.UnitTest;

internal class FrameValidators
{
    internal class Tick
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

    internal static readonly Tick tick12a = new() { Time = 12, Value = 99.88 };
    internal static readonly Tick tick12b = new() { Time = 12, Value = 44456.0111 };
    internal static readonly Tick tick12c = new() { Time = 12, Value = 1234.56 };
    internal static readonly Tick tick13 = new() { Time = 13, Value = 44456.0111 };
    internal static readonly Tick tick14 = new() { Time = 14, Value = 44456.0111 };
    internal static readonly Tick tick15 = new() { Time = 15, Value = 44456.0111 };
    internal static readonly Tick tick100 = new() { Time = 100, Value = 77234.56 };

    internal static void ValidateFileBasic(AbstractFwobFile<Tick, int> file)
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

        Assert.IsNotNull(file.Strings);
        Assert.AreEqual(0, file.Strings.Count);

        Assert.AreEqual("HelloFwob", file.Title);
        ArgumentNullException exception1 = Assert.ThrowsException<ArgumentNullException>(() => file.Title = null);
        Assert.AreEqual("value", exception1.ParamName);

        ArgumentException exception2 = Assert.ThrowsException<ArgumentException>(() => file.Title = "");
        Assert.AreEqual("value", exception2.ParamName);

        TitleTooLongException exception3 = Assert.ThrowsException<TitleTooLongException>(() => file.Title = "0123456789abcdefg");
        Assert.AreEqual("0123456789abcdefg", exception3.Title);
        Assert.AreEqual(17, exception3.TitleLength);

        file.Title = "New Title";
        Assert.AreEqual("New Title", file.Title);
    }

    /// <summary>
    /// Add exactly one frame <see cref="tick12a"/> to the collection.
    /// </summary>
    internal static void AddOneFrame(IFrameCollection<Tick, int> file, int count = 1)
    {
        Assert.IsTrue(count > 0);

        ArgumentNullException exception1 = Assert.ThrowsException<ArgumentNullException>(() => file.AppendFrames(null));
        Assert.AreEqual("frames", exception1.ParamName);

        ArgumentNullException exception2 = Assert.ThrowsException<ArgumentNullException>(() => file.AppendFrames((IEnumerable<Tick>)null));
        Assert.AreEqual("frames", exception2.ParamName);
        Assert.AreEqual(0, file.AppendFrames());
        for (int i = 0; i < count; i++)
        {
            Assert.AreEqual(1, file.AppendFrames(tick12a));
        }
    }

    /// <summary>
    /// Add exactly three frames <see cref="tick12a"/>, <see cref="tick12b"/>, <see cref="tick12c"/> to the collection.
    /// </summary>
    internal static void AddFramesSameKey(IFrameCollection<Tick, int> file)
    {
        Assert.AreEqual(3, file.AppendFrames(tick12a, tick12b, tick12c));
    }

    /// <summary>
    /// Add exactly three frames <see cref="tick12a"/>, <see cref="tick13"/>, <see cref="tick100"/> to the collection.
    /// </summary>
    internal static void AddFramesMultiKeys(IFrameCollection<Tick, int> file)
    {
        Assert.AreEqual(3, file.AppendFrames(tick12a, tick13, tick100));
    }

    /// <summary>
    /// Try to add incorrectly ordered frames <see cref="tick12a"/>, <see cref="tick100"/>, <see cref="tick13"/> to the collection.
    /// </summary>
    internal static void AddFramesPartially(IFrameCollection<Tick, int> file, string? fileName = null)
    {
        KeyOrderViolationException exception1 = Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFrames(tick12a, tick100, tick13));
        Assert.AreEqual(fileName, exception1.FileName);
    }

    /// <summary>
    /// Try to add exactly one frame <see cref="tick13"/> to the collection.
    /// </summary>
    internal static void AddFramesPartially2(IFrameCollection<Tick, int> file, string? fileName = null)
    {
        KeyOrderViolationException exception1 = Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFrames(tick13));
        Assert.AreEqual(fileName, exception1.FileName);
    }

    /// <summary>
    /// Validate methods and properties.
    /// Assuming the collection contains exactly zero frame.
    /// </summary>
    /// <param name="file"></param>
    internal static void ValidateNoFrame(IFrameQueryable<Tick, int> file, string? fileName = null)
    {
        Assert.IsNotNull(file);

        Assert.IsNull(file.FirstFrame);
        Assert.IsNull(file.LastFrame);
        Assert.AreEqual(0, file.FrameCount);

        Assert.AreEqual(0, file.LowerBoundOf(-1000));
        Assert.AreEqual(0, file.LowerBoundOf(1000));

        Assert.AreEqual(0, file.UpperBoundOf(-1000));
        Assert.AreEqual(0, file.UpperBoundOf(1000));

        Assert.AreEqual((0, 0), file.EqualRangeOf(-1000));
        Assert.AreEqual((0, 0), file.EqualRangeOf(1000));

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

        ArgumentException exception1 = Assert.ThrowsException<ArgumentException>(() => file.GetFramesBetween(12, 0).Any());
        Assert.AreEqual("lastKey", exception1.ParamName);
        Assert.IsFalse(file.GetFramesBetween(0, 12).Any());

        Assert.IsFalse(file.GetFramesBefore(-1000).Any());
        Assert.IsFalse(file.GetFramesBefore(0).Any());
        Assert.IsFalse(file.GetFramesBefore(1000).Any());

        Assert.IsFalse(file.GetFramesAfter(-1000).Any());
        Assert.IsFalse(file.GetFramesAfter(0).Any());
        Assert.IsFalse(file.GetFramesAfter(1000).Any());

        Assert.IsFalse(file.GetAllFrames().Any());
        Assert.IsFalse(file.GetEnumerator().MoveNext());
        Assert.IsFalse(file.Any());
    }

    /// <summary>
    /// Validate methods and properties.
    /// Assuming the collection contains exactly <paramref name="count"/> duplicates of one frame: <see cref="tick12a"/>.
    /// </summary>
    /// <param name="file"></param>
    internal static void ValidateOneFrame(IFrameQueryable<Tick, int> file, int count = 1)
    {
        Assert.IsNotNull(file);
        Assert.IsTrue(count > 0);

        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick12a, file.LastFrame);
        Assert.AreEqual(count, file.FrameCount);

        Assert.AreEqual(0, file.LowerBoundOf(-1000));
        Assert.AreEqual(0, file.LowerBoundOf(11));
        Assert.AreEqual(0, file.LowerBoundOf(12));
        Assert.AreEqual(count, file.LowerBoundOf(13));
        Assert.AreEqual(count, file.LowerBoundOf(1000));

        Assert.AreEqual(0, file.UpperBoundOf(-1000));
        Assert.AreEqual(0, file.UpperBoundOf(11));
        Assert.AreEqual(count, file.UpperBoundOf(12));
        Assert.AreEqual(count, file.UpperBoundOf(13));
        Assert.AreEqual(count, file.UpperBoundOf(1000));

        Assert.AreEqual((0, 0), file.EqualRangeOf(-1000));
        Assert.AreEqual((0, 0), file.EqualRangeOf(11));
        Assert.AreEqual((0, count), file.EqualRangeOf(12));
        Assert.AreEqual((count, count), file.EqualRangeOf(13));
        Assert.AreEqual((count, count), file.EqualRangeOf(1000));

        Assert.IsNull(file.GetKeyAt(-1));
        Assert.IsNull(file[-1]);
        Assert.IsNull(file.GetFrameAt(-1));
        for (int i = 0; i < count; i++)
        {
            Assert.AreEqual(12, file.GetKeyAt(i));
            Assert.AreEqual(tick12a, file[i]);
            Assert.AreEqual(tick12a, file.GetFrameAt(i));
        }
        Assert.IsNull(file.GetKeyAt(count));
        Assert.IsNull(file[count]);
        Assert.IsNull(file.GetFrameAt(count));
        if (count <= 12)
            Assert.IsNull(file.GetFrameAt(12));

        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(count, file.GetFrames(12).Count());
        Assert.AreEqual(count, file.GetFrames(12, 1000).Count());
        Assert.AreEqual(0, file.GetFrames(-1, 1000).Count());

        Assert.AreEqual(count, file.GetFramesBetween(-1, 20).Count());
        Assert.AreEqual(count, file.GetFramesBetween(11, 20).Count());
        Assert.AreEqual(count, file.GetFramesBetween(12, 20).Count());
        Assert.AreEqual(0, file.GetFramesBetween(13, 20).Count());
        Assert.AreEqual(count, file.GetFramesBetween(-1, 13).Count());
        Assert.AreEqual(count, file.GetFramesBetween(-1, 12).Count());
        Assert.AreEqual(0, file.GetFramesBetween(-1, 11).Count());

        Assert.AreEqual(0, file.GetFramesBefore(0).Count());
        Assert.AreEqual(0, file.GetFramesBefore(11).Count());
        Assert.AreEqual(count, file.GetFramesBefore(12).Count());
        Assert.AreEqual(count, file.GetFramesBefore(13).Count());

        Assert.AreEqual(count, file.GetFramesAfter(-1).Count());
        Assert.AreEqual(count, file.GetFramesAfter(11).Count());
        Assert.AreEqual(count, file.GetFramesAfter(12).Count());
        Assert.AreEqual(0, file.GetFramesAfter(13).Count());

        Assert.AreEqual(count, file.GetAllFrames().Count());
        Assert.IsTrue(file.GetEnumerator().MoveNext());
        Assert.AreEqual(tick12a, file.First());
        Assert.AreEqual(tick12a, file.Last());
        Assert.AreEqual(count, file.Count());
    }

    /// <summary>
    /// Validate methods and properties.
    /// Assuming the collection contains exactly three frames: <see cref="tick12a"/>, <see cref="tick12b"/>, <see cref="tick12c"/>.
    /// </summary>
    /// <param name="file"></param>
    internal static void ValidateFramesSameKey(IFrameQueryable<Tick, int> file)
    {
        Assert.IsNotNull(file);

        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick12c, file.LastFrame);
        Assert.AreEqual(3, file.FrameCount);

        Assert.AreEqual(0, file.LowerBoundOf(-1000));
        Assert.AreEqual(0, file.LowerBoundOf(11));
        Assert.AreEqual(0, file.LowerBoundOf(12));
        Assert.AreEqual(3, file.LowerBoundOf(13));
        Assert.AreEqual(3, file.LowerBoundOf(1000));

        Assert.AreEqual(0, file.UpperBoundOf(-1000));
        Assert.AreEqual(0, file.UpperBoundOf(11));
        Assert.AreEqual(3, file.UpperBoundOf(12));
        Assert.AreEqual(3, file.UpperBoundOf(13));
        Assert.AreEqual(3, file.UpperBoundOf(1000));

        Assert.AreEqual((0, 0), file.EqualRangeOf(-1000));
        Assert.AreEqual((0, 0), file.EqualRangeOf(11));
        Assert.AreEqual((0, 3), file.EqualRangeOf(12));
        Assert.AreEqual((3, 3), file.EqualRangeOf(13));
        Assert.AreEqual((3, 3), file.EqualRangeOf(1000));

        Assert.IsNull(file.GetKeyAt(-1000));
        Assert.IsNull(file.GetKeyAt(-1));
        Assert.AreEqual(12, file.GetKeyAt(0));
        Assert.AreEqual(12, file.GetKeyAt(1));
        Assert.AreEqual(12, file.GetKeyAt(2));
        Assert.IsNull(file.GetKeyAt(3));
        Assert.IsNull(file.GetKeyAt(12));
        Assert.IsNull(file.GetKeyAt(1000));

        Assert.IsNull(file[-1000]);
        Assert.IsNull(file[-1]);
        Assert.AreEqual(tick12a, file[0]);
        Assert.AreEqual(tick12b, file[1]);
        Assert.AreEqual(tick12c, file[2]);
        Assert.IsNull(file[3]);
        Assert.IsNull(file[12]);
        Assert.IsNull(file[1000]);

        Assert.IsNull(file.GetFrameAt(-1000));
        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(tick12a, file.GetFrameAt(0));
        Assert.AreEqual(tick12b, file.GetFrameAt(1));
        Assert.AreEqual(tick12c, file.GetFrameAt(2));
        Assert.IsNull(file.GetFrameAt(3));
        Assert.IsNull(file.GetFrameAt(12));
        Assert.IsNull(file.GetFrameAt(1000));

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

        Assert.AreEqual(3, file.GetAllFrames().Count());
        Assert.IsTrue(file.GetEnumerator().MoveNext());
        Assert.AreEqual(tick12a, file.First());
        Assert.AreEqual(tick12c, file.Last());
        Assert.AreEqual(3, file.Count());
    }

    /// <summary>
    /// Validate methods and properties.
    /// Assuming the collection contains exactly three frames: <see cref="tick12a"/>, <see cref="tick13"/>, <see cref="tick100"/>.
    /// </summary>
    /// <param name="file"></param>
    internal static void ValidateFramesMultiKeys(IFrameQueryable<Tick, int> file)
    {
        Assert.IsNotNull(file);

        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);
        Assert.AreEqual(3, file.FrameCount);

        Assert.AreEqual(0, file.LowerBoundOf(-1000));
        Assert.AreEqual(0, file.LowerBoundOf(11));
        Assert.AreEqual(0, file.LowerBoundOf(12));
        Assert.AreEqual(1, file.LowerBoundOf(13));
        Assert.AreEqual(2, file.LowerBoundOf(14));
        Assert.AreEqual(2, file.LowerBoundOf(99));
        Assert.AreEqual(2, file.LowerBoundOf(100));
        Assert.AreEqual(3, file.LowerBoundOf(101));
        Assert.AreEqual(3, file.LowerBoundOf(1000));

        Assert.AreEqual(0, file.UpperBoundOf(-1000));
        Assert.AreEqual(0, file.UpperBoundOf(11));
        Assert.AreEqual(1, file.UpperBoundOf(12));
        Assert.AreEqual(2, file.UpperBoundOf(13));
        Assert.AreEqual(2, file.UpperBoundOf(14));
        Assert.AreEqual(2, file.UpperBoundOf(99));
        Assert.AreEqual(3, file.UpperBoundOf(100));
        Assert.AreEqual(3, file.UpperBoundOf(101));
        Assert.AreEqual(3, file.UpperBoundOf(1000));

        Assert.AreEqual((0, 0), file.EqualRangeOf(-1000));
        Assert.AreEqual((0, 0), file.EqualRangeOf(11));
        Assert.AreEqual((0, 1), file.EqualRangeOf(12));
        Assert.AreEqual((1, 2), file.EqualRangeOf(13));
        Assert.AreEqual((2, 2), file.EqualRangeOf(14));
        Assert.AreEqual((2, 2), file.EqualRangeOf(99));
        Assert.AreEqual((2, 3), file.EqualRangeOf(100));
        Assert.AreEqual((3, 3), file.EqualRangeOf(101));
        Assert.AreEqual((3, 3), file.EqualRangeOf(1000));

        Assert.IsNull(file.GetKeyAt(-1000));
        Assert.IsNull(file.GetKeyAt(-1));
        Assert.AreEqual(12, file.GetKeyAt(0));
        Assert.AreEqual(13, file.GetKeyAt(1));
        Assert.AreEqual(100, file.GetKeyAt(2));
        Assert.IsNull(file.GetKeyAt(3));
        Assert.IsNull(file.GetKeyAt(12));
        Assert.IsNull(file.GetKeyAt(1000));

        Assert.IsNull(file[-1000]);
        Assert.IsNull(file[-1]);
        Assert.AreEqual(tick12a, file[0]);
        Assert.AreEqual(tick13, file[1]);
        Assert.AreEqual(tick100, file[2]);
        Assert.IsNull(file[3]);
        Assert.IsNull(file[12]);
        Assert.IsNull(file[1000]);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(tick12a, file.GetFrameAt(0));
        Assert.AreEqual(tick13, file.GetFrameAt(1));
        Assert.AreEqual(tick100, file.GetFrameAt(2));
        Assert.IsNull(file.GetFrameAt(3));
        Assert.IsNull(file.GetFrameAt(12));

        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(1, file.GetFrames(12).Count());
        Assert.AreEqual(1, file.GetFrames(13).Count());
        Assert.AreEqual(1, file.GetFrames(100).Count());
        Assert.AreEqual(1, file.GetFrames(11, 12).Count());
        Assert.AreEqual(1, file.GetFrames(11, 13).Count());
        Assert.AreEqual(2, file.GetFrames(12, 13).Count());
        Assert.AreEqual(2, file.GetFrames(12, 100, 1000).Count());
        Assert.AreEqual(3, file.GetFrames(11, 12, 13, 14, 100, 1000).Count());
        Assert.IsFalse(file.GetFrames(1000).Any());

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

        Assert.AreEqual(3, file.GetAllFrames().Count());
        Assert.IsTrue(file.GetEnumerator().MoveNext());
        Assert.AreEqual(tick12a, file.First());
        Assert.AreEqual(tick100, file.Last());
        Assert.AreEqual(3, file.Count());
    }

    /// <summary>
    /// Validate methods and properties.
    /// Assuming the collection contains exactly two frames: <see cref="tick12a"/>, <see cref="tick100"/>.
    /// </summary>
    /// <param name="file"></param>
    internal static void ValidateFramesPartially(IFrameQueryable<Tick, int> file)
    {
        Assert.IsNotNull(file);

        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);
        Assert.AreEqual(2, file.FrameCount);

        Assert.AreEqual(0, file.LowerBoundOf(-1000));
        Assert.AreEqual(0, file.LowerBoundOf(11));
        Assert.AreEqual(0, file.LowerBoundOf(12));
        Assert.AreEqual(1, file.LowerBoundOf(13));
        Assert.AreEqual(1, file.LowerBoundOf(99));
        Assert.AreEqual(1, file.LowerBoundOf(100));
        Assert.AreEqual(2, file.LowerBoundOf(101));
        Assert.AreEqual(2, file.LowerBoundOf(1000));

        Assert.AreEqual(0, file.UpperBoundOf(-1000));
        Assert.AreEqual(0, file.UpperBoundOf(11));
        Assert.AreEqual(1, file.UpperBoundOf(12));
        Assert.AreEqual(1, file.UpperBoundOf(13));
        Assert.AreEqual(1, file.UpperBoundOf(99));
        Assert.AreEqual(2, file.UpperBoundOf(100));
        Assert.AreEqual(2, file.UpperBoundOf(101));
        Assert.AreEqual(2, file.UpperBoundOf(1000));

        Assert.AreEqual((0, 0), file.EqualRangeOf(-1000));
        Assert.AreEqual((0, 0), file.EqualRangeOf(11));
        Assert.AreEqual((0, 1), file.EqualRangeOf(12));
        Assert.AreEqual((1, 1), file.EqualRangeOf(13));
        Assert.AreEqual((1, 1), file.EqualRangeOf(99));
        Assert.AreEqual((1, 2), file.EqualRangeOf(100));
        Assert.AreEqual((2, 2), file.EqualRangeOf(101));
        Assert.AreEqual((2, 2), file.EqualRangeOf(1000));

        Assert.IsNull(file.GetKeyAt(-1000));
        Assert.IsNull(file.GetKeyAt(-1));
        Assert.AreEqual(12, file.GetKeyAt(0));
        Assert.AreEqual(100, file.GetKeyAt(1));
        Assert.IsNull(file.GetKeyAt(2));
        Assert.IsNull(file.GetKeyAt(12));
        Assert.IsNull(file.GetKeyAt(1000));

        Assert.IsNull(file[-1000]);
        Assert.IsNull(file[-1]);
        Assert.AreEqual(tick12a, file[0]);
        Assert.AreEqual(tick100, file[1]);
        Assert.IsNull(file[2]);
        Assert.IsNull(file[12]);
        Assert.IsNull(file[1000]);

        Assert.IsNull(file.GetFrameAt(-1));
        Assert.AreEqual(tick12a, file.GetFrameAt(0));
        Assert.AreEqual(tick100, file.GetFrameAt(1));
        Assert.IsNull(file.GetFrameAt(2));
        Assert.IsNull(file.GetFrameAt(12));

        Assert.IsFalse(file.GetFrames(-1).Any());
        Assert.AreEqual(1, file.GetFrames(12).Count());
        Assert.AreEqual(1, file.GetFrames(11, 12).Count());
        Assert.AreEqual(1, file.GetFrames(12, 13).Count());
        Assert.AreEqual(1, file.GetFrames(11, 12, 13).Count());
        Assert.AreEqual(1, file.GetFrames(13, 100, 1000).Count());
        Assert.AreEqual(2, file.GetFrames(11, 12, 13, 100, 1000).Count());

        ArgumentException exception1 = Assert.ThrowsException<ArgumentException>(() => file.GetFramesBetween(20, -1).Count());
        Assert.AreEqual("lastKey", exception1.ParamName);
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

        Assert.AreEqual(2, file.GetAllFrames().Count());
        Assert.IsTrue(file.GetEnumerator().MoveNext());
        Assert.AreEqual(tick12a, file.First());
        Assert.AreEqual(tick100, file.Last());
        Assert.AreEqual(2, file.Count());
    }

    /// <summary>
    /// Validate methods and properties by adding transactionally and deleting frames.
    /// Assuming the collection contains zero frame.
    /// </summary>
    /// <param name="file"></param>
    internal static void ValidateFramesAppendingTx(IFrameCollection<Tick, int> file, string? fileName = null)
    {
        ArgumentNullException exception1 = Assert.ThrowsException<ArgumentNullException>(() => file.AppendFrames(null));
        Assert.AreEqual("frames", exception1.ParamName);
        ArgumentNullException exception2 = Assert.ThrowsException<ArgumentNullException>(() => file.AppendFrames((IEnumerable<Tick>)null));
        Assert.AreEqual("frames", exception2.ParamName);
        ArgumentNullException exception3 = Assert.ThrowsException<ArgumentNullException>(() => file.AppendFramesTx(null));
        Assert.AreEqual("frames", exception3.ParamName);
        ArgumentNullException exception4 = Assert.ThrowsException<ArgumentNullException>(() => file.AppendFramesTx((IEnumerable<Tick>)null));
        Assert.AreEqual("frames", exception4.ParamName);
        Assert.AreEqual(0, file.AppendFramesTx());
        ValidateNoFrame(file);

        KeyOrderViolationException exception5 = Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFrames(tick12a, tick100, tick13));
        Assert.AreEqual(fileName, exception5.FileName);
        ValidateFramesPartially(file);

        // Clearing
        file.DeleteAllFrames();

        KeyOrderViolationException exception6 = Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12a, tick100, tick13));
        Assert.AreEqual(fileName, exception6.FileName);
        ValidateNoFrame(file);

        AddOneFrame(file);
        ValidateOneFrame(file);

        KeyOrderViolationException exception7 = Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick100, tick13));
        Assert.AreEqual(fileName, exception7.FileName);
        ValidateOneFrame(file);

        Assert.AreEqual(1, file.AppendFrames(tick100));
        ValidateFramesPartially(file);

        KeyOrderViolationException exception8 = Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick13));
        Assert.AreEqual(fileName, exception8.FileName);
        ValidateFramesPartially(file);

        KeyOrderViolationException exception9 = Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12a));
        Assert.AreEqual(fileName, exception9.FileName);
        KeyOrderViolationException exception10 = Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12b));
        Assert.AreEqual(fileName, exception10.FileName);
        KeyOrderViolationException exception11 = Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick13));
        Assert.AreEqual(fileName, exception11.FileName);
        ValidateFramesPartially(file);

        // Clearing
        file.DeleteAllFrames();

        AddFramesMultiKeys(file);
        ValidateFramesMultiKeys(file);

        file.DeleteAllFrames();
    }

    /// <summary>
    /// Validate methods and properties by adding frames with different values in the string field.
    /// Assuming the collection contains zero frame.
    /// </summary>
    /// <param name="file"></param>
    internal static void ValidateFrameStringField(IFrameCollection<Tick, int> file, string? fileName = null)
    {
        // Non-transactional
        tick13.Str = null;
        Assert.AreEqual(1, file.AppendFrames(tick13));
        Assert.AreEqual(tick13, file.GetFrameAt(0));
        Assert.AreEqual(tick13, file.LastFrame);
        Assert.AreEqual(1, file.FrameCount);

        tick13.Str = "";
        Assert.AreEqual(1, file.AppendFrames(tick13));
        Assert.AreEqual(tick13, file.GetFrameAt(1));
        Assert.AreEqual(tick13, file.LastFrame);
        Assert.AreEqual(2, file.FrameCount);

        tick13.Str = "a";
        Assert.AreEqual(1, file.AppendFrames(tick13));
        Assert.AreEqual(tick13, file.GetFrameAt(2));
        Assert.AreEqual(tick13, file.LastFrame);
        Assert.AreEqual(3, file.FrameCount);

        tick13.Str = "abcd";
        Assert.AreEqual(1, file.AppendFrames(tick13));
        Assert.AreEqual(tick13, file.GetFrameAt(3));
        Assert.AreEqual(tick13, file.LastFrame);
        Assert.AreEqual(4, file.FrameCount);

        tick13.Str = "abcde";
        StringTooLongException exception1 = Assert.ThrowsException<StringTooLongException>(() => file.AppendFrames(tick13));
        Assert.AreEqual("Str", exception1.FieldName);
        Assert.AreEqual("abcde", exception1.StringLiteral);
        Assert.AreEqual(5, exception1.StringLength);
        Assert.IsNull(file.GetFrameAt(4));

        tick13.Str = "abcd";
        Assert.AreEqual(tick13, file.LastFrame);
        Assert.AreEqual(4, file.FrameCount);
        KeyOrderViolationException exception2 = Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFrames(tick12a));
        Assert.AreEqual(fileName, exception2.FileName);
        Assert.AreEqual(4, file.FrameCount);

        // Transactional
        tick13.Str = null;
        Assert.AreEqual(1, file.AppendFramesTx(tick13));
        Assert.AreEqual(tick13, file.GetFrameAt(0));
        Assert.AreEqual(tick13, file.LastFrame);
        Assert.AreEqual(5, file.FrameCount);

        tick13.Str = "";
        Assert.AreEqual(1, file.AppendFramesTx(tick13));
        Assert.AreEqual(tick13, file.GetFrameAt(1));
        Assert.AreEqual(tick13, file.LastFrame);
        Assert.AreEqual(6, file.FrameCount);

        tick13.Str = "a";
        Assert.AreEqual(1, file.AppendFramesTx(tick13));
        Assert.AreEqual(tick13, file.GetFrameAt(2));
        Assert.AreEqual(tick13, file.LastFrame);
        Assert.AreEqual(7, file.FrameCount);

        tick13.Str = "abcd";
        Assert.AreEqual(1, file.AppendFramesTx(tick13));
        Assert.AreEqual(tick13, file.GetFrameAt(3));
        Assert.AreEqual(tick13, file.LastFrame);
        Assert.AreEqual(8, file.FrameCount);

        tick13.Str = "abcde";
        StringTooLongException exception3 = Assert.ThrowsException<StringTooLongException>(() => file.AppendFramesTx(tick13));
        Assert.AreEqual("Str", exception3.FieldName);
        Assert.AreEqual("abcde", exception3.StringLiteral);
        Assert.AreEqual(5, exception3.StringLength);
        Assert.IsNull(file.GetFrameAt(8));

        tick13.Str = "abcd";
        Assert.AreEqual(tick13, file.LastFrame);
        Assert.AreEqual(8, file.FrameCount);
        KeyOrderViolationException exception4 = Assert.ThrowsException<KeyOrderViolationException>(() => file.AppendFramesTx(tick12a));
        Assert.AreEqual(fileName, exception4.FileName);
        Assert.AreEqual(8, file.FrameCount);

        // Reset
        tick13.Str = null;
    }

    /// <summary>
    /// Validate methods and properties by adding frames with different values in the string field.
    /// Assuming the collection contains zero frame.
    /// </summary>
    /// <param name="file"></param>
    internal static void ValidateFrameStringField2(IFrameCollection<Tick, int> file)
    {
        Assert.AreEqual(8, file.FrameCount);

        tick13.Str = null;
        Assert.AreEqual(tick13, file.GetFrameAt(0));

        tick13.Str = "";
        Assert.AreEqual(tick13, file.GetFrameAt(1));

        tick13.Str = "a";
        Assert.AreEqual(tick13, file.GetFrameAt(2));

        tick13.Str = "abcd";
        Assert.AreEqual(tick13, file.GetFrameAt(3));

        // Reset
        tick13.Str = null;
    }

    /// <summary>
    /// Validate methods and properties by adding and deleting frames using different overloaded methods.
    /// Assuming the collection contains zero frame.
    /// </summary>
    /// <param name="file"></param>
    internal static void ValidateFrameDeletion(IFrameCollection<Tick, int> file)
    {
        Assert.IsNotNull(file);

        Assert.AreEqual(9, file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100));
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(0, file.DeleteFramesAfter(101));
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(2, file.DeleteFramesAfter(100));
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick15, file.LastFrame);

        Assert.AreEqual(5, file.DeleteFramesAfter(13));
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick12a, file.LastFrame);

        Assert.AreEqual(2, file.DeleteFramesAfter(0));
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(9, file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100));
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(9, file.DeleteFramesAfter(0));
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(9, file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100));
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(0, file.DeleteFramesBefore(11));
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(2, file.DeleteFramesBefore(12));
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(tick13, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(5, file.DeleteFramesBefore(99));
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(tick100, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(2, file.DeleteFramesAfter(100));
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(0, file.DeleteFramesBefore(13));
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(0, file.DeleteFramesAfter(13));
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(9, file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100));
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(9, file.DeleteFramesBefore(100));
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(0, file.DeleteAllFrames());
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(9, file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100));
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(9, file.DeleteAllFrames());
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(1, file.AppendFrames(tick13));
        Assert.AreEqual(1, file.FrameCount);
        Assert.AreEqual(tick13, file.FirstFrame);
        Assert.AreEqual(tick13, file.LastFrame);

        Assert.AreEqual(1, file.DeleteAllFrames());
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(9, file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100));
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        ArgumentException exception1 = Assert.ThrowsException<ArgumentException>(() => file.DeleteFramesBetween(15, 14));
        Assert.AreEqual("lastKey", exception1.ParamName);
        Assert.AreEqual(5, file.DeleteFramesBetween(13, 15));
        Assert.AreEqual(4, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(0, file.DeleteFramesBetween(13, 20));
        Assert.AreEqual(4, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(2, file.DeleteFramesBetween(13, 100));
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick12a, file.LastFrame);

        Assert.AreEqual(2, file.DeleteFramesBetween(-100, 12));
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(0, file.DeleteFramesBetween(-100, 12));
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(9, file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100));
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        ArgumentException exception2 = Assert.ThrowsException<ArgumentException>(() => file.DeleteFrames(9, -100, 12));
        Assert.AreEqual("keys", exception2.ParamName);
        Assert.AreEqual(0, file.DeleteFrames(0, 11, 16, 18, 101, 200));
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(2, file.DeleteFrames(-100, 12));
        Assert.AreEqual(7, file.FrameCount);
        Assert.AreEqual(tick13, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(1, file.DeleteFrames(-100, 14, 200));
        Assert.AreEqual(6, file.FrameCount);
        Assert.AreEqual(tick13, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(2, file.DeleteFrames(15));
        Assert.AreEqual(4, file.FrameCount);
        Assert.AreEqual(tick13, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(2, file.DeleteFrames(100));
        Assert.AreEqual(2, file.FrameCount);
        Assert.AreEqual(tick13, file.FirstFrame);
        Assert.AreEqual(tick13, file.LastFrame);

        Assert.AreEqual(2, file.DeleteFrames(0, 12, 13, 14, 15, 16, 100, 200));
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(0, file.DeleteFrames(0, 12, 13, 14, 15, 16, 100, 200));
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);

        Assert.AreEqual(9, file.AppendFrames(tick12a, tick12a, tick13, tick13, tick14, tick15, tick15, tick100, tick100));
        Assert.AreEqual(9, file.FrameCount);
        Assert.AreEqual(tick12a, file.FirstFrame);
        Assert.AreEqual(tick100, file.LastFrame);

        Assert.AreEqual(6, file.DeleteFrames(0, 11, 12, 13, 17, 19, 100, 200));
        Assert.AreEqual(3, file.FrameCount);
        Assert.AreEqual(tick14, file.FirstFrame);
        Assert.AreEqual(tick15, file.LastFrame);

        Assert.AreEqual(3, file.DeleteAllFrames());
        Assert.AreEqual(0, file.FrameCount);
        Assert.AreEqual(file.FirstFrame, null);
        Assert.AreEqual(file.LastFrame, null);
    }
}

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
