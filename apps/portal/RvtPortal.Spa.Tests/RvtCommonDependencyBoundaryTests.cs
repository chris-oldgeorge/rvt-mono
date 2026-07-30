// File summary: Confines the RVT common source reference to the host adapter layer and keeps private feeds out of the repo.
// Major updates:
// - 2026-07-24 confined the host to communication abstractions and the SendGrid adapter.
// - 2026-07-17 pending Added the zero-package boundary scanner and regression fixtures.

using System.Text;
using System.Xml.Linq;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public sealed class RvtCommonDependencyBoundaryTests
{
    [Fact]
    public void Scanner_FindsPackageAndNamespaceReferences()
    {
        using TemporaryDirectory fixture = TemporaryDirectory.Create();
        File.WriteAllText(
            Path.Combine(fixture.Path, "Consumer.csproj"),
            "<Project><ItemGroup><PackageReference Include=\"Rvt.Monitor.Common\" Version=\"0.2.0\" /></ItemGroup></Project>");
        File.WriteAllText(Path.Combine(fixture.Path, "Consumer.cs"), "using Rvt.Monitor.Common;");
        File.WriteAllText(Path.Combine(fixture.Path, "CommentOnly.cs"), "// Shared adapter seam for Rvt.Monitor.Common.");

        IReadOnlyList<string> findings = RepositoryDependencyScanner.FindCommonReferences(fixture.Path);

        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void Scanner_IgnoresMarkersInsideMultilineCSharpBlockComments()
    {
        using TemporaryDirectory fixture = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(fixture.Path, "CommentOnly.cs"), "/*\nRvt.Monitor.Common\n*/");

        Assert.Empty(RepositoryDependencyScanner.FindCommonReferences(fixture.Path));
    }

    [Fact]
    public void Scanner_FindsCodeAfterCSharpBlockCommentEndsOnSameLine()
    {
        using TemporaryDirectory fixture = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(fixture.Path, "Consumer.cs"), "/* comment\n*/ using Rvt.Monitor.Common;");

        Assert.Single(RepositoryDependencyScanner.FindCommonReferences(fixture.Path));
    }

    [Fact]
    public void Scanner_IgnoresSolutionProjectMembershipBecauseItIsNotADependency()
    {
        using TemporaryDirectory fixture = TemporaryDirectory.Create();
        File.WriteAllText(
            Path.Combine(fixture.Path, "Portal.sln"),
            "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Rvt.Monitor.Common\", \"libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj\", \"{65FAF867-E680-46FC-A921-8F5D9DE0180E}\"");

        Assert.Empty(RepositoryDependencyScanner.FindCommonReferences(fixture.Path));
    }

    [Fact]
    // RVT common is an adapter-side dependency: only the host (RvtPortal.Spa) may reference its source project.
    // The business core reaches email through its own IEmailDelivery port, so the hexagonal boundary still holds.
    public void RvtCommon_IsConfinedToTheHostAdapterProject()
    {
        string[] offenders = [.. RepositoryDependencyScanner.FindCommonReferences(RepositoryLayout.Root)
            .Select(finding => finding.Replace('\\', '/'))
            .Where(finding => !finding.StartsWith("RvtPortal.Spa/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

        Assert.Empty(offenders);
    }

    [Fact]
    // Strongest form of the same boundary: the compiled application core must not reference RVT common at all.
    public void ApplicationCore_DoesNotReferenceRvtCommon()
    {
        string?[] referenced = [.. typeof(RvtPortal.Application.Time.IRvtDateTimeProvider).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)];

        Assert.DoesNotContain(referenced, name => name?.StartsWith("Rvt.Monitor.", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void HostAdapter_UsesOnlyApprovedCommunicationAdapterProjects()
    {
        string projectPath = Path.Combine(RepositoryLayout.Root, "RvtPortal.Spa", "RvtPortal.Spa.csproj");
        XDocument project = XDocument.Load(projectPath);
        string?[] packageReferences = [.. project.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(package => package?.StartsWith("Rvt.", StringComparison.OrdinalIgnoreCase) == true)];
        string[] sourceReferences = [.. project.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(reference => reference?.Contains(
                "libs/rvt-monitor-common/src/",
                StringComparison.OrdinalIgnoreCase) == true)
            .Select(reference => Path.GetFileNameWithoutExtension(reference)
                ?? throw new InvalidOperationException("Project reference did not have a file name."))
            .Order(StringComparer.Ordinal)];

        Assert.Empty(packageReferences);
        Assert.Equal(
            ["Rvt.Communication.Abstractions", "Rvt.Communication.SendGridMail"],
            sourceReferences);
    }

    [Fact]
    public void HostAdapter_DoesNotUseUnapprovedProviderNamespaces()
    {
        string hostRoot = Path.Combine(RepositoryLayout.Root, "RvtPortal.Spa");
        string[] forbiddenMarkers =
        [
            "using Rvt.Communication;",
            "Rvt.Communication.MicrosoftGraphMail",
            "Rvt.Communication.TransmitSms",
            string.Concat("Rvt.Monitor.Common.", "Infrastructure"),
            "Amazon.S3",
            "Rvt.Storage.S3"
        ];

        string[] offenders = [.. Directory.EnumerateFiles(hostRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !Path.GetFileName(path).Contains(" 2.", StringComparison.Ordinal))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => (line, index))
                .Where(candidate => forbiddenMarkers.Any(marker =>
                    candidate.line.Contains(marker, StringComparison.Ordinal)))
                .Select(candidate =>
                    $"{Path.GetRelativePath(hostRoot, path)}:{candidate.index + 1}:{candidate.line.Trim()}"))
            .Order(StringComparer.Ordinal)];

        Assert.Empty(offenders);
    }

    [Fact]
    public void NuGetConfig_UsesNuGetOrgWithoutPrivateFeedOrCredentials()
    {
        string nugetConfig = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "NuGet.config"));

        Assert.Contains("nuget.org", nugetConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("github.com", nugetConfig, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("packageSourceCredentials", nugetConfig, StringComparison.OrdinalIgnoreCase);
        foreach (string? literalTokenMarker in new[] { "ghp_", "github_pat_", "ghs_" })
        {
            Assert.DoesNotContain(literalTokenMarker, nugetConfig, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static class RepositoryDependencyScanner
    {
        private static readonly HashSet<string> _scannedExtensions =
        [
            ".cs",
            ".csproj",
            ".props",
            ".targets",
            ".config"
        ];

        private static readonly HashSet<string> _excludedDirectories =
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

            return [.. EnumerateSourceFiles(root)
                .Where(path => !Path.GetFileName(path).Equals(
                    "RvtCommonDependencyBoundaryTests.cs",
                    StringComparison.Ordinal))
                .SelectMany(path => FindMatches(root, path, markers))
                .Order(StringComparer.Ordinal)];
        }

        private static IEnumerable<string> EnumerateSourceFiles(string root)
        {
            Stack<string> pending = new();
            pending.Push(root);

            while (pending.TryPop(out string? current))
            {
                foreach (string directory in Directory.EnumerateDirectories(current))
                {
                    if (!_excludedDirectories.Contains(Path.GetFileName(directory)))
                    {
                        pending.Push(directory);
                    }
                }

                foreach (string file in Directory.EnumerateFiles(current))
                {
                    if (_scannedExtensions.Contains(Path.GetExtension(file)))
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
            int lineNumber = 0;
            bool isCSharp = Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase);
            bool isInBlockComment = false;
            foreach (string line in File.ReadLines(path))
            {
                lineNumber++;
                string trimmedLine = line.Trim();
                string searchableLine = isCSharp
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
            StringBuilder code = new(line.Length);
            int position = 0;

            while (position < line.Length)
            {
                if (isInBlockComment)
                {
                    int blockEnd = line.IndexOf("*/", position, StringComparison.Ordinal);
                    if (blockEnd < 0)
                    {
                        break;
                    }

                    isInBlockComment = false;
                    position = blockEnd + 2;
                    continue;
                }

                int lineComment = line.IndexOf("//", position, StringComparison.Ordinal);
                int blockStart = line.IndexOf("/*", position, StringComparison.Ordinal);
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
            string path = System.IO.Path.Combine(
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
