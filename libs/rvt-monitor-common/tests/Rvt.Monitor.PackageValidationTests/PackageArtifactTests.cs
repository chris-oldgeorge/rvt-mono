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
    private static readonly string PackageRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly string Artifacts = Path.GetFullPath(
        Path.Combine(PackageRoot, "artifacts/packages"));

    [TestMethod]
    public void PackageCatalogDeclaresTheExactApprovedTrain()
    {
        var rows = File.ReadAllLines(Path.Combine(PackageRoot, "release/package-catalog.tsv"))
            .Select((line, index) =>
            {
                var columns = line.Split('\t');
                Assert.HasCount(2, columns, $"Catalog row {index + 1} must have two tab-separated columns.");
                return columns;
            })
            .ToArray();
        var expectedPackageIds = new[]
        {
            "Rvt.Monitor.Common",
            "Rvt.Monitor.IntegrationTesting",
            "Rvt.Communication.Abstractions",
            "Rvt.Communication",
            "Rvt.Communication.SendGridMail",
            "Rvt.Communication.MicrosoftGraphMail",
            "Rvt.Communication.TransmitSms",
            "Rvt.Storage.Abstractions",
            "Rvt.Storage.Local",
            "Rvt.Storage.AzureBlob",
            "Rvt.Storage.S3"
        };

        CollectionAssert.AreEqual(expectedPackageIds, rows.Select(row => row[0]).ToArray());
        foreach (var row in rows)
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(PackageRoot, row[1])),
                $"Catalog project path does not exist: {row[1]}");
        }
    }

    [TestMethod]
    public void ReleaseContainsExactlyTheSevenTemporaryCompatibilityPackages()
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
            "Rvt.Communication",
            "Rvt.Communication.Abstractions",
            "Rvt.Communication.MicrosoftGraphMail",
            "Rvt.Communication.SendGridMail",
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
    [DataRow("Rvt.Communication", "Rvt.Communication.dll")]
    [DataRow("Rvt.Communication.Abstractions", "Rvt.Communication.Abstractions.dll")]
    [DataRow("Rvt.Communication.MicrosoftGraphMail", "Rvt.Communication.MicrosoftGraphMail.dll")]
    [DataRow("Rvt.Communication.SendGridMail", "Rvt.Communication.SendGridMail.dll")]
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
    [DataRow("Rvt.Communication", "Rvt.Communication.dll")]
    [DataRow("Rvt.Communication.Abstractions", "Rvt.Communication.Abstractions.dll")]
    [DataRow("Rvt.Communication.MicrosoftGraphMail", "Rvt.Communication.MicrosoftGraphMail.dll")]
    [DataRow("Rvt.Communication.SendGridMail", "Rvt.Communication.SendGridMail.dll")]
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
    public void InfrastructureDependenciesStartAtTheSynchronizedCompatibilityVersion()
    {
        using var archive = Open("Rvt.Monitor.Common.Infrastructure");
        var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);
        var dependencies = document.Descendants()
            .Where(element => element.Name.LocalName == "dependency")
            .Where(element => (string?)element.Attribute("id") is "Rvt.Monitor.Common" or "Rvt.Communication" or "Rvt.Communication.MicrosoftGraphMail" or "Rvt.Communication.SendGridMail")
            .ToArray();

        Assert.AreEqual(4, dependencies.Length);
        Assert.IsTrue(dependencies.All(dependency =>
            string.Equals($"[{Version}]", (string?)dependency.Attribute("version"), StringComparison.Ordinal)));
    }

    private static ZipArchive Open(string packageId) => ZipFile.OpenRead(
        Path.Combine(Artifacts, $"{packageId}.{Version}.nupkg"));
}
