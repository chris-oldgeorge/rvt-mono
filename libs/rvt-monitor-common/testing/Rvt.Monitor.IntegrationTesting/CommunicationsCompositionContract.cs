using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Communication.Abstractions;

namespace Rvt.Monitor.IntegrationTesting;

// Summary: The one communications-composition contract every monitor host test asserts.
// Major updates:
// - 2026-07-29 TestKit consolidation: replaced five per-monitor test copies that
//   differed only in which Add<X>Monitor extension they invoked.

/// <summary>
/// Verifies that a monitor host composes the communications stack correctly:
/// SendGrid by default, Microsoft Graph on request, TransmitSMS always, the
/// provider-neutral workflows resolvable, and the startup validators present
/// and startable. Framework-neutral: failures throw
/// <see cref="InvalidOperationException"/> so MSTest and xUnit callers surface
/// them identically.
/// </summary>
public static class CommunicationsCompositionContract
{
    public static async Task VerifyMissingProviderComposesSendGridSmsAndWorkflowsAsync(
        Action<IServiceCollection, IConfiguration> composeMonitor)
    {
        (ServiceCollection services, IConfiguration configuration) = CreateServices(null);
        composeMonitor(services, configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        RequireImplementation<IEmailDeliveryPort>(provider, "Rvt.Communication.SendGridMail.SendGridEmailAdapter");
        RequireImplementation<ISmsDeliveryPort>(provider, "Rvt.Communication.TransmitSms.TransmitSmsAdapter");
        _ = provider.GetRequiredService<INotificationDeliveryService>();
        await StartValidatorsAsync(services, provider);
    }

    public static void VerifyMicrosoftGraphIsSelectedCaseInsensitively(
        Action<IServiceCollection, IConfiguration> composeMonitor)
    {
        (ServiceCollection services, IConfiguration configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["RVT:EMAIL_PROVIDER"] = "mIcRoSoFtGrApH",
            ["RVT__EMAIL_PROVIDER"] = "invalid-fallback-must-not-win"
        });
        composeMonitor(services, configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        RequireImplementation<IEmailDeliveryPort>(
            provider,
            "Rvt.Communication.MicrosoftGraphMail.MicrosoftGraphEmailAdapter");
        RequireRegisteredValidator(
            services,
            "Rvt.Communication.MicrosoftGraphMail.MicrosoftGraphMailStartupValidationService");
    }

    public static void VerifyInvalidProviderFailsWithTheSafeMessage(
        Action<IServiceCollection, IConfiguration> composeMonitor)
    {
        const string invalidProvider = "sensitive-invalid-provider";
        (ServiceCollection services, IConfiguration configuration) = CreateServices(new Dictionary<string, string?>
        {
            ["RVT__EMAIL_PROVIDER"] = invalidProvider
        });

        InvalidOperationException? failure = null;
        try
        {
            composeMonitor(services, configuration);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
        }

        if (failure is null)
        {
            throw new InvalidOperationException(
                "Composing with an invalid email provider must fail at composition time.");
        }

        if (failure.Message != "RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.")
        {
            throw new InvalidOperationException(
                $"Unexpected invalid-provider message: '{failure.Message}'.");
        }

        if (failure.Message.Contains(invalidProvider, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The invalid-provider message must not echo the configured value.");
        }
    }

    private static (ServiceCollection Services, IConfiguration Configuration) CreateServices(
        IReadOnlyDictionary<string, string?>? settings)
    {
        Dictionary<string, string?> values = new()
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
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        return (services, configuration);
    }

    private static void RequireImplementation<TService>(IServiceProvider provider, string expectedTypeFullName)
        where TService : notnull
    {
        string? actual = provider.GetRequiredService<TService>().GetType().FullName;
        if (actual != expectedTypeFullName)
        {
            throw new InvalidOperationException(
                $"{typeof(TService).Name} resolved to '{actual}' instead of '{expectedTypeFullName}'.");
        }
    }

    // Inspects service DESCRIPTORS rather than resolving IHostedService: some
    // hosts (Omnidots) register hosted services whose construction needs real
    // infrastructure, and this contract must not materialize those.
    private static void RequireRegisteredValidator(IServiceCollection services, string expectedTypeFullName)
    {
        if (!CommunicationValidatorTypes(services)
                .Any(type => type.FullName == expectedTypeFullName))
        {
            throw new InvalidOperationException(
                $"Expected hosted startup validator '{expectedTypeFullName}' was not registered.");
        }
    }

    private static async Task StartValidatorsAsync(IServiceCollection services, IServiceProvider provider)
    {
        RequireRegisteredValidator(services, "Rvt.Communication.SendGridMail.SendGridMailStartupValidationService");
        RequireRegisteredValidator(services, "Rvt.Communication.TransmitSms.TransmitSmsStartupValidationService");
        foreach (Type validatorType in CommunicationValidatorTypes(services))
        {
            IHostedService validator = (IHostedService)ActivatorUtilities.CreateInstance(provider, validatorType);
            await validator.StartAsync(CancellationToken.None);
        }
    }

    private static Type[] CommunicationValidatorTypes(IServiceCollection services) =>
        [.. services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .Where(type => type?.Namespace?.StartsWith("Rvt.Communication.", StringComparison.Ordinal) == true)
            .Cast<Type>()];
}
