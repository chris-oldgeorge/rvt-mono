namespace Rvt.Monitor.CommonTests.Architecture;

/// <summary>
/// Pins the five monitor hosts' communications composition to one shape.
/// </summary>
/// <remarks>
/// The provider split (docs/architecture/rvt-monitor-common/communications.md)
/// deliberately makes each host own provider selection: no facade or
/// meta-package, no vendor names in the neutral projects, and the
/// source-boundary guard requires every host to reference the adapter projects
/// directly. The <c>AddEmailProvider</c> method is therefore intentionally
/// repeated per host, and the cost of that decision is drift. These tests
/// convert the repetition into a contract: any copy that diverges — a changed
/// default, a reordered key, a reworded error — fails here, naming the host,
/// while a coordinated change to all five stays a normal edit.
/// </remarks>
[TestClass]
public sealed class HostCommunicationsCompositionParityTests
{
    private static readonly string[] _hostServiceFiles =
    [
        "apps/monitors/airqmonitor/AirQMonitor/api/AirQMonitorServices.cs",
        "apps/monitors/myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs",
        "apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs",
        "apps/monitors/reportingmonitor/ReportingMonitor/api/ReportingMonitorServices.cs",
        "apps/monitors/svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs"
    ];

    private const string _methodStartMarker = "    private static void AddEmailProvider(IServiceCollection services, IConfiguration configuration)";

    private const string _registrationBlock =
        "        services.AddRvtCommunication();\n" +
        "        AddEmailProvider(services, configuration);\n" +
        "        services.AddTransmitSms(configuration);";

    [TestMethod]
    public void EveryHostCarriesTheSameEmailProviderMethod()
    {
        string root = FindRepositoryRoot();
        string canonical = ExtractEmailProviderMethod(root, _hostServiceFiles[0]);

        foreach (string relativePath in _hostServiceFiles.Skip(1))
        {
            Assert.AreEqual(
                canonical,
                ExtractEmailProviderMethod(root, relativePath),
                $"{relativePath} drifted from {_hostServiceFiles[0]}. The five copies are " +
                "intentional (each host owns provider selection) but must stay identical; " +
                "apply the change to all five hosts.");
        }
    }

    [TestMethod]
    public void TheEmailProviderMethodKeepsItsSelectionContract()
    {
        string method = ExtractEmailProviderMethod(FindRepositoryRoot(), _hostServiceFiles[0]);

        // The canonical key, the literal legacy alias, and their precedence.
        StringAssert.Contains(method, "configuration[\"RVT:EMAIL_PROVIDER\"]");
        StringAssert.Contains(method, "?? configuration[\"RVT__EMAIL_PROVIDER\"]");
        StringAssert.Contains(method, "?? \"SendGrid\";");

        // Case-insensitive matching for both providers.
        StringAssert.Contains(method, "string.Equals(configuredProvider, \"SendGrid\", StringComparison.OrdinalIgnoreCase)");
        StringAssert.Contains(method, "string.Equals(configuredProvider, \"MicrosoftGraph\", StringComparison.OrdinalIgnoreCase)");

        // The exact failure message; it must not echo the configured value.
        StringAssert.Contains(method, "\"RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.\"");
        Assert.DoesNotContain("configuredProvider}", method, StringComparison.Ordinal);
    }

    [TestMethod]
    public void EveryHostRegistersTheCommunicationsTrioInOrder()
    {
        string root = FindRepositoryRoot();
        foreach (string relativePath in _hostServiceFiles)
        {
            string source = ReadNormalized(root, relativePath);
            StringAssert.Contains(
                source,
                _registrationBlock,
                $"{relativePath} must register AddRvtCommunication, AddEmailProvider, and " +
                "AddTransmitSms together, in that order.");
        }
    }

    private static string ExtractEmailProviderMethod(string root, string relativePath)
    {
        string source = ReadNormalized(root, relativePath);
        int start = source.IndexOf(_methodStartMarker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, $"{relativePath} no longer declares AddEmailProvider.");

        int end = source.IndexOf("\n    }", start, StringComparison.Ordinal);
        Assert.IsGreaterThan(start, end, $"{relativePath}: could not find the end of AddEmailProvider.");
        return source[start..(end + "\n    }".Length)];
    }

    private static string ReadNormalized(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath)).ReplaceLineEndings("\n");

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
