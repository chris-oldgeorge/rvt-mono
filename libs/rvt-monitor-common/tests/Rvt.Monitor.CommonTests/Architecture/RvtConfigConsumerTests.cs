namespace Rvt.Monitor.CommonTests.Architecture;

// Summary: Freezes the static RvtConfig consumer surface (review G6): the
// process-wide configuration singleton is legacy, so existing readers are
// allowlisted and the list may only shrink — no new production file may read
// RvtConfig. Retarget new code at injected options instead.
[TestClass]
public sealed class RvtConfigConsumerTests
{
    private const string _rvtConfigDefinition =
        "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Configuration/RvtConfig.cs";

    private static readonly string[] _productionSourceDirectories =
    [
        "apps/monitors",
        "apps/portal",
        "libs/rvt-monitor-common/src"
    ];

    // Shrink-only allowlist (16 files on 2026-07-30, down from the review's 19):
    // removing an entry when a consumer migrates to injected options is welcome;
    // no file may join it.
    private static readonly string[] _allowedConsumers =
    [
        "apps/monitors/airqmonitor/AirQMonitor/api/AirQApi.cs",
        "apps/monitors/airqmonitor/AirQMonitor/api/AirQMonitorServices.cs",
        "apps/monitors/airqmonitor/AirQMonitor/api/AirQService.cs",
        "apps/monitors/airqmonitor/AirQMonitor/api/MonitorApiEndpoints.cs",
        "apps/monitors/myatmmonitor/MyAtmMonitor/api/MonitorApiEndpoints.cs",
        "apps/monitors/myatmmonitor/MyAtmMonitor/api/MyAtmApi.cs",
        "apps/monitors/myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs",
        "apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/MonitorApiEndpoints.cs",
        "apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/OmnidotsApi.cs",
        "apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs",
        "apps/monitors/reportingmonitor/ReportingMonitor/api/ReportingMonitorApi.cs",
        "apps/monitors/svantekmonitor/SvantekMonitor/api/MonitorApiEndpoints.cs",
        "apps/monitors/svantekmonitor/SvantekMonitor/api/SvantekApi.cs",
        "apps/monitors/svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs",
        "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Mqtt/MqttOptions.cs",
        "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rules/AlertActivityTimeDto.cs"
    ];

    [TestMethod]
    public void StaticRvtConfigReadsAreLimitedToTheShrinkOnlyConsumerAllowlist()
    {
        string root = FindRepositoryRoot();
        string[] consumers = [.. _productionSourceDirectories
            .SelectMany(directory => ReadProductionSource(root, directory))
            .Where(file => file.RelativePath != _rvtConfigDefinition)
            .Where(file => file.Text.Contains("RvtConfig.", StringComparison.Ordinal))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        // The guard must keep matching real usage; an empty result means the
        // marker rotted, not that the last consumer left.
        Assert.IsNotEmpty(consumers);

        string[] offenders = [.. consumers
            .Where(path => !_allowedConsumers.Contains(path, StringComparer.Ordinal))];

        CollectionAssert.AreEqual(Array.Empty<string>(), offenders);
    }

    private static IReadOnlyList<(string RelativePath, string Text)> ReadProductionSource(
        string root,
        string relativeDirectory)
    {
        string directory = Path.Combine(root, relativeDirectory);
        return [.. Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGenerated(path))
            .Where(path => !Normalize(path).Contains("Tests/", StringComparison.Ordinal))
            .Select(path => (
                Normalize(Path.GetRelativePath(root, path)),
                File.ReadAllText(path)))];
    }

    private static bool IsGenerated(string path)
    {
        string normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.Ordinal) ||
            normalized.Contains("/obj/", StringComparison.Ordinal) ||
            normalized.Contains("/node_modules/", StringComparison.Ordinal);
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
