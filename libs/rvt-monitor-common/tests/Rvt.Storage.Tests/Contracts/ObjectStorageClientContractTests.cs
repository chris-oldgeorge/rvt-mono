namespace Rvt.Storage.Tests.Contracts;

[TestClass]
public abstract class ObjectStorageClientContractTests
{
    protected abstract Task<IObjectStorageClientFixture> CreateFixtureAsync();

    [TestMethod]
    public async Task GetObjectUri_IsPartOfThePortAndReturnsAnAbsoluteUri()
    {
        // Every adapter implemented this identically before it was added to the
        // port, which forced consumers to bind to concrete adapter types.
        await using IObjectStorageClientFixture fixture = await CreateFixtureAsync();
        StorageObjectKey key = StorageObjectKey.Parse("project-a/source-a/sample.bin");

        Uri uri = fixture.Client.GetObjectUri(key);

        Assert.IsTrue(uri.IsAbsoluteUri, $"Expected an absolute URI but got '{uri}'.");
        Assert.IsNotEmpty(uri.Scheme);
    }

    [TestMethod]
    public async Task GetObjectUri_WithNullKey_ThrowsArgumentNullException()
    {
        await using IObjectStorageClientFixture fixture = await CreateFixtureAsync();

        Assert.ThrowsExactly<ArgumentNullException>(() => fixture.Client.GetObjectUri(null!));
    }

    [TestMethod]
    public async Task WriteAsync_WithNonSeekableContent_ReturnsSameNormalizedKey()
    {
        await using IObjectStorageClientFixture fixture = await CreateFixtureAsync();
        StorageObjectKey key = StorageObjectKey.Parse(" project\\source//sample.bin ");
        await using NonSeekableReadStream content = new([1, 2, 3]);

        StorageWriteResult result = await fixture.Client.WriteAsync(
            new StorageWriteRequest(key, content, "application/octet-stream"), TestContext.CancellationToken);

        Assert.AreSame(key, result.Key);
        Assert.AreEqual("project/source/sample.bin", result.Key.Value);
    }

    [TestMethod]
    public async Task OpenReadAsync_AfterWrite_ReturnsEqualContentAndMetadata()
    {
        await using IObjectStorageClientFixture fixture = await CreateFixtureAsync();
        StorageObjectKey key = StorageObjectKey.Parse("project-a/source-a/sample.bin");
        byte[] expectedContent = [4, 5, 6, 7];

        await WriteAsync(
            fixture.Client,
            key,
            expectedContent,
            "application/octet-stream");

        await using StorageReadResult? result = await fixture.Client.OpenReadAsync(key, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual("application/octet-stream", result.ContentType);
        Assert.AreEqual(expectedContent.Length, result.Length);
        CollectionAssert.AreEqual(expectedContent, await ReadAllBytesAsync(result.Content));
    }

    [TestMethod]
    public async Task OpenReadAsync_WhenKeyIsMissing_ReturnsNull()
    {
        await using IObjectStorageClientFixture fixture = await CreateFixtureAsync();

        StorageReadResult? result = await fixture.Client.OpenReadAsync(
            StorageObjectKey.Parse("project-a/source-a/missing.bin"), TestContext.CancellationToken);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task WriteAsync_WhenKeyExists_ReplacesContent()
    {
        await using IObjectStorageClientFixture fixture = await CreateFixtureAsync();
        StorageObjectKey key = StorageObjectKey.Parse("project-a/source-a/sample.bin");
        await WriteAsync(fixture.Client, key, [1, 2], "first/type");

        await WriteAsync(fixture.Client, key, [8, 9, 10], "second/type");

        await AssertStoredObjectAsync(
            fixture.Client,
            key,
            [8, 9, 10],
            "second/type");
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_ReturnsTrueThenFalse()
    {
        await using IObjectStorageClientFixture fixture = await CreateFixtureAsync();
        StorageObjectKey key = StorageObjectKey.Parse("project-a/source-a/sample.bin");
        await WriteAsync(fixture.Client, key, [1, 2, 3], "application/octet-stream");

        bool existingResult = await fixture.Client.DeleteIfExistsAsync(key, TestContext.CancellationToken);
        bool missingResult = await fixture.Client.DeleteIfExistsAsync(key, TestContext.CancellationToken);

        Assert.IsTrue(existingResult);
        Assert.IsFalse(missingResult);
        Assert.IsNull(await fixture.Client.OpenReadAsync(key, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task WriteAsync_WhenCallerAlreadyCancelled_PreservesExistingObject()
    {
        await using IObjectStorageClientFixture fixture = await CreateFixtureAsync();
        StorageObjectKey key = StorageObjectKey.Parse("project-a/source-a/sample.bin");
        await WriteAsync(fixture.Client, key, [1, 2, 3], "original/type");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Client.WriteAsync(
                new StorageWriteRequest(
                    key,
                    new MemoryStream([9, 9, 9], writable: false),
                    "replacement/type"),
                cancellation.Token));

        await AssertStoredObjectAsync(
            fixture.Client,
            key,
            [1, 2, 3],
            "original/type");
    }

    [TestMethod]
    public async Task OpenReadAsync_WhenCallerAlreadyCancelled_PreservesExistingObject()
    {
        await using IObjectStorageClientFixture fixture = await CreateFixtureAsync();
        StorageObjectKey key = StorageObjectKey.Parse("project-a/source-a/sample.bin");
        await WriteAsync(fixture.Client, key, [1, 2, 3], "original/type");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Client.OpenReadAsync(key, cancellation.Token));

        await AssertStoredObjectAsync(
            fixture.Client,
            key,
            [1, 2, 3],
            "original/type");
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_WhenCallerAlreadyCancelled_PreservesExistingObject()
    {
        await using IObjectStorageClientFixture fixture = await CreateFixtureAsync();
        StorageObjectKey key = StorageObjectKey.Parse("project-a/source-a/sample.bin");
        await WriteAsync(fixture.Client, key, [1, 2, 3], "original/type");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Client.DeleteIfExistsAsync(key, cancellation.Token));

        await AssertStoredObjectAsync(
            fixture.Client,
            key,
            [1, 2, 3],
            "original/type");
    }

    private static Task<StorageWriteResult> WriteAsync(
        IObjectStorageClient client,
        StorageObjectKey key,
        byte[] content,
        string? contentType) =>
        client.WriteAsync(
            new StorageWriteRequest(
                key,
                new MemoryStream(content, writable: false),
                contentType));

    private static async Task AssertStoredObjectAsync(
        IObjectStorageClient client,
        StorageObjectKey key,
        byte[] expectedContent,
        string? expectedContentType)
    {
        await using StorageReadResult? result = await client.OpenReadAsync(key);
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedContentType, result.ContentType);
        Assert.AreEqual(expectedContent.Length, result.Length);
        CollectionAssert.AreEqual(expectedContent, await ReadAllBytesAsync(result.Content));
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream content)
    {
        using MemoryStream buffer = new();
        await content.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private sealed class NonSeekableReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream inner = new(content, writable: false);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    public TestContext TestContext { get; set; } = null!;
}

public interface IObjectStorageClientFixture : IAsyncDisposable
{
    IObjectStorageClient Client { get; }
}
