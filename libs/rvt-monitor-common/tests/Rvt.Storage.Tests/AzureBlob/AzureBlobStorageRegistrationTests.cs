using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Storage.AzureBlob;

namespace Rvt.Storage.Tests.AzureBlob;

[TestClass]
public sealed class AzureBlobStorageRegistrationTests
{
    [TestMethod]
    public void AddRvtAzureBlobStorage_WithFactory_RegistersExactlyOneNamedClient()
    {
        using var provider = CreateProvider(services =>
            services.AddRvtAzureBlobStorage(
                "recordings",
                _ => ConnectionStringOptions()));

        var registrations = provider.GetServices<ObjectStorageClientRegistration>().ToArray();

        Assert.HasCount(1, registrations);
        Assert.AreEqual("recordings", registrations[0].ResourceName);
        Assert.IsInstanceOfType<AzureBlobObjectStorageClient>(registrations[0].Client);
    }

    [TestMethod]
    public void AddRvtAzureBlobStorage_FactoryReturnsTheKeyedSingleton()
    {
        using var provider = CreateProvider(services =>
            services.AddRvtAzureBlobStorage("recordings", ConnectionStringOptions()));

        var factoryClient = provider
            .GetRequiredService<IObjectStorageClientFactory>()
            .GetRequiredClient("recordings");
        var keyedClient =
            provider.GetRequiredKeyedService<AzureBlobObjectStorageClient>("recordings");

        Assert.AreSame(keyedClient, factoryClient);
        Assert.AreSame(factoryClient, provider
            .GetRequiredService<IObjectStorageClientFactory>()
            .GetRequiredClient("recordings"));
    }

    [TestMethod]
    public async Task AddRvtAzureBlobStorage_HostStartupResolvesAndValidatesNamedClient()
    {
        using var provider = CreateProvider(services =>
            services.AddRvtAzureBlobStorage("recordings", ConnectionStringOptions()));

        var hostedService = provider.GetServices<IHostedService>().Single();

        await hostedService.StartAsync(CancellationToken.None);
        Assert.IsInstanceOfType<AzureBlobObjectStorageClient>(
            provider.GetRequiredService<IObjectStorageClientFactory>().GetRequiredClient("recordings"));
    }

    [TestMethod]
    public async Task AddRvtAzureBlobStorage_WhenCredentialsAreMissing_StartupValidationFailsSafely()
    {
        using var provider = CreateProvider(services =>
            services.AddRvtAzureBlobStorage("recordings", new AzureBlobStorageOptions()));

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            provider.GetServices<IHostedService>().Single().StartAsync(CancellationToken.None));

        Assert.Contains("RVT__BLOB_CONNECTION_STRING", exception.Message);
        Assert.Contains("RVT__BLOB_SERVICE_URI", exception.Message);
    }

    [TestMethod]
    public void AddRvtAzureBlobStorage_WhenConnectionStringAndInvalidServiceUriAreConfigured_PrefersConnectionString()
    {
        using var provider = CreateProvider(services =>
            services.AddRvtAzureBlobStorage(
                "recordings",
                ConnectionStringOptions() with
                {
                    ServiceUri = "configured-invalid-service-uri",
                }));

        var client = provider
            .GetRequiredService<IObjectStorageClientFactory>()
            .GetRequiredClient("recordings");

        Assert.IsInstanceOfType<AzureBlobObjectStorageClient>(client);
    }

    [TestMethod]
    public void AddRvtAzureBlobStorage_WhenServiceUriIsNotAbsolute_ThrowsSafely()
    {
        const string configuredValue = "configured-relative-service-uri";
        using var provider = CreateProvider(services =>
            services.AddRvtAzureBlobStorage(
                "recordings",
                new AzureBlobStorageOptions
                {
                    Container = "recordings",
                    ServiceUri = configuredValue,
                }));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            provider.GetRequiredService<IObjectStorageClientFactory>());

        Assert.Contains("RVT__BLOB_SERVICE_URI", exception.Message);
        Assert.DoesNotContain(configuredValue, exception.Message);
    }

    [TestMethod]
    public void AddRvtAzureBlobStorage_WhenContainerIsBlank_ThrowsSafely()
    {
        const string configuredValue = "   ";
        using var provider = CreateProvider(services =>
            services.AddRvtAzureBlobStorage(
                "recordings",
                ConnectionStringOptions() with { Container = configuredValue }));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            provider.GetRequiredService<IObjectStorageClientFactory>());

        Assert.Contains("container", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(configuredValue, exception.Message);
    }

    [TestMethod]
    public void AddRvtAzureBlobStorage_WhenPrefixContainsTraversal_ThrowsSafely()
    {
        const string configuredValue = "../configured-prefix";
        using var provider = CreateProvider(services =>
            services.AddRvtAzureBlobStorage(
                "recordings",
                ConnectionStringOptions() with { Prefix = configuredValue }));

        var exception = Assert.ThrowsExactly<ArgumentException>(() =>
            provider.GetRequiredService<IObjectStorageClientFactory>());

        Assert.DoesNotContain(configuredValue, exception.Message);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void AddRvtAzureBlobStorage_WhenResourceNameIsBlank_ThrowsAtRegistration(
        string resourceName)
    {
        var services = new ServiceCollection();

        var exception = Assert.ThrowsExactly<ArgumentException>(() =>
            services.AddRvtAzureBlobStorage(resourceName, ConnectionStringOptions()));

        Assert.AreEqual("resourceName", exception.ParamName);
    }

    private static AzureBlobStorageOptions ConnectionStringOptions() =>
        new()
        {
            Container = " recordings ",
            ConnectionString = "UseDevelopmentStorage=true",
        };

    private static ServiceProvider CreateProvider(
        Action<IServiceCollection> configureServices)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        configureServices(services);
        return services.BuildServiceProvider();
    }
}
