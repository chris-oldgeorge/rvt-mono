namespace MyAtmMonitorTests.Architecture;

[TestClass]
public sealed class ConsumerMessagingBoundaryTests
{
    private static readonly string[] SynchronousCompatibilityCallers =
    [
        "myatmmonitor/MyAtmMonitor/api/MyAtmRuleProcessor.cs",
        "omnidotsmonitor/OmnidotsMonitor/api/OmnidotsRuleProcessor.cs"
    ];

    [TestMethod]
    public void ObsoleteSynchronousMessageCallsAreLimitedToConsumerCompatibilityAllowlist()
    {
        var root = RepositoryRoot();
        var callers = new[] { "myatmmonitor/MyAtmMonitor", "omnidotsmonitor/OmnidotsMonitor" }
            .SelectMany(relativeDirectory => ReadProductionSource(root, relativeDirectory))
            .Where(file => file.Text.Contains(".Sendmessage(", StringComparison.Ordinal) ||
                file.Text.Contains(".SendMessage(", StringComparison.Ordinal))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            SynchronousCompatibilityCallers.Order(StringComparer.Ordinal).ToArray(),
            callers);
    }

    private static IEnumerable<SourceFile> ReadProductionSource(string root, string relativeDirectory)
    {
        var directory = Path.Combine(root, relativeDirectory);
        return Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj")
            .Where(path => !HasExcludedDirectory(directory, path))
            .Select(path => new SourceFile(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                File.ReadAllText(path)));
    }

    private static bool HasExcludedDirectory(string root, string path)
    {
        var segments = Path.GetRelativePath(root, path)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith("Test", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith("Tests", StringComparison.OrdinalIgnoreCase));
    }

    private static string RepositoryRoot()
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

        throw new InvalidOperationException("Could not find repository root from test output directory.");
    }

    private sealed record SourceFile(string RelativePath, string Text);
}
