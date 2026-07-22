namespace Rvt.Monitor.CommonTests.Architecture;

[TestClass]
public sealed class CommunicationsBoundaryTests
{
    private static readonly string[] LegacyTransportFiles =
    [
        "src/Rvt.Monitor.Common/Communications/Email" + "Sender.cs",
        "src/Rvt.Monitor.Common/Communications/SmsSender.cs",
        "src/Rvt.Monitor.Common/Communications/CommsClient.cs",
        "src/Rvt.Monitor.Common/Communications/ICommsClient.cs",
        "src/Rvt.Monitor.Common/Sms/TransmitSmsClient.cs"
    ];

    private static readonly string[] SynchronousCompatibilityCallers =
    [
        "src/Rvt.Monitor.Common/Rules/RuleAlertNotificationDispatcher.cs"
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
            "src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj"));
        Assert.DoesNotContain("PackageReference Include=\"SendGrid\"", commonProject);

        var commonSource = ReadProductionSource(root, "src/Rvt.Monitor.Common");
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
    public void SendGridProviderTypesAndPackageAreConfinedToInfrastructure()
    {
        var root = FindRepositoryRoot();
        var providerReferences = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) ||
                path.EndsWith(".csproj", StringComparison.Ordinal))
            .Where(path => !IsGenerated(path))
            .Select(path => new
            {
                RelativePath = Normalize(Path.GetRelativePath(root, path)),
                Text = File.ReadAllText(path)
            })
            .Where(file => !file.RelativePath.EndsWith(
                "CommunicationsBoundaryTests.cs",
                StringComparison.Ordinal))
            .Where(file => file.Text.Contains("using " + "SendGrid", StringComparison.Ordinal) ||
                file.Text.Contains("SendGrid.Helpers" + ".Mail", StringComparison.Ordinal) ||
                file.Text.Contains("PackageReference Include=\"SendGrid\"", StringComparison.Ordinal))
            .ToArray();

        Assert.IsNotEmpty(providerReferences);
        Assert.IsTrue(providerReferences.All(file =>
            file.RelativePath.Contains("Rvt.Monitor.Common.Infrastructure", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ObsoleteSynchronousMessageCallsAreLimitedToExplicitCompatibilityAllowlist()
    {
        var root = FindRepositoryRoot();
        var callers = ReadProductionSource(root, "src/Rvt.Monitor.Common")
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
