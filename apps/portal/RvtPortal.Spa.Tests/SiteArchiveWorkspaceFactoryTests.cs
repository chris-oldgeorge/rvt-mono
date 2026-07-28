using RvtPortal.Spa.Adapters.Archive;

namespace RvtPortal.Spa.Tests;

public sealed class SiteArchiveWorkspaceFactoryTests
{
    [Fact]
    public async Task Create_UsesUniqueWorkspacesAndOneStableBlobKeyPerSite()
    {
        Guid siteId = Guid.NewGuid();
        SiteArchiveWorkspaceFactory factory = new();
        await using SiteArchiveWorkspace first = factory.Create(siteId);
        await using SiteArchiveWorkspace second = factory.Create(siteId);

        Assert.NotEqual(first.RootPath, second.RootPath);
        Assert.NotEqual(first.ZipPath, second.ZipPath);
        Assert.Equal($"{siteId:N}/site-archive.zip", first.BlobName);
        Assert.Equal(first.BlobName, second.BlobName);
    }
}
