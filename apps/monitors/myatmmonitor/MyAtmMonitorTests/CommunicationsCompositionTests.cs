using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyAtm.Api;
using Rvt.Communication.Abstractions;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class CommunicationsCompositionTests
{
    [TestMethod]
    public async Task AddMyAtmMonitor_MissingProvider_ComposesSendGridSmsAndWorkflows()
    {
        var (services, configuration) = CreateServices();
        services.AddMyAtmMonitor(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.AreEqual(
            "Rvt.Communication.SendGridMail.SendGridEmailAdapter",
            provider.GetRequiredService<IEmailDeliveryPort>().GetType().FullName);
        Assert.AreEqual(
            "Rvt.Communication.TransmitSms.TransmitSmsAdapter",
            provider.GetRequiredService<ISmsDeliveryPort>().GetType().FullName);
        Assert.IsNotNull(provider.GetRequiredService<INotificationDeliveryService>());
        Assert.IsNotNull(provider.GetRequiredService<IMessageService>());
        await StartValidatorsAsync(provider);
    }

    [TestMethod]
    public void AddMyAtmMonitor_MicrosoftGraphCaseInsensitive_ComposesMicrosoftGraph()
    {
        var (services, configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["RVT:EMAIL_PROVIDER"] = "mIcRoSoFtGrApH",
            ["RVT__EMAIL_PROVIDER"] = "invalid-fallback-must-not-win"
        });

        services.AddMyAtmMonitor(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.AreEqual(
            "Rvt.Communication.MicrosoftGraphMail.MicrosoftGraphEmailAdapter",
            provider.GetRequiredService<IEmailDeliveryPort>().GetType().FullName);
        Assert.IsTrue(provider.GetServices<IHostedService>()
            .Any(service => service.GetType().FullName ==
                "Rvt.Communication.MicrosoftGraphMail.MicrosoftGraphMailStartupValidationService"));
    }

    [TestMethod]
    public void AddMyAtmMonitor_InvalidProvider_ThrowsSafeMessageAtCompositionTime()
    {
        const string invalidProvider = "sensitive-invalid-provider";
        var (services, configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["RVT__EMAIL_PROVIDER"] = invalidProvider
        });

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            services.AddMyAtmMonitor(configuration));

        Assert.AreEqual("RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.", exception.Message);
        Assert.DoesNotContain(invalidProvider, exception.Message, StringComparison.Ordinal);
    }

    private static (ServiceCollection Services, IConfiguration Configuration) CreateServices(
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["RVT:EMAIL_ENABLED"] = "false",
            ["RVT:SMS_ENABLED"] = "false"
        };
        if (settings is not null)
        {
            foreach (var setting in settings)
            {
                values[setting.Key] = setting.Value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        return (services, configuration);
    }

    private static async Task StartValidatorsAsync(IServiceProvider provider)
    {
        var validators = provider.GetServices<IHostedService>().ToArray();
        Assert.IsTrue(validators.Any(service => service.GetType().FullName ==
            "Rvt.Communication.SendGridMail.SendGridMailStartupValidationService"));
        Assert.IsTrue(validators.Any(service => service.GetType().FullName ==
            "Rvt.Communication.TransmitSms.TransmitSmsStartupValidationService"));
        foreach (var validator in validators)
        {
            await validator.StartAsync(CancellationToken.None);
        }
    }
}
