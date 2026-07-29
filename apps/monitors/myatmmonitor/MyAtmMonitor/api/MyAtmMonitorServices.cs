using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Api.Ports;
using MyAtm.Api.UseCases;
using MyAtm.Model.Config;
using Rvt.Communication;
using Rvt.Communication.MicrosoftGraphMail;
using Rvt.Communication.SendGridMail;
using Rvt.Communication.TransmitSms;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace MyAtm.Api;

// Summary: Composition root that registers the MyAtm monitor ports and services in the host container.
// Major updates:
// - 2026-07-12 DI composition: replaced manual MyAtmService wiring with container registrations.
public static class MyAtmMonitorServices
{
    public static IServiceCollection AddMyAtmMonitor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<MyAtmMonitorOptions>, MyAtmMonitorOptionsValidator>();
        services.AddOptions<MyAtmMonitorOptions>()
            .BindConfiguration(MyAtmMonitorOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<MyAtmMonitorOptions>>().Value);
        services.AddSingleton<IValidateOptions<MyAtmVendorOptions>, MyAtmVendorOptionsValidator>();
        services.AddOptions<MyAtmVendorOptions>()
            .BindConfiguration(MyAtmVendorOptions.SectionName)
            .ValidateOnStart();
        services.PostConfigure<MyAtmVendorOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                options.BaseUrl = RvtConfig.BASE_URL;
            }

            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                options.ApiKey = RvtConfig.TOKEN;
            }
        });
        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<MyAtmVendorOptions>>().Value);
        services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);
        services.AddSingleton(provider => new MyAtmRequestPolicy(
            provider.GetRequiredService<MyAtmVendorOptions>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddHttpClient("MyAtmVendor");
        services.AddSingleton<IHttpClient>(provider => new HttpWebClient(
            provider.GetRequiredService<MyAtmVendorOptions>(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("MyAtmVendor"),
            provider.GetRequiredService<MyAtmRequestPolicy>()));
        services.AddSingleton<IDBClient>(_ => new DBClient(RvtConfig.DB_CONNECTION_STRING));
        services.AddSingleton<IMyAtmMonitorQueries>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMyAtmRuleQueries>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMyAtmMeasurementQueries>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMyAtmSiteScheduleQueries>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMyAtmMonitorCommands>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMyAtmMeasurementCommands>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMyAtmOperationalCommands>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMyAtmHealthQueries>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMyAtmDustImportCommands>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMyAtmAlertCommitCommands>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMyAtmAccessoryCommands>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMonitorDeliveryOutboxCommands>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton<IMonitorDeliveryOutboxQueries>(provider => provider.GetRequiredService<IDBClient>());
        services.AddSingleton(provider => provider.GetRequiredService<MyAtmMonitorOptions>()
            .ToDeliveryOptions(RvtConfig.INSERT_TOPIC, RvtConfig.ALERT_TOPIC));
        // Broker settings are supplied explicitly rather than read from
        // static configuration inside the client.
        services.AddSingleton(_ => MqttOptions.FromRvtConfig());
        services.AddSingleton<IMqttClient>(provider =>
            new RvtMqttClient(provider.GetRequiredService<MqttOptions>()));
        services.AddRvtCommunication();
        AddEmailProvider(services, configuration);
        services.AddTransmitSms(configuration);
        services.AddSingleton<IMonitorDeliveryFailureSink, MyAtmDeliveryFailureSink>();
        services.AddSingleton<MonitorDeliveryDispatcher>();
        services.AddSingleton(provider => new MyAtmHttpGateway(
            provider.GetRequiredService<IHttpClient>(),
            provider.GetRequiredService<MyAtmMonitorOptions>().DevicePageSize,
            provider.GetRequiredService<MyAtmMonitorOptions>().MeasurementPageSize,
            provider.GetRequiredService<MyAtmMonitorOptions>().AccessoryPageSize));
        services.AddSingleton<IMyAtmVendorGateway>(provider => provider.GetRequiredService<MyAtmHttpGateway>());
        services.AddSingleton(provider => new MyAtmMonitorReader(
            provider.GetRequiredService<IMyAtmMonitorQueries>(),
            provider.GetRequiredService<IMyAtmOperationalCommands>(),
            RvtConfig.TESTLOCAL));
        services.AddSingleton<MyAtmRuleEvaluator>();
        services.AddSingleton(provider => new MyAtmRuleProcessor(
            provider.GetRequiredService<IMyAtmRuleQueries>()));
        services.AddSingleton(provider => new StoreMonitorsHandler(
            provider.GetRequiredService<IMyAtmVendorGateway>(),
            provider.GetRequiredService<IMyAtmMonitorCommands>(),
            provider.GetRequiredService<IMyAtmOperationalCommands>(),
            RvtConfig.TESTLOCAL,
            provider.GetRequiredService<MyAtmMonitorOptions>().DevicePageSize,
            provider.GetRequiredService<MyAtmMonitorOptions>().MaxDevicePagesPerRun));
        services.AddSingleton<CheckForOfflineMonitorsHandler>();
        services.AddSingleton<ClearMonitorsOfflineFlagHandler>();
        services.AddSingleton<ClearOlderErrorMessagesHandler>();
        services.AddSingleton(provider => new StoreDustLevelsHandler(
            provider.GetRequiredService<IMyAtmVendorGateway>(),
            provider.GetRequiredService<MyAtmMonitorReader>(),
            provider.GetRequiredService<IMyAtmRuleQueries>(),
            provider.GetRequiredService<IMyAtmDustImportCommands>(),
            provider.GetRequiredService<IMyAtmOperationalCommands>(),
            provider.GetRequiredService<MyAtmRuleEvaluator>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<MyAtmMonitorOptions>().MaxPagesPerMonitorPerRun));
        services.AddSingleton(provider => new ProcessDustLevelsHandler(
            provider.GetRequiredService<IMyAtmMonitorQueries>(),
            provider.GetRequiredService<IMyAtmRuleQueries>(),
            provider.GetRequiredService<IMyAtmAlertCommitCommands>(),
            provider.GetRequiredService<IMyAtmOperationalCommands>(),
            provider.GetRequiredService<MyAtmRuleProcessor>(),
            provider.GetRequiredService<TimeProvider>(),
            RvtConfig.TESTLOCAL));
        services.AddSingleton(provider => new StoreAccessoryInfoHandler(
            provider.GetRequiredService<IMyAtmVendorGateway>(),
            provider.GetRequiredService<MyAtmMonitorReader>(),
            provider.GetRequiredService<IMyAtmAccessoryCommands>(),
            provider.GetRequiredService<IMyAtmMeasurementQueries>(),
            provider.GetRequiredService<IMyAtmOperationalCommands>(),
            provider.GetRequiredService<MyAtmMonitorOptions>().MaxPagesPerMonitorPerRun));
        services.AddSingleton(provider =>
        {
            RvtLogger.CreateLogger(provider.GetRequiredService<ILoggerFactory>(), "MyAtmService");
            try
            {
                return new MyAtmService(
                    provider.GetRequiredService<StoreMonitorsHandler>(),
                    provider.GetRequiredService<CheckForOfflineMonitorsHandler>(),
                    provider.GetRequiredService<StoreDustLevelsHandler>(),
                    provider.GetRequiredService<ProcessDustLevelsHandler>(),
                    provider.GetRequiredService<ClearOlderErrorMessagesHandler>(),
                    provider.GetRequiredService<StoreAccessoryInfoHandler>(),
                    provider.GetRequiredService<MonitorDeliveryDispatcher>(),
                    provider.GetRequiredService<MyAtmMonitorOptions>());
            }
            catch (Exception e)
            {
                IDBClient dbClient = provider.GetRequiredService<IDBClient>();
                dbClient.HandleException("failed to start monitor application", e);
                throw; // Need this to kill the instance.
            }
        });
        services.AddSingleton<IMyAtmMonitorJobs>(provider => provider.GetRequiredService<MyAtmService>());
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
