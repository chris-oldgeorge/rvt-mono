namespace Rvt.Monitor.CommonTests.Architecture;

[TestClass]
public sealed class CommunicationsBoundaryTests
{
    private static readonly string[] LegacyTransportFiles =
    [
        "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/Email" + "Sender.cs",
        "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/SmsSender.cs",
        "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/CommsClient.cs",
        "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/ICommsClient.cs",
        "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Sms/TransmitSmsClient.cs"
    ];

    // Emptied on 2026-07-29 when legacy-retirement step 4 deleted
    // RuleAlertNotificationDispatcher, the last synchronous caller in Common.
    private static readonly string[] _synchronousCompatibilityCallers = [];

    private static readonly string[] _activeSourceDirectories =
    [
        "libs/rvt-monitor-common/src",
        "apps/monitors",
        "apps/portal"
    ];

    private static readonly string[] _solutionFiles =
    [
        "libs/rvt-monitor-common/rvt-common.sln",
        "Rvt.Mono.slnx"
    ];

    [TestMethod]
    public void CommonContainsNoLegacyTransportOrProviderPackage()
    {
        string root = FindRepositoryRoot();
        foreach (string relativePath in LegacyTransportFiles)
        {
            Assert.IsFalse(File.Exists(Path.Combine(root, relativePath)));
        }

        string commonProject = File.ReadAllText(Path.Combine(
            root,
            "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj"));
        Assert.DoesNotContain("PackageReference Include=\"SendGrid\"", commonProject);

        IReadOnlyList<(string RelativePath, string Text)> commonSource = ReadProductionSource(root, "libs/rvt-monitor-common/src/Rvt.Monitor.Common");
        Assert.IsFalse(commonSource.Any(file => file.Text.Contains(
            "Email" + "Sender.",
            StringComparison.Ordinal)));
        Assert.IsFalse(commonSource.Any(file => file.Text.Contains(
            "new Sms" + "Sender",
            StringComparison.Ordinal)));
        Assert.IsFalse(commonSource.Any(file => file.Text.Contains("RvtConfig.", StringComparison.Ordinal) &&
            file.RelativePath.Contains("/Communications/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SendGridProviderTypesAndPackageAreConfinedToSendGridMailAdapter()
    {
        AssertProviderOwnership(
            "Rvt.Communication.SendGridMail",
            "using " + "SendGrid",
            "SendGrid.Helpers" + ".Mail",
            "PackageReference Include=\"SendGrid\"");
    }

    [TestMethod]
    public void MicrosoftGraphProviderTypesAndPackagesAreConfinedToMicrosoftGraphMailAdapter()
    {
        AssertProviderOwnership(
            "Rvt.Communication.MicrosoftGraphMail",
            "AzureIdentityGraphAccessTokenProvider",
            "MicrosoftGraphEmailAdapter",
            "graph.microsoft.com");
    }

    [TestMethod]
    public void TransmitSmsProviderTypesAreConfinedToTransmitSmsAdapter()
    {
        AssertProviderOwnership(
            "Rvt.Communication.TransmitSms",
            "TransmitSmsClient",
            "TransmitSmsOptions",
            "TransmitSmsDeliveryAdapter");
    }

    [TestMethod]
    public void ProviderNeutralProjectsContainNoVendorDependencies()
    {
        string root = FindRepositoryRoot();
        string[] neutralProjects =
        [
            "libs/rvt-monitor-common/src/Rvt.Communication.Abstractions",
            "libs/rvt-monitor-common/src/Rvt.Communication",
            "libs/rvt-monitor-common/src/Rvt.Monitor.Common"
        ];
        string[] vendorMarkers =
        [
            "using " + "SendGrid",
            "SendGrid.Helpers" + ".Mail",
            "PackageReference Include=\"SendGrid\"",
            "graph.microsoft.com",
            "AzureIdentityGraphAccessTokenProvider",
            "MicrosoftGraphEmailAdapter",
            "TransmitSmsClient",
            "TransmitSmsOptions",
            "TransmitSmsDeliveryAdapter"
        ];

        string[] offenders = [.. neutralProjects
            .SelectMany(project => ReadProductionSource(root, project))
            .Where(file => vendorMarkers.Any(marker =>
                file.Text.Contains(marker, StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(Array.Empty<string>(), offenders);
    }

    [TestMethod]
    public void RemovedInfrastructureProjectIsAbsentFromActiveSourceAndSolutions()
    {
        string root = FindRepositoryRoot();
        string removedIdentity = string.Concat("Rvt.Monitor.Common.", "Infrastructure");
        string removedProject = Path.Combine(
            root,
            "libs/rvt-monitor-common/src",
            removedIdentity);

        Assert.IsFalse(Directory.Exists(removedProject));

        string[] activeReferences = [.. _activeSourceDirectories
            .SelectMany(relative => ReadProductionSource(root, relative))
            .Where(file => file.Text.Contains(removedIdentity, StringComparison.Ordinal))
            .Select(file => file.RelativePath)
            .Concat(_solutionFiles
            .Where(relative => File.ReadAllText(Path.Combine(root, relative))
                .Contains(removedIdentity, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(Array.Empty<string>(), activeReferences);
    }

    [TestMethod]
    public void ObsoleteSynchronousMessageCallsAreLimitedToExplicitCompatibilityAllowlist()
    {
        string root = FindRepositoryRoot();
        string[] callers = [.. ReadProductionSource(root, "libs/rvt-monitor-common/src/Rvt.Monitor.Common")
            .Where(file => file.Text.Contains(".Sendmessage(", StringComparison.Ordinal) ||
                file.Text.Contains(".SendMessage(", StringComparison.Ordinal))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(
            _synchronousCompatibilityCallers.Order(StringComparer.Ordinal).ToArray(),
            callers);
    }

    private static IReadOnlyList<(string RelativePath, string Text)> ReadProductionSource(
        string root,
        string? relativeDirectory = null)
    {
        string directory = relativeDirectory is null ? root : Path.Combine(root, relativeDirectory);
        return [.. Directory
            .EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) ||
                path.EndsWith(".csproj", StringComparison.Ordinal))
            .Where(path => !IsGenerated(path))
            .Where(path => !Normalize(path).Contains("Tests/", StringComparison.Ordinal))
            .Select(path => (
                Normalize(Path.GetRelativePath(root, path)),
                File.ReadAllText(path)))];
    }

    private static void AssertProviderOwnership(string expectedProject, params string[] markers)
    {
        string root = FindRepositoryRoot();
        (string RelativePath, string Text)[] providerReferences = [.. ReadProductionSource(root, "libs/rvt-monitor-common/src").Where(file => markers.Any(marker => file.Text.Contains(marker, StringComparison.Ordinal)))];

        Assert.IsNotEmpty(providerReferences);
        Assert.IsTrue(
            providerReferences.All(file =>
                file.RelativePath.Contains(expectedProject, StringComparison.Ordinal)),
            string.Join(Environment.NewLine, providerReferences.Select(file => file.RelativePath)));
    }

    private static bool IsGenerated(string path)
    {
        string normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.Ordinal) ||
            normalized.Contains("/obj/", StringComparison.Ordinal) ||
            normalized.Contains("/.git/", StringComparison.Ordinal);
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
