namespace Rvt.Monitor.CommonTests.Architecture;

// Summary: Enforces the M11 no-split ruling (2026-07-30): Rvt.Monitor.Common
// stays one package, so the guard against erosion is internal technology
// confinement (review G7) — each infrastructure technology may only be
// referenced from the namespace that owns it.
[TestClass]
public sealed class TechnologyConfinementTests
{
    private const string _commonProjectDirectory = "libs/rvt-monitor-common/src/Rvt.Monitor.Common";

    [TestMethod]
    public void EntityFrameworkAndNpgsqlAreConfinedToDataAndAlertPersistence()
    {
        AssertTechnologyConfinement(
            markers: ["using Microsoft.EntityFrameworkCore", "using Npgsql"],
            allowedDirectories: ["Data/", "Alerts/Persistence/"]);
    }

    [TestMethod]
    public void QuartzIsConfinedToSchedulingHostingAndBackgroundServices()
    {
        AssertTechnologyConfinement(
            markers: ["using Quartz"],
            allowedDirectories: ["Scheduling/", "Hosting/"],
            allowedFiles: ["Alerts/DurableAlertBackgroundService.cs"]);
    }

    [TestMethod]
    public void MqttNetIsConfinedToMqtt()
    {
        AssertTechnologyConfinement(
            markers: ["using MQTTnet"],
            allowedDirectories: ["Mqtt/"]);
    }

    private static void AssertTechnologyConfinement(
        string[] markers,
        string[] allowedDirectories,
        string[]? allowedFiles = null)
    {
        string root = FindRepositoryRoot();
        (string RelativePath, string Text)[] references = [.. ReadProductionSource(root)
            .Where(file => markers.Any(marker => file.Text.Contains(marker, StringComparison.Ordinal)))];

        // The guard must keep matching real usage; an empty result means the
        // markers rotted, not that the technology left the package.
        Assert.IsNotEmpty(references);

        string[] allowedDirectoryPrefixes = [.. allowedDirectories
            .Select(directory => _commonProjectDirectory + "/" + directory)];
        string[] allowedFilePaths = [.. (allowedFiles ?? [])
            .Select(file => _commonProjectDirectory + "/" + file)];

        string[] offenders = [.. references
            .Where(file => !allowedDirectoryPrefixes.Any(prefix =>
                    file.RelativePath.StartsWith(prefix, StringComparison.Ordinal)) &&
                !allowedFilePaths.Contains(file.RelativePath, StringComparer.Ordinal))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(Array.Empty<string>(), offenders);
    }

    private static IReadOnlyList<(string RelativePath, string Text)> ReadProductionSource(string root)
    {
        string directory = Path.Combine(root, _commonProjectDirectory);
        return [.. Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGenerated(path))
            .Select(path => (
                Normalize(Path.GetRelativePath(root, path)),
                File.ReadAllText(path)))];
    }

    private static bool IsGenerated(string path)
    {
        string normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.Ordinal) ||
            normalized.Contains("/obj/", StringComparison.Ordinal);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
