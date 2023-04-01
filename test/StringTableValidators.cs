#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozo.Fwob.Abstraction;
using System;

namespace Mozo.Fwob.UnitTest;

internal class StringTableValidators
{

    internal static void ValidateStringTableData(IStringTable file)
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

    internal static void ValidateStringTableSequential(IStringTable file)
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

        // Duplicate
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

        // Adding after clearing
        Assert.AreEqual(0, file.AppendString("hello2"));
        Assert.AreEqual(1, file.StringCount);
        Assert.AreEqual("hello2", file.GetString(0));
        Assert.AreEqual(-1, file.GetIndex("mystr"));
        Assert.AreEqual(0, file.GetIndex("hello2"));
        Assert.AreEqual(1, file.StringCount);
    }

    internal static void ValidateStringTableWrite(IStringTable file)
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
}

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
