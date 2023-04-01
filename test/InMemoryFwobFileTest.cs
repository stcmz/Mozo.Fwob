using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Mozo.Fwob.UnitTest.FrameValidators;
using static Mozo.Fwob.UnitTest.StringTableValidators;

namespace Mozo.Fwob.UnitTest;

[TestClass]
public class InMemoryFwobFileTest
{
    [TestMethod]
    public void TestBasicNewObject()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");

        ValidateFileBasic(file);
    }

    [TestMethod]
    public void TestStringTableNoRandomAccess()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");

        ValidateStringTableSequential(file);
    }

    [TestMethod]
    public void TestFramesStringField()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");

        ValidateFrameStringField(file);
        ValidateFrameStringField2(file);
    }

    [TestMethod]
    public void TestFramesBasic()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");

        ValidateNoFrame(file);

        // Adding the first frame
        AddOneFrame(file);
        ValidateOneFrame(file);

        AddOneFrame(file);
        Assert.AreEqual(3, file.AppendFramesTx(tick12a, tick12a, tick12a));

        // Clearing
        file.DeleteAllFrames();
        ValidateNoFrame(file);
    }

    [TestMethod]
    public void TestFramesSameKey()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");

        AddFramesSameKey(file);
        ValidateFramesSameKey(file);

        // Clearing
        file.DeleteAllFrames();
        ValidateNoFrame(file);
    }

    [TestMethod]
    public void TestFramesMultiKeys()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");

        AddFramesMultiKeys(file);
        ValidateFramesMultiKeys(file);

        // Clearing
        file.DeleteAllFrames();
        ValidateNoFrame(file);
    }

    [TestMethod]
    public void TestReadingFramesPartially()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");

        ValidateNoFrame(file);
        AddFramesPartially(file);
        AddFramesPartially2(file);
        ValidateFramesPartially(file);

        AddFramesPartially2(file);
        ValidateFramesPartially(file);
    }

    [TestMethod]
    public void TestFramesTransactional()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");

        ValidateFramesAppendingTx(file);
    }

    [TestMethod]
    public void TestFrameDeletion()
    {
        InMemoryFwobFile<Tick, int> file = new("HelloFwob");

        ValidateFrameDeletion(file);
    }
}
