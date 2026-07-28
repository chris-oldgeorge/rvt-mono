using Rvt.Storage;

namespace Rvt.Storage.Tests.Abstractions;

[TestClass]
public sealed class ObjectStorageClientFactoryTests
{
    [TestMethod]
    public void GetRequiredClient_ReturnsOrdinalNamedRegistration()
    {
        var lowercaseClient = new FakeObjectStorageClient();
        var titlecaseClient = new FakeObjectStorageClient();
        var factory = new ObjectStorageClientFactory(
        [
            new("reports", lowercaseClient),
            new("Reports", titlecaseClient),
        ]);

        Assert.AreSame(lowercaseClient, factory.GetRequiredClient("reports"));
        Assert.AreSame(titlecaseClient, factory.GetRequiredClient("Reports"));
    }

    [TestMethod]
    public void Constructor_RejectsDuplicateResourceNames()
    {
        ObjectStorageClientRegistration[] registrations = new[]
        {
            new ObjectStorageClientRegistration("reports", new FakeObjectStorageClient()),
            new ObjectStorageClientRegistration("reports", new FakeObjectStorageClient()),
        };

        Assert.ThrowsExactly<ArgumentException>(() => new ObjectStorageClientFactory(registrations));
    }

    [TestMethod]
    public void GetRequiredClient_RejectsUnknownResourceWithoutListingOtherResources()
    {
        var factory = new ObjectStorageClientFactory(
        [
            new("customer-secrets", new FakeObjectStorageClient()),
            new("archive-secrets", new FakeObjectStorageClient()),
        ]);

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => factory.GetRequiredClient("missing"));

        Assert.AreEqual("Object storage resource 'missing' is not registered.", exception.Message);
        Assert.DoesNotContain("customer-secrets", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("archive-secrets", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FakeObjectStorageClient : IObjectStorageClient
    {
        public Uri GetObjectUri(StorageObjectKey key) => throw new NotSupportedException();

        public Task<StorageWriteResult> WriteAsync(
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<StorageReadResult?> OpenReadAsync(
            StorageObjectKey key,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DeleteIfExistsAsync(
            StorageObjectKey key,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
