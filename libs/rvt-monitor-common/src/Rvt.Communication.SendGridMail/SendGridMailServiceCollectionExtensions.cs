using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Communication.Abstractions;

namespace Rvt.Communication.SendGridMail;

public static class SendGridMailServiceCollectionExtensions
{
    public static IServiceCollection AddSendGridMail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddSendGridMail(SendGridMailOptions.FromConfiguration(configuration));
    }

    public static IServiceCollection AddSendGridMail(
        this IServiceCollection services,
        SendGridMailOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IEmailDeliveryPort)))
        {
            throw new InvalidOperationException("An email delivery provider is already registered.");
        }

        services.AddSingleton(options);
        services.AddSingleton<ISendGridClientFactory, SendGridClientFactory>();
        services.AddSingleton<SendGridEmailAdapter>();
        services.AddSingleton<IEmailDeliveryPort>(provider =>
            provider.GetRequiredService<SendGridEmailAdapter>());
        services.AddSingleton<IHostedService, SendGridMailStartupValidationService>();
        return services;
    }
}
