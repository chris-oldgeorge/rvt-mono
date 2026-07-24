using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rvt.Communication;
using Rvt.Communication.Abstractions;
using Rvt.Communication.MicrosoftGraphMail;
using Rvt.Communication.SendGridMail;
using Rvt.Communication.TransmitSms;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Storage;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Model.Config;

namespace Svantek.Api;

// Summary: Composition root that registers the Svantek monitor ports and services in the host container.
// Major updates:
// - 2026-07-12 DI composition: replaced manual SvantekService wiring with container registrations.
public static class SvantekMonitorServices
{
    public static IServiceCollection AddSvantekMonitor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMonitorBlobStorage();
        services.AddSingleton<IHttpClient>(_ => new HttpWebClient<SvantekService>(RvtConfig.BASE_URL));
        services.AddSingleton<IDBClient>(_ => new DBClient(RvtConfig.DB_CONNECTION_STRING));
        services.AddSingleton<IMqttClient, RvtMqttClient>();
        services.AddRvtCommunication();
        AddEmailProvider(services, configuration);
        services.AddTransmitSms(configuration);
        services.AddSingleton<IValidateOptions<SvantekImportOptions>, SvantekImportOptionsValidator>();
        services.AddOptions<SvantekImportOptions>()
            .BindConfiguration(SvantekImportOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<SvantekImportOptions>>().Value);
        services.AddSingleton<NoiseRequestWindowCalculator>();
        services.AddSingleton(provider => new SvantekApi(
            provider.GetRequiredService<IHttpClient>(),
            provider.GetRequiredService<IDBClient>(),
            provider.GetRequiredService<IMqttClient>(),
            provider.GetRequiredService<IMessageService>(),
            RvtConfig.API_KEY,
            provider.GetRequiredService<IBlobStorageService>(),
            RvtConfig.TESTLOCAL,
            provider.GetRequiredService<NoiseRequestWindowCalculator>()));
        services.AddSingleton(provider =>
        {
            RvtLogger.CreateLogger(provider.GetRequiredService<ILoggerFactory>(), "SvantekService");
            try
            {
                return new SvantekService(provider.GetRequiredService<SvantekApi>());
            }
            catch (Exception e)
            {
                var dbClient = provider.GetRequiredService<IDBClient>();
                dbClient.HandleException("failed to start monitor application", e);
                throw; // Need this to kill the instance.
            }
        });
        services.AddSingleton<ISvantekMonitorJobs>(provider =>
            provider.GetRequiredService<SvantekService>());
        return services;
    }

    private static void AddEmailProvider(IServiceCollection services, IConfiguration configuration)
    {
        var configuredProvider = configuration["RVT:EMAIL_PROVIDER"]
            ?? configuration["RVT__EMAIL_PROVIDER"]
            ?? "SendGrid";

        if (string.Equals(configuredProvider, "SendGrid", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSendGridMail(configuration);
        }
        else if (string.Equals(configuredProvider, "MicrosoftGraph", StringComparison.OrdinalIgnoreCase))
        {
            services.AddMicrosoftGraphMail(configuration);
        }
        else
        {
            throw new InvalidOperationException(
                "RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.");
        }
    }
}
