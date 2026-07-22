using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Api.UseCases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Infrastructure.Communications;
using Rvt.Monitor.Common.Mqtt;

namespace AirQ.Api;

// Summary: Composition root that registers the AirQ monitor ports and services in the host container.
// Major updates:
// - 2026-07-12 DI composition: replaced manual AirQService wiring with container registrations.
public static class AirQMonitorServices
{
    public static IServiceCollection AddAirQMonitor(this IServiceCollection services)
    {
        services.AddSingleton<IHttpClient>(_ => new HttpWebClient<AirQService>(RvtConfig.BASE_URL));
        services.AddSingleton<IDBClient>(_ => new DBClient(RvtConfig.DB_CONNECTION_STRING));
        services.AddSingleton<IMqttClient, RvtMqttClient>();
        services.AddMonitorCommunications();
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
                var dbClient = provider.GetRequiredService<IDBClient>();
                dbClient.HandleException("failed to start monitor application", e);
                throw; // Need this to kill the instance.
            }
        });
        services.AddSingleton<IAirQDateImporter>(provider => provider.GetRequiredService<AirQService>());
        return services;
    }
}
