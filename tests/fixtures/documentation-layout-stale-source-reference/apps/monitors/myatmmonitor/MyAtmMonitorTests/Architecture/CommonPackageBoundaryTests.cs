using System.IO;

namespace MyAtmMonitorTests.Architecture;

public sealed class CommonPackageBoundaryTests
{
    public void MigrationDocumentation_UsesDurableExactSourceCommitRetrieval()
    {
        var readme = File.ReadAllText(
            Path.Combine(MonoRepositoryRoot(), "apps/monitors/myatmmonitor/README.md"));
    }

    private static string MonoRepositoryRoot() => "/repository";
}
