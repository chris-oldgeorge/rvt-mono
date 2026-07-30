using System.Xml.Linq;

namespace Rvt.Storage.Tests.Architecture;

[TestClass]
public sealed class StorageDependencyBoundaryTests
{
    private static readonly string[] _azureStoragePackages = ["Azure.Identity", "Azure.Storage.Blobs"];

    [TestMethod]
    public void Common_ReferencesNoCloudProviderSdkPackages()
    {
        StorageProjectSnapshot project = StorageProjectSnapshot.Load("Rvt.Monitor.Common");

        project.AssertPackagesExclude(
            "AWSSDK.S3",
            "Azure.Identity",
            "Azure.Storage.Blobs");
    }

    [TestMethod]
    public void Common_ProductionSourceUsesNoCloudProviderNamespaces()
    {
        StorageProjectSnapshot project = StorageProjectSnapshot.Load("Rvt.Monitor.Common");

        project.AssertSourceExcludes("Amazon.", "Azure.Storage");
    }

    [TestMethod]
    public void Abstractions_RemainsProviderFrameworkAndFilesystemIndependent()
    {
        StorageProjectSnapshot project = StorageProjectSnapshot.Load("Rvt.Storage.Abstractions");

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

    private static readonly string[] _expectedAbstractionReference = ["Rvt.Storage.Abstractions"];

    [TestMethod]
    public void Local_ReferencesNoCloudProviderSdk()
    {
        StorageProjectSnapshot project = StorageProjectSnapshot.Load("Rvt.Storage.Local");

        CollectionAssert.AreEquivalent(
            _expectedAbstractionReference,
            project.ProjectReferences.ToArray());
        project.AssertPackagesExclude("Azure.", "AWSSDK.S3");
        project.AssertSourceExcludes("Azure.", "Amazon.");
    }

    [TestMethod]
    public void AzureBlob_ReferencesAzureSdkAndNoAmazonSdk()
    {
        StorageProjectSnapshot project = StorageProjectSnapshot.Load("Rvt.Storage.AzureBlob");

        CollectionAssert.AreEquivalent(
            _expectedAbstractionReference,
            project.ProjectReferences.ToArray());
        CollectionAssert.IsSubsetOf(
            _azureStoragePackages,
            project.PackageReferences.ToArray());
        project.AssertPackagesExclude("AWSSDK.S3");
        project.AssertSourceIncludes("Azure.Storage.Blobs", "Azure.Identity");
        project.AssertSourceExcludes("Amazon.");
    }

    [TestMethod]
    public void S3_ReferencesAmazonSdkAndNoAzureSdk()
    {
        StorageProjectSnapshot project = StorageProjectSnapshot.Load("Rvt.Storage.S3");

        CollectionAssert.AreEquivalent(
            _expectedAbstractionReference,
            project.ProjectReferences.ToArray());
        CollectionAssert.Contains(project.PackageReferences.ToArray(), "AWSSDK.S3");
        project.AssertPackagesExclude("Azure.");
        project.AssertSourceIncludes("Amazon.");
        project.AssertSourceExcludes("Azure.");
    }

    private sealed class StorageProjectSnapshot
    {
        private readonly CSharpDependencyAnalysis _sourceAnalysis;

        private StorageProjectSnapshot(
            IReadOnlyCollection<string> packageReferences,
            IReadOnlyCollection<string> projectReferences,
            CSharpDependencyAnalysis sourceAnalysis)
        {
            PackageReferences = packageReferences;
            ProjectReferences = projectReferences;
            this._sourceAnalysis = sourceAnalysis;
        }

        public IReadOnlyCollection<string> PackageReferences { get; }

        public IReadOnlyCollection<string> ProjectReferences { get; }

        public static StorageProjectSnapshot Load(string projectName)
        {
            string repositoryRoot = FindRepositoryRoot();
            string projectDirectory = Path.Combine(
                repositoryRoot,
                "libs",
                "rvt-monitor-common",
                "src",
                projectName);
            string projectPath = Path.Combine(projectDirectory, $"{projectName}.csproj");
            Assert.IsTrue(
                File.Exists(projectPath),
                $"Expected storage project '{projectPath}' to exist.");

            XDocument project = XDocument.Load(projectPath);
            IReadOnlyCollection<string> packageReferences = ProjectDependencyReader.ReadActiveIdentities(
                project,
                "PackageReference");
            string[] projectReferences = [.. ProjectDependencyReader
                .ReadActiveIdentities(project, "ProjectReference")
                .Select(value => Path.GetFileNameWithoutExtension(
                    value.Replace('\\', Path.DirectorySeparatorChar)))
                .OrderBy(value => value, StringComparer.Ordinal)];
            Dictionary<string, string> sourceFiles = Directory
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
            foreach (string forbiddenPrefix in forbiddenPrefixes)
            {
                string[] matches = [.. PackageReferences
                    .Where(package => package.StartsWith(
                        forbiddenPrefix,
                        StringComparison.OrdinalIgnoreCase))];
                Assert.IsEmpty(
                    matches,
                    $"Forbidden package prefix '{forbiddenPrefix}' matched: "
                    + string.Join(", ", matches));
            }
        }

        public void AssertSourceIncludes(params string[] requiredMarkers)
        {
            foreach (string requiredMarker in requiredMarkers)
            {
                Assert.IsTrue(
                    _sourceAnalysis.UsesDependency(requiredMarker),
                    $"Expected production source to use '{requiredMarker}'.");
            }
        }

        public void AssertSourceExcludes(params string[] forbiddenMarkers)
        {
            foreach (string forbiddenMarker in forbiddenMarkers)
            {
                IReadOnlyCollection<string> matches = _sourceAnalysis.GetSourceFilesUsing(forbiddenMarker);
                Assert.IsEmpty(
                    matches,
                    $"Forbidden source dependency '{forbiddenMarker}' was found in: "
                    + string.Join(", ", matches));
            }
        }

        private static string FindRepositoryRoot()
        {
            foreach (string? startingPath in new[]
                     {
                         Directory.GetCurrentDirectory(),
                         AppContext.BaseDirectory,
                     })
            {
                for (DirectoryInfo? directory = new(startingPath);
                     directory is not null;
                     directory = directory.Parent)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "Rvt.Mono.slnx"))
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
            string relativePath = Path.GetRelativePath(projectDirectory, sourcePath);
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
