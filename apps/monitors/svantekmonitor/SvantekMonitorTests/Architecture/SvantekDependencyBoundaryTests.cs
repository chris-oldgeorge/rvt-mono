namespace SvantekMonitorTests.Architecture;

[TestClass]
public sealed class SvantekDependencyBoundaryTests
{
    [TestMethod]
    public void ApiPartials_DoNotCallConcreteDatabaseClientFieldDirectly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var apiFiles = Directory.GetFiles(
            Path.Combine(repositoryRoot, "svantekmonitor", "SvantekMonitor", "api"),
            "SvantekApi*.cs",
            SearchOption.TopDirectoryOnly);

        var directCalls = apiFiles
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = Path.GetRelativePath(repositoryRoot, path),
                    Line = index + 1,
                    Text = line
                }))
            .Where(row => row.Text.Contains("dbClient.", StringComparison.Ordinal))
            .Select(row => $"{row.Path}:{row.Line}: {row.Text.Trim()}")
            .ToList();

        CollectionAssert.AreEqual(Array.Empty<string>(), directCalls);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not find repository root from test output directory.");
        return string.Empty;
    }
}
