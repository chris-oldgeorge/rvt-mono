using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Omnidots.Api;
using Rvt.Monitor.Common.Communications;

namespace OmnidotsMonitorTests.Architecture;

[TestClass]
public sealed class CommunicationsCompositionTests
{
    [TestMethod]
    public async Task AddOmnidotsMonitor_ResolvesSharedCommunicationsAndValidatesStartup()
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
        services.AddOmnidotsMonitor();

        using var provider = services.BuildServiceProvider();

        Assert.IsNotNull(provider.GetRequiredService<IEmailDeliveryPort>());
        Assert.IsNotNull(provider.GetRequiredService<ISmsDeliveryPort>());
        Assert.IsNotNull(provider.GetRequiredService<INotificationDeliveryService>());
        Assert.IsNotNull(provider.GetRequiredService<IMessageService>());
        var implementationType = services.Single(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType?.Name == "CommunicationsStartupValidationService")
            .ImplementationType!;
        var validator = (IHostedService)ActivatorUtilities.CreateInstance(provider, implementationType);
        await validator.StartAsync(CancellationToken.None);
    }
}
