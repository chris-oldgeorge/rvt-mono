using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Omnidots.Api;
using Rvt.Communication.Abstractions;

namespace OmnidotsMonitorTests.Architecture;

[TestClass]
public sealed class CommunicationsCompositionTests
{
    [TestMethod]
    public async Task AddOmnidotsMonitor_MissingProvider_ComposesSendGridSmsAndWorkflows()
    {
        (ServiceCollection? services, IConfiguration? configuration) = CreateServices();
        services.AddOmnidotsMonitor(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.AreEqual(
            "Rvt.Communication.SendGridMail.SendGridEmailAdapter",
            provider.GetRequiredService<IEmailDeliveryPort>().GetType().FullName);
        Assert.AreEqual(
            "Rvt.Communication.TransmitSms.TransmitSmsAdapter",
            provider.GetRequiredService<ISmsDeliveryPort>().GetType().FullName);
        Assert.IsNotNull(provider.GetRequiredService<INotificationDeliveryService>());
        Assert.IsNotNull(provider.GetRequiredService<IMessageService>());
        await StartValidatorsAsync(services, provider);
    }

    [TestMethod]
    public void AddOmnidotsMonitor_MicrosoftGraphCaseInsensitive_ComposesMicrosoftGraph()
    {
        (ServiceCollection? services, IConfiguration? configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["RVT:EMAIL_PROVIDER"] = "mIcRoSoFtGrApH",
            ["RVT__EMAIL_PROVIDER"] = "invalid-fallback-must-not-win"
        });

        services.AddOmnidotsMonitor(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.AreEqual(
            "Rvt.Communication.MicrosoftGraphMail.MicrosoftGraphEmailAdapter",
            provider.GetRequiredService<IEmailDeliveryPort>().GetType().FullName);
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ImplementationType?.FullName ==
                "Rvt.Communication.MicrosoftGraphMail.MicrosoftGraphMailStartupValidationService"));
    }

    [TestMethod]
    public void AddOmnidotsMonitor_InvalidProvider_ThrowsSafeMessageAtCompositionTime()
    {
        const string invalidProvider = "sensitive-invalid-provider";
        (ServiceCollection? services, IConfiguration? configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["RVT__EMAIL_PROVIDER"] = invalidProvider
        });

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            services.AddOmnidotsMonitor(configuration));

        Assert.AreEqual("RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.", exception.Message);
        Assert.DoesNotContain(invalidProvider, exception.Message, StringComparison.Ordinal);
    }

    private static (ServiceCollection Services, IConfiguration Configuration) CreateServices(
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        Dictionary<string, string?> values = new Dictionary<string, string?>
        {
            ["RVT:EMAIL_ENABLED"] = "false",
            ["RVT:SMS_ENABLED"] = "false"
        };
        if (settings is not null)
        {
            foreach (KeyValuePair<string, string?> setting in settings)
            {
                values[setting.Key] = setting.Value;
            }
        }

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        ServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        return (services, configuration);
    }

    private static async Task StartValidatorsAsync(
        IServiceCollection services,
        IServiceProvider provider)
    {
        Type[] validatorTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .Where(type => type?.Namespace?.StartsWith("Rvt.Communication.", StringComparison.Ordinal) == true)
            .Cast<Type>()
            .ToArray();
        Assert.IsTrue(validatorTypes.Any(type => type.FullName ==
            "Rvt.Communication.SendGridMail.SendGridMailStartupValidationService"));
        Assert.IsTrue(validatorTypes.Any(type => type.FullName ==
            "Rvt.Communication.TransmitSms.TransmitSmsStartupValidationService"));
        foreach (Type? validatorType in validatorTypes)
        {
            IHostedService validator = (IHostedService)ActivatorUtilities.CreateInstance(provider, validatorType);
            await validator.StartAsync(CancellationToken.None);
        }
    }
}
