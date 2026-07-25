using RvtPortal.Spa.Adapters.Archive;

namespace RvtPortal.Spa.Tests;

public sealed class SiteArchiveWorkspaceFactoryTests
{
    [Fact]
    public async Task Create_UsesUniqueWorkspacesAndOneStableBlobKeyPerSite()
    {
        var siteId = Guid.NewGuid();
        var factory = new SiteArchiveWorkspaceFactory();
        await using var first = factory.Create(siteId);
        await using var second = factory.Create(siteId);

        Assert.NotEqual(first.RootPath, second.RootPath);
        Assert.NotEqual(first.ZipPath, second.ZipPath);
        Assert.Equal($"{siteId:N}/site-archive.zip", first.BlobName);
        Assert.Equal(first.BlobName, second.BlobName);
    }
}
