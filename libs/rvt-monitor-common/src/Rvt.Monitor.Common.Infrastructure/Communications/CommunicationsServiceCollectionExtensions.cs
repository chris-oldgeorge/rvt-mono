using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Communication;
using Rvt.Communication.Abstractions;
using Rvt.Communication.MicrosoftGraphMail;
using Rvt.Communication.SendGridMail;
using Rvt.Monitor.Common.Infrastructure.Sms;

namespace Rvt.Monitor.Common.Infrastructure.Communications;

public static class CommunicationsServiceCollectionExtensions
{
    public static IServiceCollection AddMonitorCommunications(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(provider => CommunicationsOptions.FromConfiguration(
            provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton(provider => SendGridMailOptions.FromConfiguration(
            provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton(provider => MicrosoftGraphMailOptions.FromConfiguration(
            provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<ISendGridClientFactory, SendGridClientFactory>();
        services.AddSingleton<SendGridEmailAdapter>();
        services.AddSingleton<IMicrosoftGraphAccessTokenProvider, AzureIdentityGraphAccessTokenProvider>();
        services.AddHttpClient<MicrosoftGraphEmailAdapter>(client =>
            client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"));
        services.AddSingleton<IEmailDeliveryPort>(provider =>
            provider.GetRequiredService<CommunicationsOptions>().EmailProvider switch
            {
                EmailProvider.MicrosoftGraph => provider.GetRequiredService<MicrosoftGraphEmailAdapter>(),
                _ => provider.GetRequiredService<SendGridEmailAdapter>()
            });
        services.AddHttpClient<TransmitSmsAdapter>();
        services.AddSingleton<ISmsDeliveryPort>(provider =>
            provider.GetRequiredService<TransmitSmsAdapter>());
        services.AddRvtCommunication();
        services.AddSingleton<IHostedService, CommunicationsStartupValidationService>();
        return services;
    }
}
