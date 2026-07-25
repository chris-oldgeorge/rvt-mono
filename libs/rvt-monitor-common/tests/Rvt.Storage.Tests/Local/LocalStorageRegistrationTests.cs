using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Storage.Local;

namespace Rvt.Storage.Tests.Local;

[TestClass]
public sealed class LocalStorageRegistrationTests
{
    [TestMethod]
    public void AddRvtLocalStorage_WithFactory_RegistersExactlyOneNamedClient()
    {
        using var provider = CreateProvider(services =>
            services.AddRvtLocalStorage(
                "recordings",
                _ => new LocalStorageOptions { RootPath = "/tmp/rvt-tests" }));

        var registrations = provider.GetServices<ObjectStorageClientRegistration>().ToArray();

        Assert.HasCount(1, registrations);
        Assert.AreEqual("recordings", registrations[0].ResourceName);
        Assert.IsInstanceOfType<LocalObjectStorageClient>(registrations[0].Client);
    }

    [TestMethod]
    public void AddRvtLocalStorage_FactoryReturnsTheKeyedSingleton()
    {
        using var provider = CreateProvider(services =>
            services.AddRvtLocalStorage(
                "recordings",
                new LocalStorageOptions { RootPath = "/tmp/rvt-tests" }));

        var factoryClient = provider
            .GetRequiredService<IObjectStorageClientFactory>()
            .GetRequiredClient("recordings");
        var keyedClient = provider.GetRequiredKeyedService<LocalObjectStorageClient>("recordings");

        Assert.AreSame(keyedClient, factoryClient);
        Assert.AreSame(factoryClient, provider
            .GetRequiredService<IObjectStorageClientFactory>()
            .GetRequiredClient("recordings"));
    }

    [TestMethod]
    public async Task AddRvtLocalStorage_HostStartupResolvesAndValidatesNamedClient()
    {
        using var provider = CreateProvider(services =>
            services.AddRvtLocalStorage(
                "recordings",
                new LocalStorageOptions { RootPath = "/tmp/rvt-tests" }));

        var hostedService = provider.GetServices<IHostedService>().Single();

        await hostedService.StartAsync(CancellationToken.None);
        Assert.IsInstanceOfType<LocalObjectStorageClient>(
            provider.GetRequiredService<IObjectStorageClientFactory>().GetRequiredClient("recordings"));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void AddRvtLocalStorage_WhenResourceNameIsBlank_ThrowsAtRegistration(string resourceName)
    {
        var services = new ServiceCollection();

        var exception = Assert.ThrowsExactly<ArgumentException>(() =>
            services.AddRvtLocalStorage(resourceName, new LocalStorageOptions()));

        Assert.AreEqual("resourceName", exception.ParamName);
    }

    private static ServiceProvider CreateProvider(
        Action<IServiceCollection> configureServices)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        configureServices(services);
        return services.BuildServiceProvider();
    }
}
