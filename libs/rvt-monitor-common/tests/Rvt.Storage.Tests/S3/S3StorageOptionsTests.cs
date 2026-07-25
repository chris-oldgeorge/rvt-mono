using Microsoft.Extensions.Configuration;
using Rvt.Storage.S3;

namespace Rvt.Storage.Tests.S3;

[TestClass]
public sealed class S3StorageOptionsTests
{
    [TestMethod]
    public void Bind_WhenNoSettings_UsesEmptyOptionalDefaults()
    {
        var options = S3StorageOptions.Bind(new ConfigurationBuilder().Build());

        Assert.AreEqual(string.Empty, options.Bucket);
        Assert.AreEqual(string.Empty, options.Prefix);
        Assert.AreEqual(string.Empty, options.Region);
        Assert.AreEqual(string.Empty, options.ServiceUrl);
        Assert.IsFalse(options.ForcePathStyle);
    }

    [TestMethod]
    public void Bind_ReadsProviderNeutralSettings()
    {
        var options = S3StorageOptions.Bind(CreateConfiguration(new()
        {
            ["BlobStorage:S3Bucket"] = "recordings",
            ["BlobStorage:Prefix"] = "tenant-a",
            ["BlobStorage:S3Region"] = "eu-west-1",
            ["BlobStorage:S3ServiceUrl"] = "https://s3.example.test",
            ["BlobStorage:S3ForcePathStyle"] = "true",
        }));

        Assert.AreEqual("recordings", options.Bucket);
        Assert.AreEqual("tenant-a", options.Prefix);
        Assert.AreEqual("eu-west-1", options.Region);
        Assert.AreEqual("https://s3.example.test", options.ServiceUrl);
        Assert.IsTrue(options.ForcePathStyle);
    }

    [TestMethod]
    [DataRow("RVT:S3_BUCKET", "RVT:S3_REGION", "RVT:S3_SERVICE_URL", "RVT:S3_FORCE_PATH_STYLE")]
    [DataRow("RVT__S3_BUCKET", "RVT__S3_REGION", "RVT__S3_SERVICE_URL", "RVT__S3_FORCE_PATH_STYLE")]
    public void Bind_ReadsRvtAliases(
        string bucketKey,
        string regionKey,
        string serviceUrlKey,
        string forcePathStyleKey)
    {
        var options = S3StorageOptions.Bind(CreateConfiguration(new()
        {
            [bucketKey] = "recordings",
            ["RVT__BLOB_PREFIX"] = "tenant-b",
            [regionKey] = "us-east-1",
            [serviceUrlKey] = "http://localhost:9000",
            [forcePathStyleKey] = "true",
        }));

        Assert.AreEqual("recordings", options.Bucket);
        Assert.AreEqual("tenant-b", options.Prefix);
        Assert.AreEqual("us-east-1", options.Region);
        Assert.AreEqual("http://localhost:9000", options.ServiceUrl);
        Assert.IsTrue(options.ForcePathStyle);
    }

    [TestMethod]
    public void Bind_WithDefaultPrefix_UsesDefaultWhenNoPrefixIsConfigured()
    {
        var options = S3StorageOptions.Bind(
            new ConfigurationBuilder().Build(),
            defaultPrefix: "rvtreports");

        Assert.AreEqual("rvtreports", options.Prefix);
    }

    [TestMethod]
    public void Bind_InvalidForcePathStyle_UsesFalse()
    {
        var options = S3StorageOptions.Bind(CreateConfiguration(new()
        {
            ["RVT__S3_FORCE_PATH_STYLE"] = "not-a-boolean",
        }));

        Assert.IsFalse(options.ForcePathStyle);
    }

    [TestMethod]
    public void OptionsSurface_DoesNotAcceptStaticCredentials()
    {
        var propertyNames = typeof(S3StorageOptions)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.IsFalse(propertyNames.Any(name =>
            name.Contains("Access", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Credential", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Token", StringComparison.OrdinalIgnoreCase)));
    }

    private static IConfiguration CreateConfiguration(
        Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
