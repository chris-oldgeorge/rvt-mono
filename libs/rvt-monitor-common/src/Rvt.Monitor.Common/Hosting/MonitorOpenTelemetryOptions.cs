using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;

namespace Rvt.Monitor.Common.Hosting;

internal sealed record MonitorOpenTelemetryOptions(
    bool Enabled,
    Uri OtlpEndpoint,
    OtlpExportProtocol Protocol,
    string ServiceName,
    string ServiceVersion,
    LogLevel LogLevel)
{
    private static readonly Uri DefaultOtlpEndpoint = new("http://localhost:4317");

    public static MonitorOpenTelemetryOptions Bind(IConfiguration configuration, string monitorName)
    {
        var enabled = configuration.GetValue<bool>("OpenTelemetry:Enabled");
        var endpointText = FirstConfigured(
            configuration["OpenTelemetry:OtlpEndpoint"],
            configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        return new MonitorOpenTelemetryOptions(
            enabled,
            ResolveEndpoint(endpointText),
            ResolveProtocol(configuration["OTEL_EXPORTER_OTLP_PROTOCOL"]),
            FirstConfigured(configuration["OTEL_SERVICE_NAME"], configuration["OpenTelemetry:ServiceName"], monitorName),
            FirstConfigured(configuration["OpenTelemetry:ServiceVersion"], configuration["RVT:SERVICE_VERSION"], "v0.1.0"),
            ResolveLogLevel(configuration["OpenTelemetry:LogLevel"]));
    }

    private static Uri ResolveEndpoint(string? endpointText)
    {
        return Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint)
            ? endpoint
            : DefaultOtlpEndpoint;
    }

    private static OtlpExportProtocol ResolveProtocol(string? protocol)
    {
        return string.Equals(protocol, "http/protobuf", StringComparison.OrdinalIgnoreCase)
            ? OtlpExportProtocol.HttpProtobuf
            : OtlpExportProtocol.Grpc;
    }

    private static LogLevel ResolveLogLevel(string? logLevel)
    {
        return Enum.TryParse<LogLevel>(logLevel, ignoreCase: true, out var parsed)
            ? parsed
            : LogLevel.Information;
    }

    private static string FirstConfigured(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
