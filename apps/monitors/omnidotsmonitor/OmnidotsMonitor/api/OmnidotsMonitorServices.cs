using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Omnidots.Api.Db;
using Omnidots.Api.Db.EntityFramework;
using Omnidots.Api.Http;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Infrastructure.Communications;
using Rvt.Monitor.Common.Mqtt;

namespace Omnidots.Api;

// Summary: Composition root that registers the Omnidots monitor ports and services in the host container.
// Major updates:
// - 2026-07-12 DI composition: replaced manual OmnidotsService wiring with container registrations.
// - 2026-07-15 Durable alerts: composed Common alert persistence, delivery, workers, and focused API handlers.
public static class OmnidotsMonitorServices
{
    public static IServiceCollection AddOmnidotsMonitor(this IServiceCollection services)
    {
        services.AddSingleton<IHttpClient>(_ => new HttpWebClient(RvtConfig.BASE_URL));
        services.AddSingleton<IDBClient>(_ => new DBClient(RvtConfig.DB_CONNECTION_STRING));
        services.AddSingleton(provider =>
            (IOmnidotsImportCursorQueries)provider.GetRequiredService<IDBClient>());
        services.AddSingleton(provider =>
            (IOmnidotsMeasurementImportCommands)provider.GetRequiredService<IDBClient>());
        services.AddSingleton(provider =>
            (IOmnidotsTraceQueries)provider.GetRequiredService<IDBClient>());
        services.AddSingleton(provider =>
            (IOmnidotsMonitorQueries)provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IValidateOptions<OmnidotsMonitoringOptions>, OmnidotsMonitoringOptionsValidator>();
        services.AddOptions<OmnidotsMonitoringOptions>()
            .BindConfiguration(OmnidotsMonitoringOptions.SectionName)
            .Configure<IConfiguration>((options, configuration) =>
            {
                var alertRecipient = configuration["RVT:OMNIDOTS_MONITORING_ALERT_TO"] ??
                    configuration["RVT__OMNIDOTS_MONITORING_ALERT_TO"];
                if (!string.IsNullOrWhiteSpace(alertRecipient))
                {
                    options.Recipient = alertRecipient;
                }
            })
            .ValidateOnStart();
        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<OmnidotsMonitoringOptions>>().Value);
        services.AddSingleton<IValidateOptions<OmnidotsTraceCollectionOptions>, OmnidotsTraceCollectionOptionsValidator>();
        services.AddOptions<OmnidotsTraceCollectionOptions>()
            .BindConfiguration(OmnidotsTraceCollectionOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<OmnidotsTraceCollectionOptions>>().Value);
        services.AddOptions<OmnidotsApiSecurityOptions>()
            .BindConfiguration(OmnidotsApiSecurityOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IConfigureOptions<OmnidotsApiSecurityOptions>, OmnidotsApiSecurityOptionsSetup>();
        services.AddSingleton<IValidateOptions<OmnidotsApiSecurityOptions>, OmnidotsApiSecurityOptionsValidator>();
        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<OmnidotsApiSecurityOptions>>().Value);
        services.AddRateLimiter();
        services.AddSingleton<IConfigureOptions<RateLimiterOptions>, OmnidotsRateLimiterOptionsSetup>();
        services.AddSingleton<IOmnidotsMonitoringNotifier, EmailOmnidotsMonitoringNotifier>();
        services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);
        services.AddSingleton<IMqttClient, RvtMqttClient>();
        services.AddMonitorCommunications();
        services.AddSingleton<IMonitorDbContextFactory<OmnidotsMonitorContext>>(
            _ => new OmnidotsMonitorContextFactory(
                RvtConfig.DB_CONNECTION_STRING,
                OmnidotsMonitorDbOptions.Current));
        services.AddSingleton<IMonitorEventPublisher>(provider => new MonitorEventPublisher(
            provider.GetRequiredService<IMqttClient>(),
            RvtConfig.INSERT_TOPIC,
            RvtConfig.ALERT_TOPIC));
        services.AddDurableAlerts<OmnidotsMonitorContext>();
        services.PostConfigure<DurableAlertOptions>(options =>
        {
            if (!string.IsNullOrWhiteSpace(RvtConfig.PORTAL_BASE_URL))
            {
                options.PortalBaseUrl = RvtConfig.PORTAL_BASE_URL;
            }
        });
        services.AddSingleton(provider => new OmnidotsHttpGateway(
            provider.GetRequiredService<IHttpClient>(),
            RvtConfig.USER_ID,
            RvtConfig.USER_AUTH));
        services.AddSingleton<OmnidotsAlarmTranslator>();
        services.AddSingleton<OmnidotsWebhookSignatureValidator>();
        services.AddSingleton<ProcessWebhookHandler>();
        services.AddSingleton<ConfigureMeasuringPointHandler>();
        services.AddSingleton(provider => new OmnidotsApi(
            provider.GetRequiredService<IHttpClient>(),
            provider.GetRequiredService<IDBClient>(),
            provider.GetRequiredService<IOmnidotsImportCursorQueries>(),
            provider.GetRequiredService<IOmnidotsMeasurementImportCommands>(),
            provider.GetRequiredService<IOmnidotsTraceQueries>(),
            provider.GetRequiredService<IMqttClient>(),
            provider.GetRequiredService<IMessageService>(),
            RvtConfig.TESTLOCAL,
            provider.GetRequiredService<OmnidotsMonitoringOptions>(),
            provider.GetRequiredService<IOmnidotsMonitoringNotifier>(),
            provider.GetRequiredService<OmnidotsTraceCollectionOptions>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton(provider =>
        {
            RvtLogger.CreateLogger(provider.GetRequiredService<ILoggerFactory>(), "OmnidotsService");
            try
            {
                return new OmnidotsService(provider.GetRequiredService<OmnidotsApi>());
            }
            catch (Exception e)
            {
                var dbClient = provider.GetRequiredService<IDBClient>();
                dbClient.HandleException("failed to start monitor application", e);
                throw; // Need this to kill the instance.
            }
        });
        return services;
    }
}
