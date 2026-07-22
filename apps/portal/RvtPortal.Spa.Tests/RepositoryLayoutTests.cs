// File summary: Guards the root-native portal solution, frontend, data, schema, and database layout.
// Major updates:
// - 2026-07-17 pending Added the repository-layout contract for portable verification.

using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public sealed class RepositoryLayoutTests
{
    [Fact]
    public void CurrentRepositoryLayout_ResolvesRequiredRoots()
    {
        Assert.True(File.Exists(RepositoryLayout.SolutionPath));
        Assert.True(File.Exists(Path.Combine(RepositoryLayout.Root, "NuGet.config")));
        Assert.True(File.Exists(Path.Combine(RepositoryLayout.ClientRoot, "package-lock.json")));
        Assert.True(Directory.Exists(RepositoryLayout.DataAccessRoot));
        Assert.True(Directory.Exists(RepositoryLayout.SchemaDeployRoot));
        Assert.True(Directory.Exists(RepositoryLayout.DatabaseRoot));
    }
}
