using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReportingMonitor.Api;
using Rvt.Communication.Abstractions;

namespace ReportingMonitorTests;

public sealed class CommunicationsCompositionTests
{
    [Fact]
    public async Task AddReportingMonitor_MissingProvider_ComposesSendGridSmsAndWorkflows()
    {
        var (services, configuration) = CreateServices();
        services.AddReportingMonitor(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Equal(
            "Rvt.Communication.SendGridMail.SendGridEmailAdapter",
            provider.GetRequiredService<IEmailDeliveryPort>().GetType().FullName);
        Assert.Equal(
            "Rvt.Communication.TransmitSms.TransmitSmsAdapter",
            provider.GetRequiredService<ISmsDeliveryPort>().GetType().FullName);
        Assert.NotNull(provider.GetRequiredService<INotificationDeliveryService>());
        Assert.NotNull(provider.GetRequiredService<IMessageService>());
        await StartValidatorsAsync(provider);
    }

    [Fact]
    public void AddReportingMonitor_MicrosoftGraphCaseInsensitive_ComposesMicrosoftGraph()
    {
        var (services, configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["RVT:EMAIL_PROVIDER"] = "mIcRoSoFtGrApH",
            ["RVT__EMAIL_PROVIDER"] = "invalid-fallback-must-not-win"
        });

        services.AddReportingMonitor(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Equal(
            "Rvt.Communication.MicrosoftGraphMail.MicrosoftGraphEmailAdapter",
            provider.GetRequiredService<IEmailDeliveryPort>().GetType().FullName);
        Assert.Contains(provider.GetServices<IHostedService>(),
            service => service.GetType().FullName ==
                "Rvt.Communication.MicrosoftGraphMail.MicrosoftGraphMailStartupValidationService");
    }

    [Fact]
    public void AddReportingMonitor_InvalidProvider_ThrowsSafeMessageAtCompositionTime()
    {
        const string invalidProvider = "sensitive-invalid-provider";
        var (services, configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["RVT__EMAIL_PROVIDER"] = invalidProvider
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddReportingMonitor(configuration));

        Assert.Equal("RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.", exception.Message);
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
        Assert.Contains(validators, service => service.GetType().FullName ==
            "Rvt.Communication.SendGridMail.SendGridMailStartupValidationService");
        Assert.Contains(validators, service => service.GetType().FullName ==
            "Rvt.Communication.TransmitSms.TransmitSmsStartupValidationService");
        foreach (var validator in validators)
        {
            await validator.StartAsync(CancellationToken.None);
        }
    }
}
