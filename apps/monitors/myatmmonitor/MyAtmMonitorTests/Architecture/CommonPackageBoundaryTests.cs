using System.Text.Json;
using System.Xml.Linq;

namespace MyAtmMonitorTests.Architecture;

[TestClass]
public sealed class CommonPackageBoundaryTests
{
    private const string CommonProject = "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj";
    private const string CommunicationAbstractionsProject = "libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj";
    private const string CommunicationProject = "libs/rvt-monitor-common/src/Rvt.Communication/Rvt.Communication.csproj";
    private const string SendGridMailProject = "libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj";
    private const string MicrosoftGraphMailProject = "libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/Rvt.Communication.MicrosoftGraphMail.csproj";
    private const string TransmitSmsProject = "libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/Rvt.Communication.TransmitSms.csproj";
    private const string StorageAbstractionsProject = "libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/Rvt.Storage.Abstractions.csproj";
    private const string StorageLocalProject = "libs/rvt-monitor-common/src/Rvt.Storage.Local/Rvt.Storage.Local.csproj";
    private const string StorageAzureBlobProject = "libs/rvt-monitor-common/src/Rvt.Storage.AzureBlob/Rvt.Storage.AzureBlob.csproj";
    private const string StorageS3Project = "libs/rvt-monitor-common/src/Rvt.Storage.S3/Rvt.Storage.S3.csproj";
    private const string IntegrationTestingProject = "libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj";

    private static readonly string[] RvtPackageIds =
    [
        "Rvt.Monitor.Common",
        "Rvt.Communication.Abstractions",
        "Rvt.Communication",
        "Rvt.Communication.SendGridMail",
        "Rvt.Communication.MicrosoftGraphMail",
        "Rvt.Communication.TransmitSms",
        "Rvt.Monitor.IntegrationTesting",
        "Rvt.Storage.Abstractions",
        "Rvt.Storage.Local",
        "Rvt.Storage.AzureBlob",
        "Rvt.Storage.S3"
    ];

    private static readonly string[] MonitorCommunicationProjects =
    [
        CommunicationAbstractionsProject,
        CommunicationProject,
        SendGridMailProject,
        MicrosoftGraphMailProject,
        TransmitSmsProject
    ];

    private static readonly string[] MonitorStorageProjects =
    [
        StorageAbstractionsProject,
        StorageLocalProject,
        StorageAzureBlobProject,
        StorageS3Project
    ];

