using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rvt.Monitor.Common.Storage;

namespace Rvt.Monitor.CommonTests.Storage;

[TestClass]
public class BlobObjectNameTests
{
    [TestMethod]
    public void Normalize_RejectsTraversalAndRootedNames()
    {
        Assert.ThrowsExactly<ArgumentException>(() => BlobObjectName.Normalize("../x.wav"));
        Assert.ThrowsExactly<ArgumentException>(() => BlobObjectName.Normalize("/x.wav"));
        Assert.ThrowsExactly<ArgumentException>(() => BlobObjectName.Normalize("folder/../../x.wav"));
    }

    [TestMethod]
    public void Normalize_RejectsWindowsDriveRootedNamesRegardlessOfHostPlatform()
    {
        Assert.ThrowsExactly<ArgumentException>(() => BlobObjectName.Normalize("C:\\x.wav"));
        Assert.ThrowsExactly<ArgumentException>(() => BlobObjectName.Normalize("C:/x.wav"));
    }

    [TestMethod]
    public void Normalize_AllowsSafeNestedObjectNames()
    {
        Assert.AreEqual("svantek/audio/abc.wav", BlobObjectName.Normalize("svantek/audio/abc.wav"));
    }
}
