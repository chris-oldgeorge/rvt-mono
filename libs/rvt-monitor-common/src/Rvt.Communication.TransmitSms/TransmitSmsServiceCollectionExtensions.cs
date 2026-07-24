using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Communication.Abstractions;

namespace Rvt.Communication.TransmitSms;

public static class TransmitSmsServiceCollectionExtensions
{
    public static IServiceCollection AddTransmitSms(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddTransmitSms(TransmitSmsOptions.FromConfiguration(configuration));
    }

    public static IServiceCollection AddTransmitSms(
        this IServiceCollection services,
        TransmitSmsOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ISmsDeliveryPort)))
        {
            throw new InvalidOperationException("An SMS delivery provider is already registered.");
        }

        services.AddSingleton(options);
        services.AddHttpClient<TransmitSmsAdapter>();
        services.AddSingleton<ISmsDeliveryPort>(provider =>
            provider.GetRequiredService<TransmitSmsAdapter>());
        services.AddSingleton<IHostedService, TransmitSmsStartupValidationService>();
        return services;
    }
}
