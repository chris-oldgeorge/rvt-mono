using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Rvt.Monitor.Common.Hosting;

internal static class MonitorOpenTelemetry
{
    public static void ConfigureLogging(ILoggingBuilder logging, IConfiguration configuration, string monitorName)
    {
        var options = MonitorOpenTelemetryOptions.Bind(configuration, monitorName);
        if (!options.Enabled)
        {
            return;
        }

        logging.SetMinimumLevel(options.LogLevel);
        logging.AddOpenTelemetry(openTelemetry =>
        {
            openTelemetry.IncludeFormattedMessage = true;
            openTelemetry.IncludeScopes = true;
            openTelemetry.ParseStateValues = true;
            openTelemetry.SetResourceBuilder(CreateResourceBuilder(options));
            openTelemetry.AddOtlpExporter(exporter => ConfigureExporter(exporter, options));
        });
    }

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration, string monitorName)
    {
        var options = MonitorOpenTelemetryOptions.Bind(configuration, monitorName);
        if (!options.Enabled)
        {
            return;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => ConfigureResource(resource, options))
            .WithTracing(tracing => tracing
                .AddSource(MonitorJobTelemetry.ActivitySourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(exporter => ConfigureExporter(exporter, options)))
            .WithMetrics(metrics => metrics
                .AddMeter(MonitorJobTelemetry.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(exporter => ConfigureExporter(exporter, options)));
    }

    private static ResourceBuilder CreateResourceBuilder(MonitorOpenTelemetryOptions options)
    {
        return ConfigureResource(ResourceBuilder.CreateDefault(), options);
    }

    private static ResourceBuilder ConfigureResource(ResourceBuilder resource, MonitorOpenTelemetryOptions options)
    {
        return resource.AddService(
            serviceName: options.ServiceName,
            serviceVersion: options.ServiceVersion);
    }

    private static void ConfigureExporter(OtlpExporterOptions exporter, MonitorOpenTelemetryOptions options)
    {
        exporter.Endpoint = options.OtlpEndpoint;
        exporter.Protocol = options.Protocol;
    }
}
