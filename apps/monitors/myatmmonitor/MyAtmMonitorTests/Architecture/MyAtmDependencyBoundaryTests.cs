using System.Xml.Linq;

namespace MyAtmMonitorTests.Architecture;

[TestClass]
public sealed class MyAtmDependencyBoundaryTests
{
    [TestMethod]
    public void MapperlyPackageReferences_FollowMonitorAppAnalyzerPolicy()
    {
        var repositoryRoot = FindRepositoryRoot();
        var violations = EnumeratePrimaryProjectFiles(repositoryRoot)
            .SelectMany(projectPath => ReadMapperlyReferences(projectPath)
                .SelectMany(reference => ValidateMapperlyReference(repositoryRoot, projectPath, reference)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    private static IEnumerable<string> ValidateMapperlyReference(
        string repositoryRoot,
        string projectPath,
        XElement packageReference)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot, projectPath).Replace('\\', '/');
        var segments = relativePath.Split('/');
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var projectDirectory = Path.GetFileName(Path.GetDirectoryName(projectPath));
        var project = packageReference.Document?.Root;
        var isTestProject = project?
            .Descendants()
            .Where(element => element.Name.LocalName == "IsTestProject")
            .Any(element => bool.TryParse(element.Value, out var value) && value) == true;

        if (segments.Length != 3 ||
            !segments[0].EndsWith("monitor", StringComparison.OrdinalIgnoreCase) ||
            segments[0].Equals("rvt-monitor-common", StringComparison.OrdinalIgnoreCase) ||
            !projectName.Equals(projectDirectory, StringComparison.Ordinal) ||
            projectName.EndsWith("Test", StringComparison.OrdinalIgnoreCase) ||
            projectName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
            isTestProject)
        {
            yield return $"{relativePath}: Mapperly is restricted to direct, non-test monitor application projects.";
        }

        if (!string.Equals(ReadMetadata(packageReference, "PrivateAssets"), "all", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{relativePath}: Riok.Mapperly must set PrivateAssets=all.";
        }

        if (!string.Equals(ReadMetadata(packageReference, "OutputItemType"), "Analyzer", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{relativePath}: Riok.Mapperly must set OutputItemType=Analyzer.";
        }
    }

    private static string? ReadMetadata(XElement packageReference, string metadataName) =>
        packageReference.Attribute(metadataName)?.Value ??
        packageReference.Elements().FirstOrDefault(element => element.Name.LocalName == metadataName)?.Value;

    private static IEnumerable<XElement> ReadMapperlyReferences(string projectPath)
    {
        var project = XDocument.Load(projectPath, LoadOptions.SetLineInfo);
        return project
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Where(element => string.Equals(
                element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value,
                "Riok.Mapperly",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static IEnumerable<string> EnumeratePrimaryProjectFiles(string repositoryRoot) =>
        Directory
            .EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => IsPrimaryRepositoryProject(path, repositoryRoot));

    [TestMethod]
    public void ProductionCSharp_DoesNotReferenceTheLegacyMyAtmOutbox()
    {
        var repositoryRoot = FindRepositoryRoot();
        var productionRoot = Path.Combine(repositoryRoot, "myatmmonitor", "MyAtmMonitor");
        var forbiddenReferences = new[]
        {
            "MyAtmOutboxMessageEntity",
            "IMyAtmOutbox",
            "MyAtmOutboxDispatcher",
            "context.OutboxMessages"
        };

        var violations = Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOutput(path, productionRoot))
            .SelectMany(path => forbiddenReferences
                .Where(reference => File.ReadAllText(path).Contains(reference, StringComparison.Ordinal))
                .Select(reference => $"{Path.GetRelativePath(repositoryRoot, path)}: {reference}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    private static bool IsGeneratedOutput(string path, string productionRoot)
    {
        var relativePath = Path.GetRelativePath(productionRoot, path).Replace('\\', '/');
        return relativePath.StartsWith("bin/", StringComparison.Ordinal) ||
            relativePath.StartsWith("obj/", StringComparison.Ordinal);
    }

    private static bool IsPrimaryRepositoryProject(string path, string repositoryRoot)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
        return !relativePath.StartsWith(".worktrees/", StringComparison.Ordinal);
    }

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

        Assert.Fail("Could not find repository root from test output directory.");
        return string.Empty;
    }
}
