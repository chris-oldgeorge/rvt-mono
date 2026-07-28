using Rvt.Monitor.IntegrationTesting;

namespace MyAtmMonitorTests.Architecture;

[TestClass]
public sealed class ConsumerMessagingBoundaryTests
{
    private static readonly string[] SynchronousCompatibilityCallers =
    [
        "apps/monitors/myatmmonitor/MyAtmMonitor/api/MyAtmRuleProcessor.cs",
        "apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/OmnidotsRuleProcessor.cs"
    ];

    [TestMethod]
    public void ObsoleteSynchronousMessageCallsAreLimitedToConsumerCompatibilityAllowlist()
    {
        string root = RepositoryLayout.Root;
        string[] callers = new[]
            {
                "apps/monitors/myatmmonitor/MyAtmMonitor",
                "apps/monitors/omnidotsmonitor/OmnidotsMonitor"
            }
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
        string directory = Path.Combine(root, relativeDirectory);
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
        string[] segments = Path.GetRelativePath(root, path)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith("Test", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith("Tests", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record SourceFile(string RelativePath, string Text);
}
