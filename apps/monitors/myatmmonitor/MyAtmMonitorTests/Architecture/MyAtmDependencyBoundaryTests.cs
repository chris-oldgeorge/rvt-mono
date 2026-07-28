using System.Xml.Linq;
using Rvt.Monitor.IntegrationTesting;

namespace MyAtmMonitorTests.Architecture;

[TestClass]
public sealed class MyAtmDependencyBoundaryTests
{
    [TestMethod]
    public void MapperlyPackageReferences_FollowMonitorAppAnalyzerPolicy()
    {
        string repositoryRoot = RepositoryLayout.Root;
        string[] violations = [.. EnumeratePrimaryProjectFiles(repositoryRoot)
            .SelectMany(projectPath => ReadMapperlyReferences(projectPath)
                .SelectMany(reference => ValidateMapperlyReference(repositoryRoot, projectPath, reference)))
            .Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            violations,
            string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> ValidateMapperlyReference(
        string repositoryRoot,
        string projectPath,
        XElement packageReference)
    {
        string relativePath = Path.GetRelativePath(repositoryRoot, projectPath).Replace('\\', '/');
        string[] segments = relativePath.Split('/');
        string projectName = Path.GetFileNameWithoutExtension(projectPath);
        string? projectDirectory = Path.GetFileName(Path.GetDirectoryName(projectPath));
        XElement? project = packageReference.Document?.Root;
        bool isTestProject = project?
            .Descendants()
            .Where(element => element.Name.LocalName == "IsTestProject")
            .Any(element => bool.TryParse(element.Value, out bool value) && value) == true;

        if (segments.Length != 5 ||
            !segments[0].Equals("apps", StringComparison.OrdinalIgnoreCase) ||
            !segments[1].Equals("monitors", StringComparison.OrdinalIgnoreCase) ||
            !segments[2].EndsWith("monitor", StringComparison.OrdinalIgnoreCase) ||
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
        XDocument project = XDocument.Load(projectPath, LoadOptions.SetLineInfo);
        return [.. project
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Where(element => string.Equals(
                element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value,
                "Riok.Mapperly",
                StringComparison.OrdinalIgnoreCase))];
    }

    private static IEnumerable<string> EnumeratePrimaryProjectFiles(string repositoryRoot) =>
        Directory
            .EnumerateFiles(
                Path.Combine(repositoryRoot, "apps", "monitors"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Where(path => IsPrimaryRepositoryProject(path, repositoryRoot));

    [TestMethod]
    public void ProductionCSharp_DoesNotReferenceTheLegacyMyAtmOutbox()
    {
        string repositoryRoot = RepositoryLayout.Root;
        string productionRoot = Path.Combine(
            repositoryRoot,
            "apps",
            "monitors",
            "myatmmonitor",
            "MyAtmMonitor");
        string[] forbiddenReferences =
        [
            "MyAtmOutboxMessageEntity",
            "IMyAtmOutbox",
            "MyAtmOutboxDispatcher",
            "context.OutboxMessages"
        ];

        string[] violations = [.. Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOutput(path, productionRoot))
            .SelectMany(path => forbiddenReferences
                .Where(reference => File.ReadAllText(path).Contains(reference, StringComparison.Ordinal))
                .Select(reference => $"{Path.GetRelativePath(repositoryRoot, path)}: {reference}"))
            .Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    private static bool IsGeneratedOutput(string path, string productionRoot)
    {
        string relativePath = Path.GetRelativePath(productionRoot, path).Replace('\\', '/');
        return relativePath.StartsWith("bin/", StringComparison.Ordinal) ||
            relativePath.StartsWith("obj/", StringComparison.Ordinal);
    }

    private static bool IsPrimaryRepositoryProject(string path, string repositoryRoot)
    {
        string relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
        return !relativePath.StartsWith(".worktrees/", StringComparison.Ordinal);
    }

}
