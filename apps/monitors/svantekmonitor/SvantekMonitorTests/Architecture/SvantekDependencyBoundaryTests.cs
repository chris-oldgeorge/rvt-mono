using Rvt.Monitor.IntegrationTesting;

namespace SvantekMonitorTests.Architecture;

[TestClass]
public sealed class SvantekDependencyBoundaryTests
{
    [TestMethod]
    public void ApiPartials_DoNotCallConcreteDatabaseClientFieldDirectly()
    {
        string repositoryRoot = RepositoryLayout.Root;
        string[] apiFiles = Directory.GetFiles(
            RepositoryLayout.GetPath(
                "apps",
                "monitors",
                "svantekmonitor",
                "SvantekMonitor",
                "api"),
            "SvantekApi*.cs",
            SearchOption.TopDirectoryOnly);

        List<string> directCalls = [.. apiFiles
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = Path.GetRelativePath(repositoryRoot, path),
                    Line = index + 1,
                    Text = line
                }))
            .Where(row => row.Text.Contains("dbClient.", StringComparison.Ordinal))
            .Select(row => $"{row.Path}:{row.Line}: {row.Text.Trim()}")];

        CollectionAssert.AreEqual(Array.Empty<string>(), directCalls);
    }

}
