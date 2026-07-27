using Rvt.Storage;

namespace Rvt.Storage.Tests.Abstractions;

[TestClass]
public sealed class StorageObjectKeyTests
{
    [TestMethod]
    [DataRow(" clips\\sample.wav ", "clips/sample.wav")]
    [DataRow("tenant//audio/sample.wav", "tenant/audio/sample.wav")]
    public void Parse_NormalizesSafeObjectNames(string input, string expected)
    {
        Assert.AreEqual(expected, StorageObjectKey.Parse(input).Value);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("/sample.wav")]
    [DataRow("../sample.wav")]
    [DataRow("nested/../../sample.wav")]
    [DataRow("C:\\sample.wav")]
    [DataRow("\\\\server\\share\\sample.wav")]
    public void Parse_RejectsUnsafeObjectNames(string input)
    {
        Assert.ThrowsExactly<ArgumentException>(() => StorageObjectKey.Parse(input));
    }
}
