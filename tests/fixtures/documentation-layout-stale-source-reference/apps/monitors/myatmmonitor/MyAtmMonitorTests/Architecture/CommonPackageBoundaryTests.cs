using System.IO;

namespace MyAtmMonitorTests.Architecture;

public sealed class CommonPackageBoundaryTests
{
    public void MigrationDocumentation_UsesDurableExactSourceCommitRetrieval()
    {
        var readme = File.ReadAllText(
            Path.Combine(MonoRepositoryRoot(), "__STALE_DOCUMENT_PATH__"));
    }

    private static string MonoRepositoryRoot() => "/repository";
}
