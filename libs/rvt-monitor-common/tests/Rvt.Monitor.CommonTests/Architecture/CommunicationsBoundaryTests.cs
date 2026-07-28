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

    private static readonly string[] SynchronousCompatibilityCallers =
    [
        "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rules/RuleAlertNotificationDispatcher.cs"
    ];

    [TestMethod]
    public void CommonContainsNoLegacyTransportOrProviderPackage()
    {
        var root = FindRepositoryRoot();
        foreach (var relativePath in LegacyTransportFiles)
        {
            Assert.IsFalse(File.Exists(Path.Combine(root, relativePath)));
        }

        var commonProject = File.ReadAllText(Path.Combine(
            root,
            "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj"));
        Assert.DoesNotContain("PackageReference Include=\"SendGrid\"", commonProject);

        var commonSource = ReadProductionSource(root, "libs/rvt-monitor-common/src/Rvt.Monitor.Common");
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
        var root = FindRepositoryRoot();
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

        var offenders = neutralProjects
            .SelectMany(project => ReadProductionSource(root, project))
            .Where(file => vendorMarkers.Any(marker =>
                file.Text.Contains(marker, StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), offenders);
    }

    [TestMethod]
    public void RemovedInfrastructureProjectIsAbsentFromActiveSourceAndSolutions()
    {
        var root = FindRepositoryRoot();
        var removedIdentity = string.Concat("Rvt.Monitor.Common.", "Infrastructure");
        var removedProject = Path.Combine(
            root,
            "libs/rvt-monitor-common/src",
            removedIdentity);

        Assert.IsFalse(Directory.Exists(removedProject));

        var activeReferences = new[]
            {
                "libs/rvt-monitor-common/src",
                "apps/monitors",
                "apps/portal"
            }
            .SelectMany(relative => ReadProductionSource(root, relative))
            .Where(file => file.Text.Contains(removedIdentity, StringComparison.Ordinal))
            .Select(file => file.RelativePath)
            .Concat(new[]
            {
                "libs/rvt-monitor-common/rvt-common.sln",
                "Rvt.Mono.slnx"
            }
            .Where(relative => File.ReadAllText(Path.Combine(root, relative))
                .Contains(removedIdentity, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), activeReferences);
    }

    [TestMethod]
    public void ObsoleteSynchronousMessageCallsAreLimitedToExplicitCompatibilityAllowlist()
    {
        var root = FindRepositoryRoot();
        var callers = ReadProductionSource(root, "libs/rvt-monitor-common/src/Rvt.Monitor.Common")
            .Where(file => file.Text.Contains(".Sendmessage(", StringComparison.Ordinal) ||
                file.Text.Contains(".SendMessage(", StringComparison.Ordinal))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            SynchronousCompatibilityCallers.Order(StringComparer.Ordinal).ToArray(),
            callers);
    }

    private static IReadOnlyList<(string RelativePath, string Text)> ReadProductionSource(
        string root,
        string? relativeDirectory = null)
    {
        var directory = relativeDirectory is null ? root : Path.Combine(root, relativeDirectory);
        return Directory
            .EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) ||
                path.EndsWith(".csproj", StringComparison.Ordinal))
            .Where(path => !IsGenerated(path))
            .Where(path => !Normalize(path).Contains("Tests/", StringComparison.Ordinal))
            .Select(path => (
                Normalize(Path.GetRelativePath(root, path)),
                File.ReadAllText(path)))
            .ToArray();
    }

    private static void AssertProviderOwnership(string expectedProject, params string[] markers)
    {
        var root = FindRepositoryRoot();
        var providerReferences = ReadProductionSource(root, "libs/rvt-monitor-common/src")
            .Where(file => markers.Any(marker => file.Text.Contains(marker, StringComparison.Ordinal)))
            .ToArray();

        Assert.IsNotEmpty(providerReferences);
        Assert.IsTrue(
            providerReferences.All(file =>
                file.RelativePath.Contains(expectedProject, StringComparison.Ordinal)),
            string.Join(Environment.NewLine, providerReferences.Select(file => file.RelativePath)));
    }

    private static bool IsGenerated(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.Ordinal) ||
            normalized.Contains("/obj/", StringComparison.Ordinal) ||
            normalized.Contains("/.git/", StringComparison.Ordinal);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

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

        throw new InvalidOperationException("Repository root was not found.");
    }
}
