using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Monitor.Common.Storage;

namespace Rvt.Monitor.CommonTests.Storage;

[TestClass]
public sealed class BlobStorageServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddMonitorBlobStorage_WhenNoProviderIsConfigured_RegistersLocalSingletons()
    {
        using var provider = CreateProvider();

        var options = provider.GetRequiredService<BlobStorageOptions>();
        var firstService = provider.GetRequiredService<IBlobStorageService>();
        var secondService = provider.GetRequiredService<IBlobStorageService>();

        Assert.AreEqual(BlobStorageProvider.Local, options.Provider);
        Assert.IsInstanceOfType<LocalFileBlobStorageService>(firstService);
        Assert.AreSame(firstService, secondService);
        Assert.AreSame(options, provider.GetRequiredService<BlobStorageOptions>());
    }

    [TestMethod]
    public void AddMonitorBlobStorage_WithOptionsFactory_RegistersCustomDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddMonitorBlobStorage(value => BlobStorageOptions.Bind(
            value,
            defaultContainer: "pdfreports",
            defaultPrefix: "rvtreports",
            legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME"));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<BlobStorageOptions>();

        Assert.AreEqual("pdfreports", options.Container);
        Assert.AreEqual("rvtreports", options.Prefix);
        Assert.IsInstanceOfType<LocalFileBlobStorageService>(provider.GetRequiredService<IBlobStorageService>());
    }

    [TestMethod]
    public void AddMonitorBlobStorage_WhenAzureConnectionStringIsConfigured_RegistersAzureAdapter()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "AzureBlob",
            ["RVT__BLOB_CONNECTION_STRING"] = "UseDevelopmentStorage=true"
        });

        var service = provider.GetRequiredService<IBlobStorageService>();

        Assert.IsInstanceOfType<AzureBlobStorageService>(service);
    }

    [TestMethod]
    public void AddMonitorBlobStorage_WhenAzureServiceUriIsConfigured_RegistersAzureAdapter()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "AzureBlob",
            ["RVT__BLOB_SERVICE_URI"] = "https://storage.example.test"
        });

        var service = provider.GetRequiredService<IBlobStorageService>();

        Assert.IsInstanceOfType<AzureBlobStorageService>(service);
    }

    [TestMethod]
    public void AddMonitorBlobStorage_WhenAzureConnectionStringAndInvalidServiceUriAreConfigured_PrefersConnectionString()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "AzureBlob",
            ["RVT__BLOB_CONNECTION_STRING"] = "UseDevelopmentStorage=true",
            ["RVT__BLOB_SERVICE_URI"] = "not-an-absolute-uri"
        });

        var service = provider.GetRequiredService<IBlobStorageService>();

        Assert.IsInstanceOfType<AzureBlobStorageService>(service);
    }

    [TestMethod]
    public void AddMonitorBlobStorage_WhenAzureSettingsAreMissing_ThrowsOnResolution()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "AzureBlob"
        });

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            provider.GetRequiredService<IBlobStorageService>());

        Assert.Contains("BLOB_CONNECTION_STRING", exception.Message);
        Assert.Contains("BLOB_SERVICE_URI", exception.Message);
    }

    [TestMethod]
    public void AddMonitorBlobStorage_WhenS3BucketIsConfigured_RegistersS3Adapter()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "S3",
            ["RVT__S3_BUCKET"] = "recordings",
            ["RVT__S3_REGION"] = "eu-west-1"
        });

        var service = provider.GetRequiredService<IBlobStorageService>();

        Assert.IsInstanceOfType<S3BlobStorageService>(service);
    }

    [TestMethod]
    public void AddMonitorBlobStorage_WhenS3BucketIsMissing_ThrowsOnResolution()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "S3"
        });

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            provider.GetRequiredService<IBlobStorageService>());

        Assert.Contains("RVT__S3_BUCKET", exception.Message);
    }

    [TestMethod]
    public void AddMonitorBlobStorage_WhenCloudPrefixContainsTraversal_ThrowsOnResolution()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "S3",
            ["RVT__S3_BUCKET"] = "recordings",
            ["RVT__BLOB_PREFIX"] = "../outside"
        });

        Assert.ThrowsExactly<ArgumentException>(() =>
            provider.GetRequiredService<IBlobStorageService>());
    }

    [TestMethod]
    public void AddMonitorBlobStorage_WhenProviderIsInvalid_ThrowsOnResolution()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "GoogleCloud"
        });

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            provider.GetRequiredService<IBlobStorageService>());

        Assert.Contains("GoogleCloud", exception.Message);
    }

    [TestMethod]
    public async Task AddMonitorBlobStorage_WhenLocalProviderIsConfigured_StartupValidationSucceeds()
    {
        using var provider = CreateProvider();

        await GetStartupValidator(provider).StartAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task AddMonitorBlobStorage_WhenAzureSettingsAreMissing_StartupValidationFails()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "AzureBlob"
        });

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            GetStartupValidator(provider).StartAsync(CancellationToken.None));

        Assert.Contains("BLOB_CONNECTION_STRING", exception.Message);
        Assert.Contains("BLOB_SERVICE_URI", exception.Message);
    }

    [TestMethod]
    public async Task AddMonitorBlobStorage_WhenS3BucketIsMissing_StartupValidationFails()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "S3"
        });

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            GetStartupValidator(provider).StartAsync(CancellationToken.None));

        Assert.Contains("RVT__S3_BUCKET", exception.Message);
    }

    [TestMethod]
    public async Task AddMonitorBlobStorage_WhenProviderIsInvalid_StartupValidationFails()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "GoogleCloud"
        });

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            GetStartupValidator(provider).StartAsync(CancellationToken.None));

        Assert.Contains("GoogleCloud", exception.Message);
    }

    private static ServiceProvider CreateProvider(Dictionary<string, string?>? values = null)
    {
        var configurationBuilder = new ConfigurationBuilder();
        if (values is not null)
        {
            configurationBuilder.AddInMemoryCollection(values);
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configurationBuilder.Build());
        services.AddMonitorBlobStorage();
        return services.BuildServiceProvider();
    }

    private static IHostedService GetStartupValidator(IServiceProvider provider) =>
        provider.GetServices<IHostedService>().Single();
}
