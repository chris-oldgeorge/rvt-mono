using System.Text.Json;
using System.Xml.Linq;

namespace MyAtmMonitorTests.Architecture;

[TestClass]
public sealed class CommonPackageBoundaryTests
{
    private const string ExpectedRvtVersion = "0.2.0-rc.1";

    private static readonly IReadOnlyDictionary<string, string> ExpectedRvtVersionBindings =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Rvt.Monitor.Common"] = "RvtCommonVersion",
            ["Rvt.Monitor.Common.Infrastructure"] = "RvtCommonInfrastructureVersion",
            ["Rvt.Monitor.IntegrationTesting"] = "RvtIntegrationTestingVersion"
        };

    private static readonly string[] ActiveSolutions =
    [
        "rvt-monitors.sln",
        "airqmonitor/airqmonitor.sln",
        "myatmmonitor/myatmmonitor.sln",
        "omnidotsmonitor/omnidotsmonitor.sln",
        "svantekmonitor/svantekmonitor.sln"
    ];

    private static readonly string[] ActiveBoundaryDocuments =
    [
        "AGENTS.md",
        "README.md",
        "observability/README.md",
        "docs/monitor-data-access-migration.md",
        "docs/container-builds.md",
        "docs/release/client-release-runbook.md",
        "myatmmonitor/README.md"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedRvtPackages =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["airqmonitor/AirQMonitor/AirQMonitor.csproj"] =
                ["Rvt.Monitor.Common", "Rvt.Monitor.Common.Infrastructure"],
            ["airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj"] =
                ["Rvt.Monitor.IntegrationTesting"],
            ["myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj"] =
                ["Rvt.Monitor.Common", "Rvt.Monitor.Common.Infrastructure"],
            ["myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj"] =
                ["Rvt.Monitor.IntegrationTesting"],
            ["omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj"] =
                ["Rvt.Monitor.Common", "Rvt.Monitor.Common.Infrastructure"],
            ["omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj"] =
                ["Rvt.Monitor.IntegrationTesting"],
            ["svantekmonitor/SvantekMonitor/SvantekMonitor.csproj"] =
                ["Rvt.Monitor.Common", "Rvt.Monitor.Common.Infrastructure"],
            ["svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj"] =
                ["Rvt.Monitor.IntegrationTesting"],
            ["reportingmonitor/ReportingMonitor/ReportingMonitor.csproj"] =
                ["Rvt.Monitor.Common", "Rvt.Monitor.Common.Infrastructure"],
            ["reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj"] =
                ["Rvt.Monitor.Common", "Rvt.Monitor.IntegrationTesting"],
            ["reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj"] =
                ["Rvt.Monitor.Common"],
            ["reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj"] =
                ["Rvt.Monitor.Common"]
        };

    [TestMethod]
    public void LocalCommonSourceTree_DoesNotExist() =>
        Assert.IsFalse(Directory.Exists(Path.Combine(RepositoryRoot(), "rvt-monitor-common")));

    [TestMethod]
    public void ActiveSolutions_DoNotListRetiredCommonProjects()
    {
        var violations = ActiveSolutions
            .Where(relative => File.ReadAllText(Path.Combine(RepositoryRoot(), relative))
                .Contains("rvt-monitor-common", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void ActiveDocumentation_DoesNotReferenceRetiredCommonSource()
    {
        var violations = ActiveBoundaryDocuments
            .Where(relative => File.ReadAllText(Path.Combine(RepositoryRoot(), relative))
                .Contains("rvt-monitor-common", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void ConsumerProjects_MatchApprovedRvtPackageMatrix()
    {
        var violations = ConsumerProjects()
            .SelectMany(ValidateRvtPackageMatrix)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void ConsumerTestProjects_ExplicitlyDeclareIsTestProject()
    {
        var violations = ConsumerProjects()
            .Where(path => Path.GetFileNameWithoutExtension(path)
                .EndsWith("Tests", StringComparison.Ordinal))
            .Where(path => !HasSingleUnconditionalTestProjectDeclaration(XDocument.Load(path)))
            .Select(Relative)
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
        var fixtureRoot = Path.Combine(RepositoryRoot(), $".test-discovery-boundary-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(fixtureRoot, $"{fixtureName}Tests");
        var projectPath = Path.Combine(projectDirectory, $"{fixtureName}Tests.csproj");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(projectPath, projectContents);

            var exception = Assert.ThrowsExactly<AssertFailedException>(
                ConsumerTestProjects_ExplicitlyDeclareIsTestProject);

            StringAssert.Contains(exception.Message, Relative(projectPath));
        }
        finally
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }
    }

    private static bool HasSingleUnconditionalTestProjectDeclaration(XDocument project)
    {
        var declarations = project
            .Descendants()
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

    [TestMethod]
    public void PackageMatrixValidation_DetectsCaseMismatchedRvtPackageId()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), $"rvt-package-boundary-{Guid.NewGuid():N}.csproj");
        try
        {
            File.WriteAllText(
                projectPath,
                "<Project><ItemGroup><PackageReference Include=\"rvt.monitor.common\" /></ItemGroup></Project>");

            var violations = ValidateRvtPackageMatrix(projectPath).ToArray();

            Assert.AreEqual(1, violations.Length);
            StringAssert.Contains(violations[0], "actual [rvt.monitor.common]");
        }
        finally
        {
            File.Delete(projectPath);
        }
    }

    [TestMethod]
    public void ConsumerProjects_DoNotContainConditionalCommonSourceSwitches()
    {
        var root = RepositoryRoot();
        var paths = ConsumerProjects()
            .Concat(Directory.EnumerateFiles(root, "*.props", SearchOption.TopDirectoryOnly))
            .Concat(Directory.EnumerateFiles(root, "*.targets", SearchOption.TopDirectoryOnly));
        var violations = paths
            .Where(path => File.ReadAllText(path).Contains("UseLocalRvtCommon", StringComparison.OrdinalIgnoreCase) ||
                File.ReadAllText(path).Contains("rvt-monitor-common", StringComparison.OrdinalIgnoreCase))
            .Select(Relative)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void ConsumerProjects_DoNotReferenceLocalCommonProjects()
    {
        var violations = FindLocalCommonProjectViolations(RepositoryRoot())
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            violations,
            string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void LocalProjectIdentityValidation_DetectsRenamedCommonProjects()
    {
        var root = Path.Combine(Path.GetTempPath(), $"rvt-renamed-common-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "renamed-shared"));
        try
        {
            foreach (var identityProperty in new[] { "AssemblyName", "PackageId", "RootNamespace" })
            {
                var project = Path.Combine(root, "renamed-shared", "Harmless.csproj");
                File.WriteAllText(
                    project,
                    $"<Project><PropertyGroup><{identityProperty}>Rvt.Monitor.Common</{identityProperty}></PropertyGroup></Project>");

                var violations = FindLocalCommonProjectViolations(root).ToArray();

                Assert.IsTrue(
                    violations.Any(violation => violation.Contains(identityProperty, StringComparison.Ordinal)),
                    $"Expected {identityProperty} to expose the renamed Common project.{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
            }

            File.WriteAllText(
                Path.Combine(root, "renamed-shared", "Harmless.csproj"),
                "<Project />");
            File.WriteAllText(
                Path.Combine(root, "Consumer.csproj"),
                "<Project><ItemGroup><ProjectReference Include=\"renamed-shared/Rvt.Monitor.Common.csproj\" /></ItemGroup></Project>");

            var referenceViolations = FindLocalCommonProjectViolations(root).ToArray();

            Assert.IsTrue(
                referenceViolations.Any(violation => violation.Contains("ProjectReference", StringComparison.Ordinal)),
                $"Expected the renamed-path ProjectReference to expose the retired Common identity.{Environment.NewLine}{string.Join(Environment.NewLine, referenceViolations)}");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void IntegrationTestingPackage_IsPrivateToTestProjects()
    {
        var violations = ConsumerProjects()
            .SelectMany(ValidateIntegrationTestingReference)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            violations,
            string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void RvtPackageVersions_AreExactAndSynchronized()
    {
        var props = XDocument.Load(Path.Combine(RepositoryRoot(), "Directory.Packages.props"));
        var common = ReadProperty(props, "RvtCommonVersion");
        var infrastructure = ReadProperty(props, "RvtCommonInfrastructureVersion");
        var integrationTesting = ReadProperty(props, "RvtIntegrationTestingVersion");

        Assert.AreEqual(ExpectedRvtVersion, common);
        Assert.AreEqual(common, infrastructure);
        Assert.AreEqual(common, integrationTesting);
    }

    [TestMethod]
    public void CentralRvtPackageBindings_UseExactSingletonRanges()
    {
        var violations = ValidateCentralRvtPackageBindings(
                XDocument.Load(Path.Combine(RepositoryRoot(), "Directory.Packages.props")))
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void ConsumerLocks_RequestExactRvtSingletonRanges()
    {
        var violations = ConsumerProjects()
            .SelectMany(ValidateConsumerLock)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void VersionPolicyValidation_DetectsCentralBindingAndLockRangeDrift()
    {
        var props = XDocument.Parse(
            """
            <Project>
              <PropertyGroup>
                <RvtCommonVersion>0.2.0-rc.1</RvtCommonVersion>
                <RvtCommonInfrastructureVersion>0.2.0-rc.1</RvtCommonInfrastructureVersion>
                <RvtIntegrationTestingVersion>0.2.0-rc.1</RvtIntegrationTestingVersion>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Rvt.Monitor.Common" Version="$(RvtCommonVersion)" />
                <PackageVersion Include="Rvt.Monitor.Common.Infrastructure" Version="[$(RvtCommonInfrastructureVersion)]" />
                <PackageVersion Include="Rvt.Monitor.IntegrationTesting" Version="[$(RvtIntegrationTestingVersion)]" />
              </ItemGroup>
            </Project>
            """);

        var centralViolations = ValidateCentralRvtPackageBindings(props).ToArray();
        var lockViolations = ValidateConsumerLockJson(
                "fixture/packages.lock.json",
                """
                {
                  "version": 1,
                  "dependencies": {
                    "net10.0": {
                      "Rvt.Monitor.Common": {
                        "type": "Direct",
                        "requested": "[0.2.0-rc.1, )",
                        "resolved": "0.2.0-rc.1"
                      }
                    }
                  }
                }
                """,
                ["Rvt.Monitor.Common"])
            .ToArray();

        Assert.IsTrue(centralViolations.Any(violation => violation.Contains("Rvt.Monitor.Common", StringComparison.Ordinal)));
        Assert.IsTrue(lockViolations.Any(violation => violation.Contains("[0.2.0-rc.1, )", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PackageConsumerWorkflow_IsCredentialSafeForUntrustedPullRequests()
    {
        var workflow = File.ReadAllText(
            Path.Combine(RepositoryRoot(), ".github/workflows/package-consumer-ci.yml"));
        var jobs = workflow[(workflow.IndexOf("jobs:", StringComparison.Ordinal) + "jobs:".Length)..];
        var trustedJobStart = jobs.IndexOf("  trusted-package-gate:", StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, trustedJobStart, "The trusted package gate job is missing.");
        var untrustedJob = jobs[..trustedJobStart];
        var trustedJob = jobs[trustedJobStart..];
        Assert.DoesNotContain("packages: read", workflow[..workflow.IndexOf("jobs:", StringComparison.Ordinal)]);
        Assert.DoesNotContain("NuGetPackageSourceCredentials_rvt", untrustedJob);
        Assert.DoesNotContain("github.token", untrustedJob);
        Assert.Contains("persist-credentials: false", untrustedJob);
        Assert.Contains(
            "if: github.event_name == 'push' || github.event.pull_request.head.repo.full_name == github.repository",
            trustedJob);
        Assert.Contains("packages: read", trustedJob);
        Assert.DoesNotContain("github.token", trustedJob);
        Assert.AreEqual(2, CountOccurrences(trustedJob, "secrets.RVT_PACKAGES_READ_USER"));
        Assert.AreEqual(2, CountOccurrences(trustedJob, "secrets.RVT_PACKAGES_READ_TOKEN"));
        Assert.AreEqual(2, CountOccurrences(workflow, "persist-credentials: false"));
        Assert.AreEqual(2, CountOccurrences(trustedJob, "NuGetPackageSourceCredentials_rvt:"));
        Assert.Contains("actions/checkout@df4cb1c069e1874edd31b4311f1884172cec0e10", workflow);
        Assert.Contains("actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1", workflow);
    }

    [TestMethod]
    public void PackageVerificationScript_UsesRunnerPortableSearchTools()
    {
        var script = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "scripts/verify-private-package-builds.sh"));

        Assert.DoesNotContain("if rg ", script);
        Assert.DoesNotContain("| rg ", script);
    }

    [TestMethod]
    public void PackageInventoryScript_UsesPortableTemporaryDirectoryFallback()
    {
        var script = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "scripts/report-rvt-package-inventory.sh"));

        Assert.Contains("${TMPDIR:-/tmp}", script);
        Assert.DoesNotContain("${TMPDIR:-/private/tmp}", script);
    }

    [TestMethod]
    public void MigrationDocumentation_UsesDurableExactSourceCommitRetrieval()
    {
        var readme = File.ReadAllText(Path.Combine(RepositoryRoot(), "myatmmonitor/README.md"));

        Assert.DoesNotContain("gh run download", readme);
        Assert.Contains("f00d5b8a320945ed08e248da8641ca0c3f7e3b82", readme);
        Assert.Contains("archive \"$source_commit\"", readme);
        Assert.Contains("0b9ec190b7a37b06044842d7a582128bc354a83463ddf5c2b027ec4658154170", readme);
        Assert.Contains("2cd2e4e9403b9c69c9aa282107bcf8221bc3749246163a92d7c17e1eac03769e", readme);
    }

    private static IEnumerable<string> ValidateCentralRvtPackageBindings(XDocument props)
    {
        foreach (var (package, property) in ExpectedRvtVersionBindings)
        {
            var versions = props.Descendants()
                .Where(element => element.Name.LocalName == "PackageVersion")
                .Where(element => string.Equals(
                    (string?)element.Attribute("Include"),
                    package,
                    StringComparison.Ordinal))
                .ToArray();
            if (versions.Length != 1)
            {
                yield return $"{package}: expected one central PackageVersion binding, found {versions.Length}.";
                continue;
            }

            var actual = ReadMetadata(versions[0], "Version");
            var expected = $"[$({property})]";
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                yield return $"{package}: expected exact central binding {expected}, actual {actual ?? "<missing>"}.";
            }
        }
    }

    private static IEnumerable<string> ValidateConsumerLock(string projectPath)
    {
        var expectedPackages = ExpectedRvtPackages.TryGetValue(Relative(projectPath), out var packages)
            ? packages
            : [];
        var lockPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "packages.lock.json");
        return ValidateConsumerLockJson(Relative(lockPath), File.ReadAllText(lockPath), expectedPackages);
    }

    private static IEnumerable<string> ValidateConsumerLockJson(
        string lockPath,
        string json,
        IReadOnlyCollection<string> expectedPackages)
    {
        using var document = JsonDocument.Parse(json);
        var expected = expectedPackages.Order(StringComparer.Ordinal).ToArray();
        foreach (var framework in document.RootElement.GetProperty("dependencies").EnumerateObject())
        {
            var direct = framework.Value.EnumerateObject()
                .Where(package => package.Value.TryGetProperty("type", out var type) &&
                    string.Equals(type.GetString(), "Direct", StringComparison.OrdinalIgnoreCase))
                .Where(package => package.Name.StartsWith("Rvt.Monitor.", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var actual = direct.Select(package => package.Name).Order(StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                yield return
                    $"{lockPath} ({framework.Name}): expected direct RVT locks [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}].";
            }

            foreach (var package in direct)
            {
                var requested = package.Value.TryGetProperty("requested", out var requestedElement)
                    ? requestedElement.GetString()
                    : null;
                if (!IsExactSingletonRange(requested))
                {
                    yield return
                        $"{lockPath} ({framework.Name}) {package.Name}: requested {requested ?? "<missing>"}, expected a closed singleton at {ExpectedRvtVersion}.";
                }
            }
        }
    }

    private static bool IsExactSingletonRange(string? requested)
    {
        if (string.Equals(requested, $"[{ExpectedRvtVersion}]", StringComparison.Ordinal))
        {
            return true;
        }

        if (requested is null || requested.Length < 5 || requested[0] != '[' || requested[^1] != ']')
        {
            return false;
        }

        var bounds = requested[1..^1].Split(',', StringSplitOptions.TrimEntries);
        return bounds.Length == 2 &&
            string.Equals(bounds[0], ExpectedRvtVersion, StringComparison.Ordinal) &&
            string.Equals(bounds[1], ExpectedRvtVersion, StringComparison.Ordinal);
    }

    private static IEnumerable<string> FindLocalCommonProjectViolations(string root)
    {
        if (Directory.Exists(Path.Combine(root, "rvt-monitor-common")))
        {
            yield return "rvt-monitor-common: retired local Common source tree is present.";
        }

        foreach (var projectPath in EnumerateProjectFiles(root))
        {
            var project = XDocument.Load(projectPath);
            var relative = Path.GetRelativePath(root, projectPath).Replace('\\', '/');
            var identities = new[]
            {
                (Kind: "project filename", Value: Path.GetFileNameWithoutExtension(projectPath)),
                (Kind: "AssemblyName", Value: ReadOptionalProperty(project, "AssemblyName")),
                (Kind: "PackageId", Value: ReadOptionalProperty(project, "PackageId")),
                (Kind: "RootNamespace", Value: ReadOptionalProperty(project, "RootNamespace"))
            };
            foreach (var identity in identities.Where(identity => IsRetiredCommonIdentity(identity.Value)))
            {
                yield return $"{relative}: {identity.Kind} uses retired local identity {identity.Value}.";
            }

            foreach (var reference in project.Descendants()
                         .Where(element => element.Name.LocalName == "ProjectReference")
                         .Select(element => (string?)element.Attribute("Include"))
                         .Where(include => !string.IsNullOrWhiteSpace(include)))
            {
                var referencedName = Path.GetFileNameWithoutExtension(reference!.Replace('\\', '/'));
                if (IsRetiredCommonIdentity(referencedName))
                {
                    yield return $"{relative}: ProjectReference resolves to retired local identity {referencedName}.";
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateProjectFiles(string root) =>
        Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !HasExcludedDirectory(root, path));

    private static string? ReadOptionalProperty(XDocument project, string name) =>
        project.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == name)
            ?.Value
            .Trim();

    private static bool IsRetiredCommonIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.StartsWith("Rvt.Monitor.Common", StringComparison.OrdinalIgnoreCase) ||
         value.StartsWith("Rvt.Monitor.IntegrationTesting", StringComparison.OrdinalIgnoreCase));

    private static int CountOccurrences(string value, string text)
    {
        var count = 0;
        var start = 0;
        while ((start = value.IndexOf(text, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += text.Length;
        }

        return count;
    }

    private static IEnumerable<string> ValidateRvtPackageMatrix(string projectPath)
    {
        var relative = Relative(projectPath);
        var references = XDocument.Load(projectPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Where(element => ((string?)element.Attribute("Include") ?? string.Empty)
                .StartsWith("Rvt.Monitor.", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var actual = references
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expected = ExpectedRvtPackages.TryGetValue(relative, out var packages)
            ? packages.Order(StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            yield return $"{relative}: expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}].";
        }

        foreach (var reference in references.Where(reference =>
                     ReadMetadata(reference, "Version") is not null ||
                     ReadMetadata(reference, "VersionOverride") is not null))
        {
            yield return $"{relative}: {(string?)reference.Attribute("Include")} must use the central exact version.";
        }
    }

    private static IEnumerable<string> ValidateIntegrationTestingReference(string projectPath)
    {
        var project = XDocument.Load(projectPath);
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var relativePath = Relative(projectPath);

        foreach (var reference in project.Descendants()
                     .Where(element => element.Name.LocalName is "PackageReference" or "ProjectReference")
                     .Where(element => ((string?)element.Attribute("Include") ?? string.Empty)
                         .Contains("Rvt.Monitor.IntegrationTesting", StringComparison.OrdinalIgnoreCase)))
        {
            if (reference.Name.LocalName == "ProjectReference")
            {
                yield return $"{relativePath}: Rvt.Monitor.IntegrationTesting must not use ProjectReference.";
                continue;
            }

            if (!projectName.EndsWith("Tests", StringComparison.Ordinal))
            {
                yield return $"{relativePath}: Rvt.Monitor.IntegrationTesting is restricted to test projects.";
            }

            if (!string.Equals(ReadMetadata(reference, "PrivateAssets"), "all", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{relativePath}: Rvt.Monitor.IntegrationTesting must set PrivateAssets=all.";
            }
        }
    }

    private static string? ReadMetadata(XElement reference, string name) =>
        reference.Attribute(name)?.Value ??
        reference.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value;

    private static string ReadProperty(XDocument props, string name) =>
        props.Descendants()
            .Single(element => element.Name.LocalName == name)
            .Value;

    private static IEnumerable<string> ConsumerProjects()
    {
        var root = RepositoryRoot();
        return Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !HasExcludedDirectory(root, path));
    }

    private static bool HasExcludedDirectory(string root, string path)
    {
        var segments = Path.GetRelativePath(root, path)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => segment.Equals(".worktrees", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("rvt-monitor-common", StringComparison.OrdinalIgnoreCase));
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(RepositoryRoot(), path).Replace('\\', '/');

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
}
