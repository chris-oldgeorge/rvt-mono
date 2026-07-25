using System.Xml.Linq;

namespace Rvt.Storage.Tests.Architecture;

[TestClass]
public sealed class StorageDependencyBoundaryTests
{
    [TestMethod]
    public void Abstractions_RemainsProviderFrameworkAndFilesystemIndependent()
    {
        var project = StorageProjectSnapshot.Load("Rvt.Storage.Abstractions");

        Assert.IsEmpty(project.PackageReferences);
        Assert.IsEmpty(project.ProjectReferences);
        project.AssertSourceExcludes(
            "Azure.",
            "Amazon.",
            "Microsoft.Extensions.",
            "System.IO.File",
            "System.IO.Directory",
            "System.IO.FileStream");
    }

    [TestMethod]
    public void Local_ReferencesNoCloudProviderSdk()
    {
        var project = StorageProjectSnapshot.Load("Rvt.Storage.Local");

        CollectionAssert.AreEquivalent(
            new[] { "Rvt.Storage.Abstractions" },
            project.ProjectReferences.ToArray());
        project.AssertPackagesExclude("Azure.", "AWSSDK.S3");
        project.AssertSourceExcludes("Azure.", "Amazon.");
    }

    [TestMethod]
    public void AzureBlob_ReferencesAzureSdkAndNoAmazonSdk()
    {
        var project = StorageProjectSnapshot.Load("Rvt.Storage.AzureBlob");

        CollectionAssert.AreEquivalent(
            new[] { "Rvt.Storage.Abstractions" },
            project.ProjectReferences.ToArray());
        CollectionAssert.IsSubsetOf(
            new[] { "Azure.Identity", "Azure.Storage.Blobs" },
            project.PackageReferences.ToArray());
        project.AssertPackagesExclude("AWSSDK.S3");
        project.AssertSourceIncludes("Azure.Storage.Blobs", "Azure.Identity");
        project.AssertSourceExcludes("Amazon.");
    }

    [TestMethod]
    public void S3_ReferencesAmazonSdkAndNoAzureSdk()
    {
        var project = StorageProjectSnapshot.Load("Rvt.Storage.S3");

        CollectionAssert.AreEquivalent(
            new[] { "Rvt.Storage.Abstractions" },
            project.ProjectReferences.ToArray());
        CollectionAssert.Contains(project.PackageReferences.ToArray(), "AWSSDK.S3");
        project.AssertPackagesExclude("Azure.");
        project.AssertSourceIncludes("Amazon.");
        project.AssertSourceExcludes("Azure.");
    }

    private sealed class StorageProjectSnapshot
    {
        private readonly CSharpDependencyAnalysis sourceAnalysis;

        private StorageProjectSnapshot(
            IReadOnlyCollection<string> packageReferences,
            IReadOnlyCollection<string> projectReferences,
            CSharpDependencyAnalysis sourceAnalysis)
        {
            PackageReferences = packageReferences;
            ProjectReferences = projectReferences;
            this.sourceAnalysis = sourceAnalysis;
        }

        public IReadOnlyCollection<string> PackageReferences { get; }

        public IReadOnlyCollection<string> ProjectReferences { get; }

        public static StorageProjectSnapshot Load(string projectName)
        {
            var repositoryRoot = FindRepositoryRoot();
            var projectDirectory = Path.Combine(
                repositoryRoot,
                "libs",
                "rvt-monitor-common",
                "src",
                projectName);
            var projectPath = Path.Combine(projectDirectory, $"{projectName}.csproj");
            Assert.IsTrue(
                File.Exists(projectPath),
                $"Expected storage project '{projectPath}' to exist.");

            var project = XDocument.Load(projectPath);
            var packageReferences = ProjectDependencyReader.ReadActiveIdentities(
                project,
                "PackageReference");
            var projectReferences = ProjectDependencyReader
                .ReadActiveIdentities(project, "ProjectReference")
                .Select(value => Path.GetFileNameWithoutExtension(
                    value.Replace('\\', Path.DirectorySeparatorChar)))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var sourceFiles = Directory
                .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsGeneratedPath(projectDirectory, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(
                    path => Path.GetRelativePath(repositoryRoot, path),
                    File.ReadAllText,
                    StringComparer.Ordinal);

            Assert.IsNotEmpty(
                sourceFiles,
                $"Expected storage project '{projectName}' to contain production source.");
            Assert.IsFalse(
                sourceFiles.Keys.Any(path => HasPathSegment(path, "obj")
                    || HasPathSegment(path, "bin")),
                "Generated obj/bin source must not participate in dependency checks.");

            return new StorageProjectSnapshot(
                packageReferences,
                projectReferences,
                CSharpDependencyAnalyzer.AnalyzeProject(sourceFiles));
        }

        public void AssertPackagesExclude(params string[] forbiddenPrefixes)
        {
            foreach (var forbiddenPrefix in forbiddenPrefixes)
            {
                var matches = PackageReferences
                    .Where(package => package.StartsWith(
                        forbiddenPrefix,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                Assert.IsEmpty(
                    matches,
                    $"Forbidden package prefix '{forbiddenPrefix}' matched: "
                    + string.Join(", ", matches));
            }
        }

        public void AssertSourceIncludes(params string[] requiredMarkers)
        {
            foreach (var requiredMarker in requiredMarkers)
            {
                Assert.IsTrue(
                    sourceAnalysis.UsesDependency(requiredMarker),
                    $"Expected production source to use '{requiredMarker}'.");
            }
        }

        public void AssertSourceExcludes(params string[] forbiddenMarkers)
        {
            foreach (var forbiddenMarker in forbiddenMarkers)
            {
                var matches = sourceAnalysis.GetSourceFilesUsing(forbiddenMarker);
                Assert.IsEmpty(
                    matches,
                    $"Forbidden source dependency '{forbiddenMarker}' was found in: "
                    + string.Join(", ", matches));
            }
        }

        private static string FindRepositoryRoot()
        {
            foreach (var startingPath in new[]
                     {
                         Directory.GetCurrentDirectory(),
                         AppContext.BaseDirectory,
                     })
            {
                for (var directory = new DirectoryInfo(startingPath);
                     directory is not null;
                     directory = directory.Parent)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "project_state.md"))
                        && Directory.Exists(Path.Combine(
                            directory.FullName,
                            "libs",
                            "rvt-monitor-common",
                            "src")))
                    {
                        return directory.FullName;
                    }
                }
            }

            Assert.Fail(
                "Could not locate the repository root from the test working directory "
                + "or test assembly base directory.");
            return null!;
        }

        private static bool IsGeneratedPath(
            string projectDirectory,
            string sourcePath)
        {
            var relativePath = Path.GetRelativePath(projectDirectory, sourcePath);
            return HasPathSegment(relativePath, "obj")
                || HasPathSegment(relativePath, "bin");
        }

        private static bool HasPathSegment(string path, string segment) =>
            path.Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries)
                .Contains(segment, StringComparer.OrdinalIgnoreCase);
    }
}
