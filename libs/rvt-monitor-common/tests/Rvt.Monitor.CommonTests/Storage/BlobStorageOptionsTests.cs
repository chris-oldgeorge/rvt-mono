using Microsoft.Extensions.Configuration;
using Rvt.Monitor.Common.Storage;

namespace Rvt.Monitor.CommonTests.Storage;

[TestClass]
public sealed class BlobStorageOptionsTests
{
    [TestMethod]
    public void Bind_WhenNoSettings_UsesLocalDefaults()
    {
        var options = BlobStorageOptions.Bind(new ConfigurationBuilder().Build());

        Assert.AreEqual(BlobStorageProvider.Local, options.Provider);
        Assert.AreEqual("audiofiles", options.Container);
        Assert.AreEqual(string.Empty, options.Prefix);
        Assert.AreEqual("/data/rvt/blobs", options.LocalRoot);
    }

    [TestMethod]
    public void Bind_UsesLegacyAudioFolderAsContainerAlias()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT:AUDIO_FOLDER"] = "legacy-audio"
            })
            .Build();

        var options = BlobStorageOptions.Bind(configuration);

        Assert.AreEqual("legacy-audio", options.Container);
    }

    [TestMethod]
    public void Bind_WithCustomDefaults_UsesReportingDefaults()
    {
        var options = BlobStorageOptions.Bind(
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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT:BLOB_REPORT_CONTAINER_NAME"] = "legacy-reports"
            })
            .Build();

        var options = BlobStorageOptions.Bind(
            configuration,
            defaultContainer: "pdfreports",
            defaultPrefix: "rvtreports",
            legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME");

        Assert.AreEqual("legacy-reports", options.Container);
        Assert.AreEqual("rvtreports", options.Prefix);
    }

    [TestMethod]
    public void Bind_PrefersExplicitContainerOverLegacyAudioFolder()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobStorage:Container"] = "provider-neutral",
                ["RVT:AUDIO_FOLDER"] = "legacy-audio"
            })
            .Build();

        var options = BlobStorageOptions.Bind(configuration);

        Assert.AreEqual("provider-neutral", options.Container);
    }

    [TestMethod]
    public void Bind_ReadsAzureValuesFromRvtEnvironmentKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT__BLOB_PROVIDER"] = "AzureBlob",
                ["RVT__BLOB_CONTAINER"] = "recordings",
                ["RVT__BLOB_PREFIX"] = "tenant-a",
                ["RVT__BLOB_LOCAL_ROOT"] = "/var/lib/rvt",
                ["RVT__BLOB_CONNECTION_STRING"] = "UseDevelopmentStorage=true",
                ["RVT__BLOB_SERVICE_URI"] = "https://storage.example.test"
            })
            .Build();

        var options = BlobStorageOptions.Bind(configuration);

        Assert.AreEqual(BlobStorageProvider.AzureBlob, options.Provider);
        Assert.AreEqual("recordings", options.Container);
        Assert.AreEqual("tenant-a", options.Prefix);
        Assert.AreEqual("/var/lib/rvt", options.LocalRoot);
        Assert.AreEqual("UseDevelopmentStorage=true", options.AzureConnectionString);
        Assert.AreEqual("https://storage.example.test", options.AzureServiceUri);
    }

    [TestMethod]
    public void Bind_ReadsS3ValuesFromBlobStorageConfigurationKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobStorage:Provider"] = "S3",
                ["BlobStorage:Container"] = "audio",
                ["BlobStorage:Prefix"] = "tenant-b",
                ["BlobStorage:S3Bucket"] = "bucket-a",
                ["BlobStorage:S3Region"] = "eu-west-1",
                ["BlobStorage:S3ServiceUrl"] = "http://localhost:9000",
                ["BlobStorage:S3ForcePathStyle"] = "true"
            })
            .Build();

        var options = BlobStorageOptions.Bind(configuration);

        Assert.AreEqual(BlobStorageProvider.S3, options.Provider);
        Assert.AreEqual("audio", options.Container);
        Assert.AreEqual("tenant-b", options.Prefix);
        Assert.AreEqual("bucket-a", options.S3Bucket);
        Assert.AreEqual("eu-west-1", options.S3Region);
        Assert.AreEqual("http://localhost:9000", options.S3ServiceUrl);
        Assert.IsTrue(options.S3ForcePathStyle);
    }

    [TestMethod]
    public void Bind_ThrowsForUnknownProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobStorage:Provider"] = "GoogleCloud"
            })
            .Build();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            BlobStorageOptions.Bind(configuration));

        Assert.Contains("GoogleCloud", exception.Message);
        Assert.Contains("Local", exception.Message);
        Assert.Contains("AzureBlob", exception.Message);
        Assert.Contains("S3", exception.Message);
    }

    [TestMethod]
    public void Bind_ThrowsForNumericProviderValue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobStorage:Provider"] = "99"
            })
            .Build();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            BlobStorageOptions.Bind(configuration));

        Assert.Contains("99", exception.Message);
    }
}
