using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Api.UseCases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rvt.Communication;
using Rvt.Communication.Abstractions;
using Rvt.Communication.MicrosoftGraphMail;
using Rvt.Communication.SendGridMail;
using Rvt.Communication.TransmitSms;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace AirQ.Api;

// Summary: Composition root that registers the AirQ monitor ports and services in the host container.
// Major updates:
// - 2026-07-12 DI composition: replaced manual AirQService wiring with container registrations.
public static class AirQMonitorServices
{
    public static IServiceCollection AddAirQMonitor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IHttpClient>(_ => new HttpWebClient(RvtConfig.BASE_URL));
        services.AddSingleton<IDBClient>(_ => new DBClient(RvtConfig.DB_CONNECTION_STRING));
        // Broker settings are supplied explicitly rather than read from
        // static configuration inside the client.
        services.AddSingleton(_ => MqttOptions.FromRvtConfig());
        services.AddSingleton<IMqttClient>(provider =>
            new RvtMqttClient(provider.GetRequiredService<MqttOptions>()));
        services.AddRvtCommunication();
        AddEmailProvider(services, configuration);
        services.AddTransmitSms(configuration);
        services.AddSingleton(provider => new AirQApi(
            provider.GetRequiredService<IHttpClient>(),
            provider.GetRequiredService<IDBClient>(),
            provider.GetRequiredService<IMqttClient>(),
            provider.GetRequiredService<IMessageService>(),
            RvtConfig.TESTLOCAL,
            provider.GetRequiredService<IConfiguration>()["AirQ:TestLocal:SerialId"]));
        services.AddSingleton(provider =>
        {
            RvtLogger.CreateLogger(provider.GetRequiredService<ILoggerFactory>(), "AirQService");
            try
            {
                return new AirQService(provider.GetRequiredService<AirQApi>());
            }
            catch (Exception e)
            {
                IDBClient dbClient = provider.GetRequiredService<IDBClient>();
                dbClient.HandleException("failed to start monitor application", e);
                throw; // Need this to kill the instance.
            }
        });
        services.AddSingleton<IAirQDateImporter>(provider => provider.GetRequiredService<AirQService>());
        return services;
    }

    private static void AddEmailProvider(IServiceCollection services, IConfiguration configuration)
    {
        string configuredProvider = configuration["RVT:EMAIL_PROVIDER"]
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
