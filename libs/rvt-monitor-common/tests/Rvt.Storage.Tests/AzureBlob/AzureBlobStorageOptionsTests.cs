using Microsoft.Extensions.Configuration;
using Rvt.Storage.AzureBlob;

namespace Rvt.Storage.Tests.AzureBlob;

[TestClass]
public sealed class AzureBlobStorageOptionsTests
{
    [TestMethod]
    public void Bind_WhenNoSettings_UsesAzureDefaults()
    {
        AzureBlobStorageOptions options = AzureBlobStorageOptions.Bind(new ConfigurationBuilder().Build());

        Assert.AreEqual("audiofiles", options.Container);
        Assert.AreEqual(string.Empty, options.Prefix);
        Assert.AreEqual(string.Empty, options.ConnectionString);
        Assert.AreEqual(string.Empty, options.ServiceUri);
    }

    [TestMethod]
    public void Bind_ReadsProviderNeutralSettings()
    {
        AzureBlobStorageOptions options = AzureBlobStorageOptions.Bind(CreateConfiguration(new()
        {
            ["BlobStorage:Container"] = "recordings",
            ["BlobStorage:Prefix"] = "tenant-a",
            ["BlobStorage:AzureConnectionString"] = "UseDevelopmentStorage=true",
            ["BlobStorage:AzureServiceUri"] = "https://neutral.example.test",
        }));

        Assert.AreEqual("recordings", options.Container);
        Assert.AreEqual("tenant-a", options.Prefix);
        Assert.AreEqual("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.AreEqual("https://neutral.example.test", options.ServiceUri);
    }

    [TestMethod]
    public void Bind_ReadsRvtColonAliases()
    {
        AzureBlobStorageOptions options = AzureBlobStorageOptions.Bind(CreateConfiguration(new()
        {
            ["RVT:BLOB_CONTAINER"] = "recordings",
            ["RVT:BLOB_PREFIX"] = "tenant-a",
            ["RVT:BLOB_CONNECTION_STRING"] = "UseDevelopmentStorage=true",
            ["RVT:BLOB_SERVICE_URI"] = "https://colon.example.test",
        }));

        Assert.AreEqual("recordings", options.Container);
        Assert.AreEqual("tenant-a", options.Prefix);
        Assert.AreEqual("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.AreEqual("https://colon.example.test", options.ServiceUri);
    }

    [TestMethod]
    public void Bind_ReadsLiteralRvtDoubleUnderscoreAliases()
    {
        AzureBlobStorageOptions options = AzureBlobStorageOptions.Bind(CreateConfiguration(new()
        {
            ["RVT__BLOB_CONTAINER"] = "recordings",
            ["RVT__BLOB_PREFIX"] = "tenant-a",
            ["RVT__BLOB_CONNECTION_STRING"] = "UseDevelopmentStorage=true",
            ["RVT__BLOB_SERVICE_URI"] = "https://literal.example.test",
        }));

        Assert.AreEqual("recordings", options.Container);
        Assert.AreEqual("tenant-a", options.Prefix);
        Assert.AreEqual("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.AreEqual("https://literal.example.test", options.ServiceUri);
    }

    [TestMethod]
    public void Bind_PrefersProviderNeutralSettingsOverAliases()
    {
        AzureBlobStorageOptions options = AzureBlobStorageOptions.Bind(CreateConfiguration(new()
        {
            ["BlobStorage:Container"] = "neutral-container",
            ["BlobStorage:Prefix"] = "neutral-prefix",
            ["BlobStorage:AzureConnectionString"] = "UseDevelopmentStorage=true",
            ["BlobStorage:AzureServiceUri"] = "https://neutral.example.test",
            ["RVT:BLOB_CONTAINER"] = "colon-container",
            ["RVT:BLOB_PREFIX"] = "colon-prefix",
            ["RVT:BLOB_CONNECTION_STRING"] = "colon-secret",
            ["RVT:BLOB_SERVICE_URI"] = "https://colon.example.test",
            ["RVT__BLOB_CONTAINER"] = "literal-container",
            ["RVT__BLOB_PREFIX"] = "literal-prefix",
            ["RVT__BLOB_CONNECTION_STRING"] = "literal-secret",
            ["RVT__BLOB_SERVICE_URI"] = "https://literal.example.test",
        }));

        Assert.AreEqual("neutral-container", options.Container);
        Assert.AreEqual("neutral-prefix", options.Prefix);
        Assert.AreEqual("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.AreEqual("https://neutral.example.test", options.ServiceUri);
    }

    [TestMethod]
    [DataRow("RVT:AUDIO_FOLDER")]
    [DataRow("RVT__AUDIO_FOLDER")]
    public void Bind_ReadsLegacyContainerAliases(string key)
    {
        AzureBlobStorageOptions options = AzureBlobStorageOptions.Bind(CreateConfiguration(new()
        {
            [key] = "legacy-audio",
        }));

        Assert.AreEqual("legacy-audio", options.Container);
    }

    [TestMethod]
    public void Bind_WithCustomDefaultsAndLegacyKey_UsesReportingValues()
    {
        AzureBlobStorageOptions defaults = AzureBlobStorageOptions.Bind(
            new ConfigurationBuilder().Build(),
            defaultContainer: "pdfreports",
            defaultPrefix: "rvtreports",
            legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME");
        AzureBlobStorageOptions alias = AzureBlobStorageOptions.Bind(
            CreateConfiguration(new()
            {
                ["RVT:BLOB_REPORT_CONTAINER_NAME"] = "legacy-reports",
            }),
            defaultContainer: "pdfreports",
            defaultPrefix: "rvtreports",
            legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME");

        Assert.AreEqual("pdfreports", defaults.Container);
        Assert.AreEqual("rvtreports", defaults.Prefix);
        Assert.AreEqual("legacy-reports", alias.Container);
        Assert.AreEqual("rvtreports", alias.Prefix);
    }

    private static IConfiguration CreateConfiguration(
        Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
