using Rvt.Storage;

namespace Rvt.Storage.Tests.Abstractions;

[TestClass]
public sealed class ObjectStorageExceptionTests
{
    [TestMethod]
    public void ObjectStorageException_MessageDoesNotReflectInnerExceptionText()
    {
        StorageObjectKey key = StorageObjectKey.Parse("tenant/report.pdf");
        InvalidOperationException innerException = new InvalidOperationException("AccountKey=not-for-output");

        ObjectStorageException exception = new ObjectStorageException(
            StorageFailureKind.AccessDenied,
            "reports",
            key,
            innerException);

        Assert.AreEqual(StorageFailureKind.AccessDenied, exception.Kind);
        Assert.AreEqual("reports", exception.ResourceName);
        Assert.AreSame(key, exception.Key);
        Assert.AreSame(innerException, exception.InnerException);
        Assert.DoesNotContain("AccountKey=not-for-output", exception.Message, StringComparison.Ordinal);
    }
}
