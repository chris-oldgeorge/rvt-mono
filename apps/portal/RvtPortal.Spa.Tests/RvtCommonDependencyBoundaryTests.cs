// File summary: Confines the RVT common source reference to the host adapter layer and keeps private feeds out of the repo.
// Major updates:
// - 2026-07-22 pending Migrated the host boundary from a private package to the mono-repository Infrastructure source project.
// - 2026-07-17 pending Adopted Rvt.Monitor.Common.Infrastructure for email: replaced the zero-package rule with a
//   host-only boundary (the business core still must not reference it) plus a credential-hygiene check on NuGet.config.
// - 2026-07-17 pending Added the zero-package boundary scanner and regression fixtures.

using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public sealed class RvtCommonDependencyBoundaryTests
{
    [Fact]
    public void Scanner_FindsPackageAndNamespaceReferences()
    {
        using var fixture = TemporaryDirectory.Create();
        File.WriteAllText(
            Path.Combine(fixture.Path, "Consumer.csproj"),
            "<Project><ItemGroup><PackageReference Include=\"Rvt.Monitor.Common\" Version=\"0.2.0\" /></ItemGroup></Project>");
        File.WriteAllText(Path.Combine(fixture.Path, "Consumer.cs"), "using Rvt.Monitor.Common;");
        File.WriteAllText(Path.Combine(fixture.Path, "CommentOnly.cs"), "// Shared adapter seam for Rvt.Monitor.Common.");

        var findings = RepositoryDependencyScanner.FindCommonReferences(fixture.Path);

        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void Scanner_IgnoresMarkersInsideMultilineCSharpBlockComments()
    {
        using var fixture = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(fixture.Path, "CommentOnly.cs"), "/*\nRvt.Monitor.Common\n*/");

        Assert.Empty(RepositoryDependencyScanner.FindCommonReferences(fixture.Path));
    }

    [Fact]
    public void Scanner_FindsCodeAfterCSharpBlockCommentEndsOnSameLine()
    {
        using var fixture = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(fixture.Path, "Consumer.cs"), "/* comment\n*/ using Rvt.Monitor.Common;");

        Assert.Single(RepositoryDependencyScanner.FindCommonReferences(fixture.Path));
    }

    [Fact]
    // RVT common is an adapter-side dependency: only the host (RvtPortal.Spa) may reference its source project.
    // The business core reaches email through its own IEmailDelivery port, so the hexagonal boundary still holds.
    public void RvtCommon_IsConfinedToTheHostAdapterProject()
    {
        var offenders = RepositoryDependencyScanner.FindCommonReferences(RepositoryLayout.Root)
            .Select(finding => finding.Replace('\\', '/'))
            .Where(finding => !finding.StartsWith("RvtPortal.Spa/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    // Strongest form of the same boundary: the compiled business core must not reference RVT common at all.
    public void BusinessLogicCore_DoesNotReferenceRvtCommon()
    {
        var referenced = typeof(RVT.BusinessLogic.IRvtDateTimeProvider).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain(referenced, name => name?.StartsWith("Rvt.Monitor.", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void HostAdapter_UsesInfrastructureSourceWithoutRvtPackageReferences()
    {
        var projectPath = Path.Combine(RepositoryLayout.Root, "RvtPortal.Spa", "RvtPortal.Spa.csproj");
        var project = System.Xml.Linq.XDocument.Load(projectPath);
        var packageReferences = project.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(package => package?.StartsWith("Rvt.Monitor.", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();
        var sourceReferences = project.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(reference => reference?.Replace('\\', '/').EndsWith(
                "libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj",
                StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Empty(packageReferences);
        Assert.Single(sourceReferences);
    }

    [Fact]
    public void NuGetConfig_UsesNuGetOrgWithoutPrivateFeedOrCredentials()
    {
        var nugetConfig = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "NuGet.config"));

        Assert.Contains("nuget.org", nugetConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("github.com", nugetConfig, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("packageSourceCredentials", nugetConfig, StringComparison.OrdinalIgnoreCase);
        foreach (var literalTokenMarker in new[] { "ghp_", "github_pat_", "ghs_" })
        {
            Assert.DoesNotContain(literalTokenMarker, nugetConfig, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static class RepositoryDependencyScanner
    {
        private static readonly HashSet<string> ScannedExtensions =
        [
            ".cs",
            ".csproj",
            ".props",
            ".targets",
            ".sln",
            ".config"
        ];

        private static readonly HashSet<string> ExcludedDirectories =
        [
            ".git",
            ".worktrees",
            "bin",
            "obj",
            "node_modules",
            "dist",
            "TestResults"
        ];

        public static IReadOnlyList<string> FindCommonReferences(string root)
        {
            string[] markers =
            [
                string.Concat("Rvt", ".Monitor", "."),
                string.Concat("rvt", "-monitor-common"),
                string.Concat("rvt", "-reporting")
            ];

            return EnumerateSourceFiles(root)
                .Where(path => !Path.GetFileName(path).Equals(
                    "RvtCommonDependencyBoundaryTests.cs",
                    StringComparison.Ordinal))
                .SelectMany(path => FindMatches(root, path, markers))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        private static IEnumerable<string> EnumerateSourceFiles(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.TryPop(out var current))
            {
                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    if (!ExcludedDirectories.Contains(Path.GetFileName(directory)))
                    {
                        pending.Push(directory);
                    }
                }

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    if (ScannedExtensions.Contains(Path.GetExtension(file)))
                    {
                        yield return file;
                    }
                }
            }
        }

        private static IEnumerable<string> FindMatches(
            string root,
            string path,
            IReadOnlyCollection<string> markers)
        {
            var lineNumber = 0;
            var isCSharp = Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase);
            var isInBlockComment = false;
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                var trimmedLine = line.Trim();
                var searchableLine = isCSharp
                    ? StripCSharpComments(line, ref isInBlockComment)
                    : line;

                if (markers.Any(marker => searchableLine.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return $"{Path.GetRelativePath(root, path)}:{lineNumber}:{trimmedLine}";
                }
            }
        }

        private static string StripCSharpComments(string line, ref bool isInBlockComment)
        {
            var code = new System.Text.StringBuilder(line.Length);
            var position = 0;

            while (position < line.Length)
            {
                if (isInBlockComment)
                {
                    var blockEnd = line.IndexOf("*/", position, StringComparison.Ordinal);
                    if (blockEnd < 0)
                    {
                        break;
                    }

                    isInBlockComment = false;
                    position = blockEnd + 2;
                    continue;
                }

                var lineComment = line.IndexOf("//", position, StringComparison.Ordinal);
                var blockStart = line.IndexOf("/*", position, StringComparison.Ordinal);
                if (lineComment >= 0 && (blockStart < 0 || lineComment < blockStart))
                {
                    code.Append(line, position, lineComment - position);
                    break;
                }

                if (blockStart < 0)
                {
                    code.Append(line, position, line.Length - position);
                    break;
                }

                code.Append(line, position, blockStart - position);
                isInBlockComment = true;
                position = blockStart + 2;
            }

            return code.ToString();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"rvt-cloud-boundary-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
