using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Storage.S3;

namespace Rvt.Storage.Tests.S3;

[TestClass]
public sealed class S3StorageRegistrationTests
{
    [TestMethod]
    public void AddRvtS3Storage_WithFactory_RegistersExactlyOneNamedClient()
    {
        using ServiceProvider provider = CreateProvider(services =>
            services.AddRvtS3Storage("recordings", _ => ValidOptions()));

        ObjectStorageClientRegistration[] registrations = [.. provider.GetServices<ObjectStorageClientRegistration>()];

        Assert.HasCount(1, registrations);
        Assert.AreEqual("recordings", registrations[0].ResourceName);
        Assert.IsInstanceOfType<S3ObjectStorageClient>(registrations[0].Client);
    }

    [TestMethod]
    public void AddRvtS3Storage_FactoryReturnsTheKeyedSingleton()
    {
        using ServiceProvider provider = CreateProvider(services =>
            services.AddRvtS3Storage("recordings", ValidOptions()));

        IObjectStorageClient factoryClient = provider
            .GetRequiredService<IObjectStorageClientFactory>()
            .GetRequiredClient("recordings");
        S3ObjectStorageClient keyedClient =
            provider.GetRequiredKeyedService<S3ObjectStorageClient>("recordings");

        Assert.AreSame(keyedClient, factoryClient);
        Assert.AreSame(factoryClient, provider
            .GetRequiredService<IObjectStorageClientFactory>()
            .GetRequiredClient("recordings"));
    }

    [TestMethod]
    public async Task AddRvtS3Storage_HostStartupResolvesAndValidatesNamedClient()
    {
        using ServiceProvider provider = CreateProvider(services =>
            services.AddRvtS3Storage("recordings", ValidOptions()));

        IHostedService hostedService = provider.GetServices<IHostedService>().Single();

        await hostedService.StartAsync(CancellationToken.None);
        Assert.IsInstanceOfType<S3ObjectStorageClient>(
            provider.GetRequiredService<IObjectStorageClientFactory>().GetRequiredClient("recordings"));
    }

    [TestMethod]
    public async Task AddRvtS3Storage_WhenBucketIsMissing_StartupValidationFailsSafely()
    {
        using ServiceProvider provider = CreateProvider(services =>
            services.AddRvtS3Storage("recordings", new S3StorageOptions()));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            provider.GetServices<IHostedService>().Single().StartAsync(CancellationToken.None));

        Assert.Contains("RVT__S3_BUCKET", exception.Message);
    }

    [TestMethod]
    public void AddRvtS3Storage_WhenPrefixContainsTraversal_ThrowsSafely()
    {
        const string configuredValue = "../configured-prefix";
        using ServiceProvider provider = CreateProvider(services =>
            services.AddRvtS3Storage(
                "recordings",
                ValidOptions() with { Prefix = configuredValue }));

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            provider.GetRequiredService<IObjectStorageClientFactory>());

        Assert.DoesNotContain(configuredValue, exception.Message);
    }

    [TestMethod]
    public void AddRvtS3Storage_WhenServiceUrlIsNotAbsolute_ThrowsSafely()
    {
        const string configuredValue = "configured-relative-service-url";
        using ServiceProvider provider = CreateProvider(services =>
            services.AddRvtS3Storage(
                "recordings",
                ValidOptions() with { ServiceUrl = configuredValue }));

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            provider.GetRequiredService<IObjectStorageClientFactory>());

        Assert.Contains("RVT__S3_SERVICE_URL", exception.Message);
        Assert.DoesNotContain(configuredValue, exception.Message);
    }

    [TestMethod]
    public void CreateClientConfiguration_WithRegionOnly_UsesRegionEndpoint()
    {
        AmazonS3Config config = S3ObjectStorageClient.CreateClientConfiguration(
            ValidOptions() with
            {
                Region = " eu-west-1 ",
                ForcePathStyle = true,
            });

        Assert.AreSame(RegionEndpoint.EUWest1, config.RegionEndpoint);
        Assert.IsNull(config.ServiceURL);
        Assert.IsTrue(config.ForcePathStyle);
    }

    [TestMethod]
    public void CreateClientConfiguration_WithCompatibleService_UsesServiceUrlAndAuthenticationRegion()
    {
        AmazonS3Config config = S3ObjectStorageClient.CreateClientConfiguration(
            ValidOptions() with
            {
                Region = " us-east-1 ",
                ServiceUrl = "https://s3.example.test/base/",
                ForcePathStyle = true,
            });

        Assert.AreEqual("https://s3.example.test/base", config.ServiceURL);
        Assert.AreEqual("us-east-1", config.AuthenticationRegion);
        Assert.IsNull(config.RegionEndpoint);
        Assert.IsTrue(config.ForcePathStyle);
    }

    [TestMethod]
    public void CreateClientConfiguration_WithNeitherRegionNorServiceUrl_LeavesEndpointUnset()
    {
        AmazonS3Config config = S3ObjectStorageClient.CreateClientConfiguration(
            ValidOptions() with { Region = string.Empty });

        Assert.IsNull(config.RegionEndpoint);
        Assert.IsNull(config.ServiceURL);
        Assert.IsNull(config.AuthenticationRegion);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void AddRvtS3Storage_WhenResourceNameIsBlank_ThrowsAtRegistration(
        string resourceName)
    {
        ServiceCollection services = new();

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            services.AddRvtS3Storage(resourceName, ValidOptions()));

        Assert.AreEqual("resourceName", exception.ParamName);
    }

    private static S3StorageOptions ValidOptions() =>
        new()
        {
            Bucket = " recordings ",
            Region = "eu-west-1",
        };

    private static ServiceProvider CreateProvider(
        Action<IServiceCollection> configureServices)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        configureServices(services);
        return services.BuildServiceProvider();
    }
}
