using Microsoft.Extensions.Configuration;
using Rvt.Storage.Local;

namespace Rvt.Storage.Tests.Local;

[TestClass]
public sealed class LocalStorageOptionsTests
{
    [TestMethod]
    public void Bind_WhenNoSettings_UsesLocalDefaults()
    {
        LocalStorageOptions options = LocalStorageOptions.Bind(new ConfigurationBuilder().Build());

        Assert.AreEqual("/data/rvt/blobs", options.RootPath);
        Assert.AreEqual("audiofiles", options.Container);
        Assert.AreEqual(string.Empty, options.Prefix);
    }

    [TestMethod]
    public void Bind_UsesLegacyAudioFolderAsContainerAlias()
    {
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["RVT:AUDIO_FOLDER"] = "legacy-audio",
        });

        LocalStorageOptions options = LocalStorageOptions.Bind(configuration);

        Assert.AreEqual("legacy-audio", options.Container);
    }

    [TestMethod]
    public void Bind_PrefersExplicitContainerOverLegacyAudioFolder()
    {
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["BlobStorage:Container"] = "provider-neutral",
            ["RVT:AUDIO_FOLDER"] = "legacy-audio",
        });

        LocalStorageOptions options = LocalStorageOptions.Bind(configuration);

        Assert.AreEqual("provider-neutral", options.Container);
    }

    [TestMethod]
    public void Bind_ReadsLiteralRvtEnvironmentKeys()
    {
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["RVT__BLOB_LOCAL_ROOT"] = "/var/lib/rvt",
            ["RVT__BLOB_CONTAINER"] = "recordings",
            ["RVT__BLOB_PREFIX"] = "tenant-a",
        });

        LocalStorageOptions options = LocalStorageOptions.Bind(configuration);

        Assert.AreEqual("/var/lib/rvt", options.RootPath);
        Assert.AreEqual("recordings", options.Container);
        Assert.AreEqual("tenant-a", options.Prefix);
    }

    [TestMethod]
    public void Bind_WithCustomDefaults_UsesReportingDefaults()
    {
        LocalStorageOptions options = LocalStorageOptions.Bind(
            new ConfigurationBuilder().Build(),
            defaultContainer: "pdfreports",
            defaultPrefix: "rvtreports",
            legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME");

        Assert.AreEqual("pdfreports", options.Container);
        Assert.AreEqual("rvtreports", options.Prefix);
    }

    [TestMethod]
    public void Bind_WithCustomLegacyKey_UsesReportingContainerAlias()
    {
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["RVT:BLOB_REPORT_CONTAINER_NAME"] = "legacy-reports",
        });

        LocalStorageOptions options = LocalStorageOptions.Bind(
            configuration,
            defaultContainer: "pdfreports",
            defaultPrefix: "rvtreports",
            legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME");

        Assert.AreEqual("legacy-reports", options.Container);
        Assert.AreEqual("rvtreports", options.Prefix);
    }

    private static IConfiguration CreateConfiguration(
        Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
