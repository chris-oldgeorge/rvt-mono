using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Monitor.Common.Communications;
using Svantek.Api;

namespace SvantekMonitorTests;

[TestClass]
public sealed class CommunicationsCompositionTests
{
    [TestMethod]
    public async Task AddSvantekMonitor_ResolvesSharedCommunicationsAndValidatesStartup()
    {
        var services = CreateServices();
        services.AddSvantekMonitor();

        using var provider = services.BuildServiceProvider();

        Assert.IsNotNull(provider.GetRequiredService<IEmailDeliveryPort>());
        Assert.IsNotNull(provider.GetRequiredService<ISmsDeliveryPort>());
        Assert.IsNotNull(provider.GetRequiredService<INotificationDeliveryService>());
        Assert.IsNotNull(provider.GetRequiredService<IMessageService>());
        await StartCommunicationsValidatorAsync(services, provider);
    }

    private static ServiceCollection CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT:EMAIL_ENABLED"] = "false",
                ["RVT:SMS_ENABLED"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        return services;
    }

    private static Task StartCommunicationsValidatorAsync(
        IServiceCollection services,
        IServiceProvider provider)
    {
        var implementationType = services.Single(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType?.Name == "CommunicationsStartupValidationService")
            .ImplementationType!;
        var validator = (IHostedService)ActivatorUtilities.CreateInstance(provider, implementationType);
        return validator.StartAsync(CancellationToken.None);
    }
}