    private static readonly string[] RvtSourceProjects =
    [
        CommonProject,
        .. MonitorCommunicationProjects,
        .. MonitorStorageProjects,
        IntegrationTestingProject
    ];

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedSourceReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj"] =
                [CommonProject, .. MonitorCommunicationProjects],
            ["apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj"] =
                [IntegrationTestingProject],
            ["apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj"] =
                [CommonProject, .. MonitorCommunicationProjects],
            ["apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj"] =
                [IntegrationTestingProject],
            ["apps/monitors/omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj"] =
                [CommonProject, .. MonitorCommunicationProjects],
            ["apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj"] =
                [IntegrationTestingProject],
            ["apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj"] =
                [CommonProject, .. MonitorCommunicationProjects, .. MonitorStorageProjects],
            ["apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj"] =
                [IntegrationTestingProject],
            ["apps/monitors/reportingmonitor/ReportingMonitor/ReportingMonitor.csproj"] =
                [CommonProject, .. MonitorCommunicationProjects, .. MonitorStorageProjects],
            ["apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj"] =
                [CommonProject, IntegrationTestingProject],
            ["apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj"] =
                [CommunicationAbstractionsProject],
            ["apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj"] =
                [StorageAbstractionsProject],
            ["apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj"] =
                [CommunicationAbstractionsProject, SendGridMailProject]
        };

    [TestMethod]
    public void ActiveConsumers_MatchApprovedRvtSourceReferenceMatrix()
    {
        var violations = ExpectedSourceReferences
            .SelectMany(pair => ValidateSourceReferenceMatrix(pair.Key, pair.Value))
            .Concat(FindActiveRvtPackageReferences())
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void ActiveConsumerLocks_DoNotRetainDirectRvtPackages()
    {
        var violations = ExpectedSourceReferences.Keys
            .Where(project => project.StartsWith("apps/monitors/", StringComparison.Ordinal))
            .Select(project => Path.ChangeExtension(project, null)!)
            .Select(project => Path.Combine(Path.GetDirectoryName(project)!, "packages.lock.json"))
            .SelectMany(ValidateSourceConsumerLock)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void MonitorCentralPackageManagement_DoesNotBindRvtPackages()
    {
        var props = XDocument.Load(Path.Combine(MonoRepositoryRoot(), "apps/monitors/Directory.Packages.props"));
        var rvtBindings = props.Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(IsRvtPackageId)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), rvtBindings);
    }

    [TestMethod]
    public void InternalRvtProjects_AreSourceOnly()
    {
        var violations = RvtSourceProjects
            .Select(path => (Path: path, Project: XDocument.Load(Path.Combine(MonoRepositoryRoot(), path))))
            .Where(item => !item.Project.Descendants()
                .Any(element => element.Name.LocalName == "IsPackable" &&
                    string.Equals(element.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase)))
            .Select(item => $"{item.Path}: internal RVT source projects must declare IsPackable=false.")
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void NuGetConfiguration_UsesOnlyNuGetOrgWithoutInternalFeedsOrCredentials()
    {
        var root = MonoRepositoryRoot();
        foreach (var relative in new[]
                 {
                     "apps/monitors/NuGet.config",
                     "apps/portal/NuGet.config",
                     "libs/rvt-monitor-common/NuGet.config"
                 })
        {
            var contents = File.ReadAllText(Path.Combine(root, relative));
            Assert.Contains("nuget.org", contents, $"{relative} must preserve nuget.org.");
            Assert.DoesNotContain("github.com", contents, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("packageSourceCredentials", contents, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("local-rvt", contents, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("artifacts/packages", contents, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Rvt.*", contents, StringComparison.OrdinalIgnoreCase);
        }
    }

    [TestMethod]
    public void ConsumerTestProjects_ExplicitlyDeclareIsTestProject()
    {
        var violations = ExpectedSourceReferences.Keys
            .Where(path => Path.GetFileNameWithoutExtension(path).EndsWith("Tests", StringComparison.Ordinal))
            .Where(path => !HasSingleUnconditionalTestProjectDeclaration(
                XDocument.Load(Path.Combine(MonoRepositoryRoot(), path))))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
    }

    [DataTestMethod]
    [DataRow(
        "ConditionalPropertyGroup",
        "<Project><PropertyGroup Condition=\"'$(Configuration)' == 'Debug'\"><IsTestProject>true</IsTestProject></PropertyGroup></Project>")]
    [DataRow(
        "TargetScopedProperty",
        "<Project><Target Name=\"ConfigureTestProject\"><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Target></Project>")]
    [DataRow(
        "TrueThenFalseOverride",
        "<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup><PropertyGroup><IsTestProject>false</IsTestProject></PropertyGroup></Project>")]
    public void TestProjectDeclarationValidation_RejectsAmbiguousDeclarations(
        string fixtureName,
        string projectContents)
    {
        var project = XDocument.Parse(projectContents);
        Assert.IsFalse(
            HasSingleUnconditionalTestProjectDeclaration(project),
            $"{fixtureName} must not satisfy the explicit test-project declaration policy.");
    }

    [TestMethod]
    public void MigrationDocumentation_UsesCheckedInMonorepoPostgreSqlMigrationPaths()
    {
        var readme = File.ReadAllText(
            Path.Combine(MonoRepositoryRoot(), "docs/modules/monitors/myatmmonitor/README.md"));

        Assert.DoesNotContain("gh run download", readme);
        Assert.Contains("libs/rvt-monitor-common/database/migrations/2026-07-15-add-monitor-delivery-outbox.postgres.sql", readme);
        Assert.Contains("apps/monitors/myatmmonitor/database/migrations/2026-07-15-migrate-myatm-outbox-to-shared.postgres.sql", readme);
        Assert.Contains("apps/monitors/myatmmonitor/database/migrations/2026-07-15-rollback-myatm-outbox-to-local.postgres.sql", readme);
        Assert.Contains("0b9ec190b7a37b06044842d7a582128bc354a83463ddf5c2b027ec4658154170", readme);
        Assert.Contains("62d1259161576b6fe4f225f9d356dfd59263f7c0187ca961a8f4a544a1afcba9", readme);
        Assert.Contains("ab81ff9a588d03cf2620025c1b3afcab28dd1c9a952aeb3f52f41d135873cac4", readme);
    }

    private static IEnumerable<string> ValidateSourceReferenceMatrix(
        string relativeProjectPath,
        IReadOnlyCollection<string> expectedReferences)
    {
        var root = MonoRepositoryRoot();
        var projectPath = Path.Combine(root, relativeProjectPath);
        var project = XDocument.Load(projectPath);
        var actualReferences = project.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => ResolveProjectReference(root, projectPath, include!))
            .Where(reference => RvtSourceProjects.Contains(reference, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expected = expectedReferences.Order(StringComparer.Ordinal).ToArray();

        if (!actualReferences.SequenceEqual(expected, StringComparer.Ordinal))
        {
            yield return
                $"{relativeProjectPath}: expected source references [{string.Join(", ", expected)}], actual [{string.Join(", ", actualReferences)}].";
        }
    }

    private static IEnumerable<string> FindActiveRvtPackageReferences()
    {
        var root = MonoRepositoryRoot();
        foreach (var scope in new[] { "apps/monitors", "apps/portal" })
        {
            foreach (var projectPath in Directory.EnumerateFiles(
                         Path.Combine(root, scope),
                         "*.csproj",
                         SearchOption.AllDirectories)
                     .Where(path => !HasGeneratedDirectory(root, path)))
            {
                var project = XDocument.Load(projectPath);
                foreach (var package in project.Descendants()
                             .Where(element => element.Name.LocalName == "PackageReference")
                             .Select(element => (string?)element.Attribute("Include"))
                             .Where(IsRvtPackageId))
                {
                    yield return $"{Relative(projectPath)}: active consumer must not PackageReference {package}.";
                }
            }
        }
    }

    private static IEnumerable<string> ValidateSourceConsumerLock(string relativeLockPath)
    {
        var lockPath = Path.Combine(MonoRepositoryRoot(), relativeLockPath);
        if (!File.Exists(lockPath))
        {
            yield return $"{relativeLockPath}: expected active-consumer lock file is missing.";
            yield break;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(lockPath));
        foreach (var framework in document.RootElement.GetProperty("dependencies").EnumerateObject())
        {
            foreach (var package in framework.Value.EnumerateObject()
                         .Where(package => IsRvtPackageId(package.Name))
                         .Where(package => package.Value.TryGetProperty("type", out var type) &&
                             string.Equals(type.GetString(), "Direct", StringComparison.OrdinalIgnoreCase)))
            {
                yield return
                    $"{relativeLockPath} ({framework.Name}): source consumer retains direct lock for {package.Name}.";
            }
        }
    }

    private static bool HasSingleUnconditionalTestProjectDeclaration(XDocument project)
    {
        var declarations = project.Descendants()
            .Where(element => element.Name.LocalName == "IsTestProject")
            .ToArray();
        if (declarations.Length != 1)
        {
            return false;
        }

        var declaration = declarations[0];
        var propertyGroup = declaration.Parent;
        return propertyGroup?.Name.LocalName == "PropertyGroup" &&
            propertyGroup.Parent == project.Root &&
            propertyGroup.Attribute("Condition") is null &&
            declaration.Attribute("Condition") is null &&
            string.Equals(declaration.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveProjectReference(string root, string projectPath, string include)
    {
        var absolute = Path.GetFullPath(include.Replace('\\', Path.DirectorySeparatorChar), Path.GetDirectoryName(projectPath)!);
        return Path.GetRelativePath(root, absolute).Replace('\\', '/');
    }

    private static bool IsRvtPackageId(string? value) =>
        value is not null && RvtPackageIds.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static bool HasGeneratedDirectory(string root, string path)
    {
        var segments = Path.GetRelativePath(root, path)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment =>
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(MonoRepositoryRoot(), path).Replace('\\', '/');

    private static string MonoRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if ((Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                 File.Exists(Path.Combine(directory.FullName, ".git"))) &&
                File.Exists(Path.Combine(directory.FullName, "Rvt.Mono.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the mono-repository root from test output directory.");
    }
}
