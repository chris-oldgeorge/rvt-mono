using Rvt.Storage.Local;

namespace Rvt.Storage.Tests.Local;

/// <summary>
/// The Local adapter previously performed no exception translation at all, so
/// <see cref="IOException"/> and <see cref="UnauthorizedAccessException"/>
/// crossed the port raw and callers switching on
/// <see cref="StorageFailureKind"/> never saw them. The provider-specific
/// contract tests are mock-based and structurally could not catch that.
/// </summary>
[TestClass]
public sealed class LocalObjectStorageFailureContractTests
{
    [TestMethod]
    public async Task WriteAsync_WhenTheRootIsAFile_ReportsThroughThePortContract()
    {
        using var temporary = new TemporaryDirectory();
        var rootAsFile = Path.Combine(temporary.Path, "not-a-directory");
        await File.WriteAllTextAsync(rootAsFile, "occupied");
        var client = new LocalObjectStorageClient(
            "recordings",
            new LocalStorageOptions { RootPath = rootAsFile, Container = "container" });

        var failure = await Assert.ThrowsExactlyAsync<ObjectStorageException>(() =>
            client.WriteAsync(new StorageWriteRequest(
                StorageObjectKey.Parse("sample.bin"),
                new MemoryStream([1, 2, 3], writable: false),
                "application/octet-stream")));

        Assert.AreEqual("recordings", failure.ResourceName);
        Assert.IsNotNull(failure.InnerException);
    }

    [TestMethod]
    public void GetObjectUri_ReturnsAFileUriUnderTheConfiguredRoot()
    {
        using var temporary = new TemporaryDirectory();
        var client = new LocalObjectStorageClient(
            "recordings",
            new LocalStorageOptions { RootPath = temporary.Path, Container = "container" });

        var uri = client.GetObjectUri(StorageObjectKey.Parse("nested/sample.bin"));

        Assert.IsTrue(uri.IsFile, $"Expected a file URI but got '{uri}'.");
        Assert.IsTrue(
            uri.LocalPath.Contains("sample.bin", StringComparison.Ordinal),
            $"Expected the key in the URI but got '{uri.LocalPath}'.");
    }

    [TestMethod]
    public async Task Operations_StillSurfaceArgumentValidationAsArgumentException()
    {
        // Validation failures are caller errors, not storage faults, and stay
        // outside the ObjectStorageException contract by design.
        var client = new LocalObjectStorageClient(
            "recordings",
            new LocalStorageOptions { RootPath = string.Empty, Container = "container" });

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            client.OpenReadAsync(StorageObjectKey.Parse("sample.bin")));
    }

    [TestMethod]
    public async Task Operations_StillSurfaceCallerCancellationAsOperationCanceled()
    {
        using var temporary = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var client = new LocalObjectStorageClient(
            "recordings",
            new LocalStorageOptions { RootPath = temporary.Path, Container = "container" });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            client.OpenReadAsync(StorageObjectKey.Parse("sample.bin"), cancellation.Token));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"rvt-storage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
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
}
