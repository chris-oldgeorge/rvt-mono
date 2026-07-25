using System.Text;
using Rvt.Storage.Local;

namespace Rvt.Storage.Tests.Local;

[TestClass]
public sealed class LocalObjectStorageClientTests
{
    [TestMethod]
    public async Task WriteAndOpenReadAsync_StreamContentAndMetadataUnderConfiguredPath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var client = CreateClient(temporaryDirectory.Path, "recordings", "tenant-a/audio");
        var content = new MemoryStream(
            Encoding.UTF8.GetBytes("recording-data"),
            writable: false);

        var result = await client.WriteAsync(
            new StorageWriteRequest(
                StorageObjectKey.Parse(" clips\\sample.wav "),
                content,
                "audio/wav"));

        Assert.AreEqual("clips/sample.wav", result.Key.Value);
        await using var read = await client.OpenReadAsync(result.Key);
        Assert.IsNotNull(read);
        Assert.AreEqual("audio/wav", read.ContentType);
        Assert.AreEqual(content.Length, read.Length);

        using var copiedContent = new MemoryStream();
        await read.Content.CopyToAsync(copiedContent);
        CollectionAssert.AreEqual(
            Encoding.UTF8.GetBytes("recording-data"),
            copiedContent.ToArray());

        var expectedPath = Path.Combine(
            temporaryDirectory.Path,
            "recordings",
            "tenant-a",
            "audio",
            "clips",
            "sample.wav");
        Assert.AreEqual(new Uri(expectedPath), client.GetObjectUri(result.Key));
    }

    [TestMethod]
    public async Task WriteAsync_CreatesMissingParentDirectories()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var client = CreateClient(temporaryDirectory.Path, "recordings", "tenant-a");

        await client.WriteAsync(CreateRequest("nested/levels/sample.wav", [1, 2, 3]));

        Assert.IsTrue(Directory.Exists(Path.Combine(
            temporaryDirectory.Path,
            "recordings",
            "tenant-a",
            "nested",
            "levels")));
    }

    [TestMethod]
    public async Task WriteAsync_OverwritesObjectAndRemovesTemporaryFiles()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var client = CreateClient(temporaryDirectory.Path, "recordings", "tenant-a");
        var key = StorageObjectKey.Parse("sample.wav");
        await client.WriteAsync(new StorageWriteRequest(
            key,
            new MemoryStream(Encoding.UTF8.GetBytes("first"), writable: false),
            "audio/wav"));

        await client.WriteAsync(new StorageWriteRequest(
            key,
            new MemoryStream(Encoding.UTF8.GetBytes("replacement"), writable: false)));

        await using var read = await client.OpenReadAsync(key);
        Assert.IsNotNull(read);
        Assert.IsNull(read.ContentType);
        using var copiedContent = new MemoryStream();
        await read.Content.CopyToAsync(copiedContent);
        CollectionAssert.AreEqual(
            Encoding.UTF8.GetBytes("replacement"),
            copiedContent.ToArray());

        var targetDirectory = Path.Combine(temporaryDirectory.Path, "recordings", "tenant-a");
        Assert.IsEmpty(Directory.GetFiles(targetDirectory, ".*.tmp"));
        Assert.IsFalse(File.Exists(Path.Combine(targetDirectory, ".sample.wav.content-type")));
    }

    [TestMethod]
    public async Task WriteAsync_WhenContentCopyFails_PreservesObjectAndRemovesTemporaryFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var client = CreateClient(temporaryDirectory.Path, "recordings", string.Empty);
        var key = StorageObjectKey.Parse("sample.wav");
        await client.WriteAsync(new StorageWriteRequest(
            key,
            new MemoryStream(Encoding.UTF8.GetBytes("original"), writable: false),
            "audio/wav"));

        await Assert.ThrowsExactlyAsync<IOException>(() =>
            client.WriteAsync(new StorageWriteRequest(
                key,
                new ThrowingReadStream(Encoding.UTF8.GetBytes("replacement")),
                "audio/mpeg")));

        await using var read = await client.OpenReadAsync(key);
        Assert.IsNotNull(read);
        Assert.AreEqual("audio/wav", read.ContentType);
        using var copiedContent = new MemoryStream();
        await read.Content.CopyToAsync(copiedContent);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("original"), copiedContent.ToArray());

        var targetDirectory = Path.Combine(temporaryDirectory.Path, "recordings");
        Assert.IsEmpty(Directory.GetFiles(targetDirectory, ".*.tmp"));
    }

    [TestMethod]
    public async Task OpenReadAsync_WhenObjectIsMissing_ReturnsNull()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var client = CreateClient(temporaryDirectory.Path, "recordings", string.Empty);

        var result = await client.OpenReadAsync(StorageObjectKey.Parse("missing.wav"));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_ReturnsExistenceAndDeletesContentTypeMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var client = CreateClient(temporaryDirectory.Path, "recordings", string.Empty);
        var key = StorageObjectKey.Parse("sample.wav");
        await client.WriteAsync(new StorageWriteRequest(
            key,
            new MemoryStream([1], writable: false),
            "audio/wav"));

        var firstResult = await client.DeleteIfExistsAsync(key);
        var secondResult = await client.DeleteIfExistsAsync(key);

        Assert.IsTrue(firstResult);
        Assert.IsFalse(secondResult);
        var targetDirectory = Path.Combine(temporaryDirectory.Path, "recordings");
        Assert.IsFalse(File.Exists(Path.Combine(targetDirectory, "sample.wav")));
        Assert.IsFalse(File.Exists(Path.Combine(targetDirectory, ".sample.wav.content-type")));
    }

    [TestMethod]
    [DataRow("../outside-container")]
    [DataRow("/outside-container")]
    [DataRow("C:\\outside-container")]
    [DataRow("\\\\server\\share")]
    [DataRow("")]
    public async Task Operations_RejectUnsafeConfiguredContainer(string container)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var client = CreateClient(temporaryDirectory.Path, container, "tenant-a");
        var key = StorageObjectKey.Parse("escape.wav");

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            client.WriteAsync(CreateRequest(key.Value, [1])));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.OpenReadAsync(key));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.DeleteIfExistsAsync(key));
    }

    [TestMethod]
    [DataRow("../outside-prefix")]
    [DataRow("/outside-prefix")]
    [DataRow("C:\\outside-prefix")]
    [DataRow("\\\\server\\share")]
    public async Task Operations_RejectUnsafeConfiguredPrefix(string prefix)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var client = CreateClient(temporaryDirectory.Path, "recordings", prefix);
        var key = StorageObjectKey.Parse("escape.wav");

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            client.WriteAsync(CreateRequest(key.Value, [1])));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.OpenReadAsync(key));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.DeleteIfExistsAsync(key));
    }

    [TestMethod]
    [DataRow("../escape.wav")]
    [DataRow("nested/../../escape.wav")]
    public void StorageObjectKey_RejectsTraversalBeforeLocalFilesystemAccess(string value)
    {
        using var temporaryDirectory = new TemporaryDirectory();

        Assert.ThrowsExactly<ArgumentException>(() => StorageObjectKey.Parse(value));
        Assert.IsFalse(Directory.Exists(temporaryDirectory.Path));
    }

    [TestMethod]
    public async Task Operations_RejectDirectorySymlinkUnderRoot()
    {
        using var localRoot = new TemporaryDirectory();
        using var outsideDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(localRoot.Path);
        Directory.CreateDirectory(outsideDirectory.Path);

        try
        {
            Directory.CreateSymbolicLink(
                Path.Combine(localRoot.Path, "recordings"),
                outsideDirectory.Path);
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows symlink creation requires privilege.");
        }
        catch (IOException) when (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows symlink creation is unavailable.");
        }

        var client = CreateClient(localRoot.Path, "recordings", string.Empty);
        var key = StorageObjectKey.Parse("escape.wav");

        await Assert.ThrowsExactlyAsync<IOException>(() =>
            client.WriteAsync(CreateRequest(key.Value, [1])));
        await Assert.ThrowsExactlyAsync<IOException>(() => client.OpenReadAsync(key));
        await Assert.ThrowsExactlyAsync<IOException>(() => client.DeleteIfExistsAsync(key));
        Assert.IsFalse(File.Exists(Path.Combine(outsideDirectory.Path, "escape.wav")));
    }

    [TestMethod]
    public async Task Operations_RejectTargetFileSymlink()
    {
        using var localRoot = new TemporaryDirectory();
        using var outsideDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(localRoot.Path, "recordings"));
        Directory.CreateDirectory(outsideDirectory.Path);
        var outsideTargetPath = Path.Combine(outsideDirectory.Path, "escape.wav");
        await File.WriteAllBytesAsync(outsideTargetPath, [9]);

        try
        {
            File.CreateSymbolicLink(
                Path.Combine(localRoot.Path, "recordings", "escape.wav"),
                outsideTargetPath);
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows symlink creation requires privilege.");
        }
        catch (IOException) when (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows symlink creation is unavailable.");
        }

        var client = CreateClient(localRoot.Path, "recordings", string.Empty);
        var key = StorageObjectKey.Parse("escape.wav");

        await Assert.ThrowsExactlyAsync<IOException>(() =>
            client.WriteAsync(CreateRequest(key.Value, [1])));
        await Assert.ThrowsExactlyAsync<IOException>(() => client.OpenReadAsync(key));
        await Assert.ThrowsExactlyAsync<IOException>(() => client.DeleteIfExistsAsync(key));
        CollectionAssert.AreEqual(new byte[] { 9 }, await File.ReadAllBytesAsync(outsideTargetPath));
    }

    [TestMethod]
    public async Task MutatingOperations_WhenAlreadyCancelled_DoNotMutateFilesystem()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var client = CreateClient(temporaryDirectory.Path, "recordings", string.Empty);
        var key = StorageObjectKey.Parse("sample.wav");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            client.WriteAsync(CreateRequest(key.Value, [1]), cancellation.Token));
        Assert.IsFalse(Directory.Exists(temporaryDirectory.Path));

        var targetDirectory = Path.Combine(temporaryDirectory.Path, "recordings");
        Directory.CreateDirectory(targetDirectory);
        var targetPath = Path.Combine(targetDirectory, "sample.wav");
        await File.WriteAllBytesAsync(targetPath, [9]);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            client.DeleteIfExistsAsync(key, cancellation.Token));
        CollectionAssert.AreEqual(new byte[] { 9 }, await File.ReadAllBytesAsync(targetPath));
    }

    private static LocalObjectStorageClient CreateClient(
        string rootPath,
        string container,
        string prefix) =>
        new(
            "recordings",
            new LocalStorageOptions
            {
                RootPath = rootPath,
                Container = container,
                Prefix = prefix,
            });

    private static StorageWriteRequest CreateRequest(string key, byte[] content) =>
        new(
            StorageObjectKey.Parse(key),
            new MemoryStream(content, writable: false));

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"rvt-storage-local-{Guid.NewGuid():N}");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class ThrowingReadStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public override Task CopyToAsync(
            Stream destination,
            int bufferSize,
            CancellationToken cancellationToken) =>
            Task.FromException(new IOException("Simulated content read failure."));
    }
}
