using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Infrastructure.Communications;
using Rvt.Monitor.Common.Infrastructure.Email.MicrosoftGraph;
using Rvt.Monitor.Common.Infrastructure.Email.SendGrid;
using Rvt.Monitor.Common.Infrastructure.Sms;

namespace Rvt.Monitor.Common.InfrastructureTests.Communications;

[TestClass]
public sealed class CommunicationsServiceCollectionExtensionsTests
{
    [TestMethod]
    public async Task AddMonitorCommunications_DefaultsToSendGridAndRegistersCompleteGraph()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["RVT:EMAIL_ENABLED"] = "true",
                ["RVT:SENDGRID_API_KEY"] = "test-key",
                ["RVT:FROM_EMAIL"] = "sender@example.test",
                ["RVT:SMS_ENABLED"] = "false"
            }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddMonitorCommunications();
        await using var provider = services.BuildServiceProvider();

        Assert.IsInstanceOfType<SendGridEmailAdapter>(provider.GetRequiredService<IEmailDeliveryPort>());
        Assert.IsInstanceOfType<TransmitSmsAdapter>(provider.GetRequiredService<ISmsDeliveryPort>());
        Assert.IsInstanceOfType<NotificationMessageComposer>(
            provider.GetRequiredService<INotificationMessageComposer>());
        Assert.IsInstanceOfType<NotificationDeliveryService>(
            provider.GetRequiredService<INotificationDeliveryService>());
        Assert.IsInstanceOfType<MessageService>(provider.GetRequiredService<IMessageService>());
        var validator = provider.GetServices<IHostedService>()
            .Single(service => service is CommunicationsStartupValidationService);
        await validator.StartAsync(CancellationToken.None);
    }

    [TestMethod]
    public void AddMonitorCommunications_SelectsMicrosoftGraphFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["RVT:EMAIL_PROVIDER"] = "MicrosoftGraph",
                ["RVT:MICROSOFT_TENANT_ID"] = "tenant",
                ["RVT:MICROSOFT_CLIENT_ID"] = "client",
                ["RVT:MICROSOFT_CLIENT_SECRET"] = "secret",
                ["RVT:MICROSOFT_SENDER_ADDRESS"] = "sender@example.test",
                ["RVT:SMS_ENABLED"] = "false"
            }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddMonitorCommunications();
        using var provider = services.BuildServiceProvider();

        Assert.IsInstanceOfType<MicrosoftGraphEmailAdapter>(
            provider.GetRequiredService<IEmailDeliveryPort>());
    }
}
