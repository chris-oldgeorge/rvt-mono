using System.Text.Json;
using System.Xml.Linq;

namespace MyAtmMonitorTests.Architecture;

[TestClass]
public sealed class CommonPackageBoundaryTests
{
    private const string ExpectedRvtVersion = "0.2.0-rc.1";
    private const string CommonProject = "libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj";
    private const string InfrastructureProject = "libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj";
    private const string IntegrationTestingProject = "libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj";

    private static readonly string[] RvtPackageIds =
    [
        "Rvt.Monitor.Common",
        "Rvt.Monitor.Common.Infrastructure",
        "Rvt.Monitor.IntegrationTesting"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedSourceReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj"] =
                [CommonProject, InfrastructureProject],
            ["apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj"] =
                [IntegrationTestingProject],
            ["apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj"] =
                [CommonProject, InfrastructureProject],
            ["apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj"] =
                [IntegrationTestingProject],
            ["apps/monitors/omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj"] =
                [CommonProject, InfrastructureProject],
            ["apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj"] =
                [IntegrationTestingProject],
            ["apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj"] =
                [CommonProject, InfrastructureProject],
            ["apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj"] =
                [IntegrationTestingProject],
            ["apps/monitors/reportingmonitor/ReportingMonitor/ReportingMonitor.csproj"] =
                [CommonProject, InfrastructureProject],
            ["apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj"] =
                [CommonProject, IntegrationTestingProject],
            ["apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj"] =
                [CommonProject],
            ["apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj"] =
                [CommonProject],
            ["apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj"] =
                [InfrastructureProject]
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
    public void PackageValidationConsumers_RetainExactPackageBoundary()
    {
        var expectations = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj"] =
                ["Rvt.Monitor.Common", "Rvt.Monitor.Common.Infrastructure"],
            ["libs/rvt-monitor-common/package-validation/TestConsumer/TestConsumer.csproj"] =
                ["Rvt.Monitor.IntegrationTesting"]
        };
        var violations = expectations
            .SelectMany(pair => ValidatePackageValidationConsumer(pair.Key, pair.Value))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void NuGetConfiguration_UsesNuGetOrgAndLocalValidationFeedWithoutCredentials()
    {
        var root = MonoRepositoryRoot();
        foreach (var relative in new[] { "apps/monitors/NuGet.config", "apps/portal/NuGet.config" })
        {
            var contents = File.ReadAllText(Path.Combine(root, relative));
            Assert.Contains("nuget.org", contents, $"{relative} must preserve nuget.org.");
            Assert.DoesNotContain("github.com", contents, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("packageSourceCredentials", contents, StringComparison.OrdinalIgnoreCase);
        }

        var validationConfig = File.ReadAllText(Path.Combine(root, "libs/rvt-monitor-common/NuGet.config"));
        Assert.Contains("nuget.org", validationConfig);
        Assert.Contains("local-rvt", validationConfig);
        Assert.Contains("../../artifacts/packages", validationConfig);
        Assert.Contains("Rvt.*", validationConfig);
        Assert.DoesNotContain("github.com", validationConfig, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("packageSourceCredentials", validationConfig, StringComparison.OrdinalIgnoreCase);
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
    public void PackageInventoryScript_UsesPortableTemporaryDirectoryFallback()
    {
        var script = File.ReadAllText(
            Path.Combine(MonoRepositoryRoot(), "apps/monitors/scripts/report-rvt-package-inventory.sh"));

        Assert.Contains("${TMPDIR:-/tmp}", script);
        Assert.DoesNotContain("${TMPDIR:-/private/tmp}", script);
    }

    [TestMethod]
    public void PackageVerificationScript_UsesRunnerPortableSearchTools()
    {
        var script = File.ReadAllText(
            Path.Combine(MonoRepositoryRoot(), "apps/monitors/scripts/verify-private-package-builds.sh"));

        Assert.DoesNotContain("if rg ", script);
        Assert.DoesNotContain("| rg ", script);
    }

    [TestMethod]
    public void MigrationDocumentation_UsesDurableExactSourceCommitRetrieval()
    {
        var readme = File.ReadAllText(
            Path.Combine(MonoRepositoryRoot(), "apps/monitors/myatmmonitor/README.md"));

        Assert.DoesNotContain("gh run download", readme);
        Assert.Contains("f00d5b8a320945ed08e248da8641ca0c3f7e3b82", readme);
        Assert.Contains("archive \"$source_commit\"", readme);
        Assert.Contains("0b9ec190b7a37b06044842d7a582128bc354a83463ddf5c2b027ec4658154170", readme);
        Assert.Contains("2cd2e4e9403b9c69c9aa282107bcf8221bc3749246163a92d7c17e1eac03769e", readme);
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
            .Where(reference =>
                string.Equals(reference, CommonProject, StringComparison.Ordinal) ||
                string.Equals(reference, InfrastructureProject, StringComparison.Ordinal) ||
                string.Equals(reference, IntegrationTestingProject, StringComparison.Ordinal))
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

    private static IEnumerable<string> ValidatePackageValidationConsumer(
        string relativeProjectPath,
        IReadOnlyCollection<string> expectedPackages)
    {
        var projectPath = Path.Combine(MonoRepositoryRoot(), relativeProjectPath);
        var project = XDocument.Load(projectPath);
        var version = project.Descendants()
            .Single(element => element.Name.LocalName == "RvtPackageVersion")
            .Value
            .Trim();
        if (!string.Equals(version, ExpectedRvtVersion, StringComparison.Ordinal))
        {
            yield return $"{relativeProjectPath}: RvtPackageVersion is {version}, expected {ExpectedRvtVersion}.";
        }

        var artifactLock = project.Descendants()
            .SingleOrDefault(element => element.Name.LocalName == "NuGetLockFilePath");
        if (artifactLock is null ||
            !artifactLock.Value.Contains("artifacts/validation-locks/", StringComparison.Ordinal) ||
            !((string?)artifactLock.Attribute("Condition") ?? string.Empty)
                .Contains("RvtUseArtifactValidationLocks", StringComparison.Ordinal))
        {
            yield return
                $"{relativeProjectPath}: must route opted-in restores to an artifact-scoped validation lock.";
        }

        var actualPackages = project.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => (Element: element, Id: (string?)element.Attribute("Include")))
            .Where(reference => IsRvtPackageId(reference.Id))
            .ToArray();
        var actualIds = actualPackages.Select(reference => reference.Id!).Order(StringComparer.Ordinal).ToArray();
        var expectedIds = expectedPackages.Order(StringComparer.Ordinal).ToArray();
        if (!actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal))
        {
            yield return
                $"{relativeProjectPath}: expected package references [{string.Join(", ", expectedIds)}], actual [{string.Join(", ", actualIds)}].";
        }

        foreach (var reference in actualPackages.Where(reference =>
                     !string.Equals(
                         (string?)reference.Element.Attribute("Version"),
                         "[$(RvtPackageVersion)]",
                         StringComparison.Ordinal)))
        {
            yield return $"{relativeProjectPath}: {reference.Id} must use [$(RvtPackageVersion)].";
        }

        if (project.Descendants().Any(element => element.Name.LocalName == "ProjectReference" &&
                RvtPackageIds.Any(id => ((string?)element.Attribute("Include") ?? string.Empty)
                    .Contains(id, StringComparison.OrdinalIgnoreCase))))
        {
            yield return $"{relativeProjectPath}: package-validation consumer must not source-reference RVT common.";
        }

        var lockPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "packages.lock.json");
        using var lockDocument = JsonDocument.Parse(File.ReadAllText(lockPath));
        foreach (var framework in lockDocument.RootElement.GetProperty("dependencies").EnumerateObject())
        {
            foreach (var expectedPackage in expectedPackages)
            {
                if (!framework.Value.TryGetProperty(expectedPackage, out var package))
                {
                    yield return $"{Relative(lockPath)} ({framework.Name}): missing {expectedPackage}.";
                    continue;
                }

                var requested = package.GetProperty("requested").GetString();
                var resolved = package.GetProperty("resolved").GetString();
                if (!string.Equals(requested, $"[{ExpectedRvtVersion}, {ExpectedRvtVersion}]", StringComparison.Ordinal) ||
                    !string.Equals(resolved, ExpectedRvtVersion, StringComparison.Ordinal))
                {
                    yield return
                        $"{Relative(lockPath)} ({framework.Name}) {expectedPackage}: requested {requested}, resolved {resolved}.";
                }
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
