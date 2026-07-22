using System.Text;
using Rvt.Monitor.Common.Storage;

namespace Rvt.Monitor.CommonTests.Storage;

[TestClass]
public sealed class LocalFileBlobStorageServiceTests
{
    [TestMethod]
    public async Task WriteAsync_WritesContentUnderRootContainerPrefixAndReturnsFileUri()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService(temporaryDirectory.Path, "recordings", "tenant-a/audio");
        var content = Encoding.UTF8.GetBytes("recording-data");

        var result = await service.WriteAsync(new BlobStorageWriteRequest(" clips\\sample.wav ", content));

        var expectedPath = Path.Combine(temporaryDirectory.Path, "recordings", "tenant-a", "audio", "clips", "sample.wav");
        Assert.IsTrue(File.Exists(expectedPath));
        CollectionAssert.AreEqual(content, await File.ReadAllBytesAsync(expectedPath));
        Assert.AreEqual("clips/sample.wav", result.ObjectName);
        Assert.AreEqual(new Uri(expectedPath).AbsoluteUri, result.Uri);
    }

    [TestMethod]
    public async Task WriteAsync_CreatesMissingParentDirectories()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService(temporaryDirectory.Path, "recordings", "tenant-a");

        await service.WriteAsync(new BlobStorageWriteRequest("nested/levels/sample.wav", [1, 2, 3]));

        Assert.IsTrue(Directory.Exists(Path.Combine(temporaryDirectory.Path, "recordings", "tenant-a", "nested", "levels")));
    }

    [TestMethod]
    public async Task WriteAsync_OverwritesExistingFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService(temporaryDirectory.Path, "recordings", "tenant-a");
        var request = new BlobStorageWriteRequest("sample.wav", Encoding.UTF8.GetBytes("first"));
        await service.WriteAsync(request);

        await service.WriteAsync(request with { Content = Encoding.UTF8.GetBytes("replacement") });

        var path = Path.Combine(temporaryDirectory.Path, "recordings", "tenant-a", "sample.wav");
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("replacement"), await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task DeleteAsync_DeletesExistingFileAndIgnoresMissingFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService(temporaryDirectory.Path, "recordings", "tenant-a");
        await service.WriteAsync(new BlobStorageWriteRequest("sample.wav", [1]));

        await service.DeleteAsync("sample.wav");
        await service.DeleteAsync("sample.wav");

        Assert.IsFalse(File.Exists(Path.Combine(temporaryDirectory.Path, "recordings", "tenant-a", "sample.wav")));
    }

    [TestMethod]
    public async Task WriteAndDeleteAsync_RejectTraversalObjectNames()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService(temporaryDirectory.Path, "recordings", "tenant-a");

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.WriteAsync(new BlobStorageWriteRequest("../escape.wav", [1])));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.DeleteAsync("nested/../../escape.wav"));
    }

    [TestMethod]
    public async Task WriteAndDeleteAsync_RejectExistingSymlinksUnderLocalRoot()
    {
        using var localRoot = new TemporaryDirectory();
        using var outsideDirectory = new TemporaryDirectory();
        using var targetRoot = new TemporaryDirectory();
        using var targetOutsideDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(localRoot.Path);
        Directory.CreateDirectory(outsideDirectory.Path);
        Directory.CreateDirectory(Path.Combine(targetRoot.Path, "recordings"));
        Directory.CreateDirectory(targetOutsideDirectory.Path);

        try
        {
            Directory.CreateSymbolicLink(Path.Combine(localRoot.Path, "recordings"), outsideDirectory.Path);
            var outsideTargetPath = Path.Combine(targetOutsideDirectory.Path, "escape.wav");
            await File.WriteAllBytesAsync(outsideTargetPath, [9]);
            File.CreateSymbolicLink(Path.Combine(targetRoot.Path, "recordings", "escape.wav"), outsideTargetPath);
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows symlink creation requires privilege.");
        }
        catch (IOException) when (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows symlink creation is unavailable.");
        }

        var directoryLinkService = CreateService(localRoot.Path, "recordings", string.Empty);
        await Assert.ThrowsExactlyAsync<IOException>(() =>
            directoryLinkService.WriteAsync(new BlobStorageWriteRequest("escape.wav", [1])));
        await Assert.ThrowsExactlyAsync<IOException>(() => directoryLinkService.DeleteAsync("escape.wav"));
        Assert.IsFalse(File.Exists(Path.Combine(outsideDirectory.Path, "escape.wav")));

        var targetLinkService = CreateService(targetRoot.Path, "recordings", string.Empty);
        await Assert.ThrowsExactlyAsync<IOException>(() =>
            targetLinkService.WriteAsync(new BlobStorageWriteRequest("escape.wav", [1])));
        await Assert.ThrowsExactlyAsync<IOException>(() => targetLinkService.DeleteAsync("escape.wav"));
        CollectionAssert.AreEqual(new byte[] { 9 }, await File.ReadAllBytesAsync(Path.Combine(targetOutsideDirectory.Path, "escape.wav")));
    }

    [DataTestMethod]
    [DataRow("../outside-container")]
    [DataRow("/outside-container")]
    [DataRow("C:\\outside-container")]
    [DataRow("\\\\server\\share")]
    public async Task WriteAndDeleteAsync_RejectUnsafeConfiguredContainer(string container)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var outsidePath = Path.Combine(Path.GetDirectoryName(temporaryDirectory.Path)!, "outside-container", "escape.wav");
        var service = CreateService(temporaryDirectory.Path, container, "tenant-a");

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.WriteAsync(new BlobStorageWriteRequest("escape.wav", [1])));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.DeleteAsync("escape.wav"));

        Assert.IsFalse(File.Exists(outsidePath));
    }

    [DataTestMethod]
    [DataRow("../outside-prefix")]
    [DataRow("/outside-prefix")]
    [DataRow("C:\\outside-prefix")]
    [DataRow("\\\\server\\share")]
    public async Task WriteAndDeleteAsync_RejectUnsafeConfiguredPrefix(string prefix)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var outsidePath = Path.Combine(temporaryDirectory.Path, "outside-prefix", "escape.wav");
        var service = CreateService(temporaryDirectory.Path, "recordings", prefix);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.WriteAsync(new BlobStorageWriteRequest("escape.wav", [1])));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.DeleteAsync("escape.wav"));

        Assert.IsFalse(File.Exists(outsidePath));
    }

    private static LocalFileBlobStorageService CreateService(string localRoot, string container, string prefix)
    {
        return new LocalFileBlobStorageService(new BlobStorageOptions
        {
            LocalRoot = localRoot,
            Container = container,
            Prefix = prefix
        });
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rvt-monitor-common-{Guid.NewGuid():N}");
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
