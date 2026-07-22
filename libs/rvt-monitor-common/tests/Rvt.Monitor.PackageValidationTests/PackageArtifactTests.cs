using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;

namespace Rvt.Monitor.PackageValidationTests;

[TestClass]
public sealed class PackageArtifactTests
{
    private static readonly string Version =
        Environment.GetEnvironmentVariable("RVT_PACKAGE_VERSION") ?? "0.2.0-rc.1";
    private static readonly string Artifacts = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/packages"));

    [TestMethod]
    public void ReleaseContainsExactlyTheThreeCompatibilityPackages()
    {
        var names = Directory.EnumerateFiles(Artifacts, "*.nupkg")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var symbolNames = Directory.EnumerateFiles(Artifacts, "*.snupkg")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var packageIds = new[]
        {
            "Rvt.Monitor.Common",
            "Rvt.Monitor.Common.Infrastructure",
            "Rvt.Monitor.IntegrationTesting"
        };

        CollectionAssert.AreEqual(
            packageIds.Select(id => $"{id}.{Version}.nupkg").Order(StringComparer.Ordinal).ToArray(),
            names);
        CollectionAssert.AreEqual(
            packageIds.Select(id => $"{id}.{Version}.snupkg").Order(StringComparer.Ordinal).ToArray(),
            symbolNames);
    }

    [TestMethod]
    [DataRow("Rvt.Monitor.Common", "Rvt.Monitor.Common.dll")]
    [DataRow("Rvt.Monitor.Common.Infrastructure", "Rvt.Monitor.Common.Infrastructure.dll")]
    [DataRow("Rvt.Monitor.IntegrationTesting", "Rvt.Monitor.IntegrationTesting.dll")]
    public void PackageContainsOnlyItsExpectedNet10Assembly(string packageId, string assemblyName)
    {
        using var archive = Open(packageId);
        var assemblies = archive.Entries
            .Where(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal) &&
                entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { $"lib/net10.0/{assemblyName}" }, assemblies);
        Assert.IsFalse(archive.Entries.Any(entry => entry.FullName.Contains("Tests.dll", StringComparison.Ordinal)));
        Assert.IsFalse(archive.Entries.Any(entry => entry.FullName.EndsWith("appsettings.Development.json", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [DataRow("Rvt.Monitor.Common", "Rvt.Monitor.Common.dll")]
    [DataRow("Rvt.Monitor.Common.Infrastructure", "Rvt.Monitor.Common.Infrastructure.dll")]
    [DataRow("Rvt.Monitor.IntegrationTesting", "Rvt.Monitor.IntegrationTesting.dll")]
    public void PackagedAssemblyInformationalVersionStartsWithRequestedVersion(
        string packageId,
        string assemblyName)
    {
        using var archive = Open(packageId);
        var assemblyEntry = archive.GetEntry($"lib/net10.0/{assemblyName}")
            ?? throw new InvalidOperationException($"{assemblyName} was not found in {packageId}.");
        using var assemblyStream = new MemoryStream();
        using (var entryStream = assemblyEntry.Open())
        {
            entryStream.CopyTo(assemblyStream);
        }

        assemblyStream.Position = 0;
        var loadContext = new AssemblyLoadContext($"{packageId}-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromStream(assemblyStream);
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            Assert.IsNotNull(informationalVersion);
            Assert.IsTrue(
                informationalVersion.Equals(Version, StringComparison.Ordinal) ||
                informationalVersion.StartsWith($"{Version}+", StringComparison.Ordinal),
                $"Expected {assemblyName} informational version '{informationalVersion}' " +
                $"to equal '{Version}' or begin with '{Version}+'.");
        }
        finally
        {
            loadContext.Unload();
        }
    }

    [TestMethod]
    public void InfrastructureDependencyStartsAtTheSynchronizedCommonVersion()
    {
        using var archive = Open("Rvt.Monitor.Common.Infrastructure");
        var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);
        var dependency = document.Descendants().Single(element =>
            element.Name.LocalName == "dependency" &&
            (string?)element.Attribute("id") == "Rvt.Monitor.Common");

        Assert.AreEqual($"[{Version}]", (string?)dependency.Attribute("version"));
    }

    private static ZipArchive Open(string packageId) => ZipFile.OpenRead(
        Path.Combine(Artifacts, $"{packageId}.{Version}.nupkg"));
}
