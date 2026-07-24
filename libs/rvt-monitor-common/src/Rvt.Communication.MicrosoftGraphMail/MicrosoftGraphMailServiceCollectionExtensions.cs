using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Communication.Abstractions;

namespace Rvt.Communication.MicrosoftGraphMail;

public static class MicrosoftGraphMailServiceCollectionExtensions
{
    public static IServiceCollection AddMicrosoftGraphMail(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddMicrosoftGraphMail(MicrosoftGraphMailOptions.FromConfiguration(configuration));
    }

    public static IServiceCollection AddMicrosoftGraphMail(this IServiceCollection services, MicrosoftGraphMailOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IEmailDeliveryPort)))
        {
            throw new InvalidOperationException("An email delivery provider is already registered.");
        }

        services.AddSingleton(options);
        services.AddSingleton<IMicrosoftGraphAccessTokenProvider, AzureIdentityGraphAccessTokenProvider>();
        services.AddHttpClient<MicrosoftGraphEmailAdapter>(client =>
            client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"));
        services.AddSingleton<IEmailDeliveryPort>(provider => provider.GetRequiredService<MicrosoftGraphEmailAdapter>());
        services.AddSingleton<IHostedService, MicrosoftGraphMailStartupValidationService>();
        return services;
    }
}
