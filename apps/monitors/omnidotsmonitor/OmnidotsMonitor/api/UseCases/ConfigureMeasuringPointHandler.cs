using System.Text.Json;
using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Api.Ports;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Diagnostics;

namespace Omnidots.Api.UseCases;

// Summary: Builds and submits an Omnidots measuring-point configuration from a secret-guarded request.
// Major updates:
// - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiConfiguration).
public class ConfigureMeasuringPointHandler
{
    private const double _minimumTuningValue = 0;
    private const double _maximumTuningValue = 1_000_000;
    private const string _invalidRequestMessage = "Invalid measuring point configuration request.";

    private readonly IOmnidotsVendorGateway _gateway;
    private readonly IOmnidotsMonitorQueries _monitorQueries;
    private readonly OmnidotsApiSecurityOptions _securityOptions;

    public ConfigureMeasuringPointHandler(
        IOmnidotsVendorGateway gateway,
        IOmnidotsMonitorQueries monitorQueries,
        OmnidotsApiSecurityOptions securityOptions)
    {
        _gateway = gateway;
        _monitorQueries = monitorQueries;
        _securityOptions = securityOptions;
    }

    public async Task<ConfigureMeasuringPointResult> RunAsync(
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        OmnidotsApiSecurityGuard.EnsureConfigurationReady(_securityOptions);

        using JsonDocument document = ParseDocument(body);
        string? suppliedSecret = ExtractSecret(document.RootElement);
        if (suppliedSecret is null ||
            !OmnidotsFixedTimeSecretComparer.Matches(suppliedSecret, _securityOptions.ConfigSecret))
        {
            throw new OmnidotsConfigurationAuthenticationException();
        }

        ConfigureMeasuringPointRequest request = DeserializeRequest(body);
        string serialId = ValidateRequest(request);
        ConfigRequest vendorRequest = CreateConfigRequest(serialId, request);
        string vendorBody = JsonSerializer.Serialize(vendorRequest);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            TokenResponse authentication = await _gateway.AuthenticateAsync(cancellationToken);
            if (!authentication.Ok || string.IsNullOrWhiteSpace(authentication.Token))
            {
                throw new OmnidotsVendorConfigurationException();
            }

            OmnidotsResponse response = await _gateway.ConfigureMeasuringPointAsync(
                authentication.Token,
                serialId,
                vendorBody,
                cancellationToken);
            if (!response.Ok)
            {
                throw new OmnidotsVendorConfigurationException();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            throw new OmnidotsVendorConfigurationException();
        }

        if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
        {
            RvtLogger.Logger.LogInformation("Configured measuring point serialId={SerialId}", serialId);
        }

        return new ConfigureMeasuringPointResult(Configured: true);
    }

    private static string? ExtractSecret(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("secret", out JsonElement secretElement) ||
            secretElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return secretElement.GetString();
    }

    private static JsonDocument ParseDocument(ReadOnlyMemory<byte> body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw InvalidRequest();
        }
    }

    private static ConfigureMeasuringPointRequest DeserializeRequest(ReadOnlyMemory<byte> body)
    {
        try
        {
            return JsonSerializer.Deserialize<ConfigureMeasuringPointRequest>(body.Span)
                ?? throw InvalidRequest();
        }
        catch (JsonException)
        {
            throw InvalidRequest();
        }
    }

    private static string ValidateRequest(ConfigureMeasuringPointRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SerialId))
        {
            throw InvalidRequest();
        }

        ValidateTuningValue(request.TraceSaveLevel);
        ValidateTuningValue(request.TracePreTrigger);
        ValidateTuningValue(request.TracePostTrigger);
        ValidateTuningValue(request.FlatLevel);
        ValidateTuningValue(request.LevelAlert);
        ValidateTuningValue(request.LevelCaution);

        double flatLevel = request.FlatLevel ?? 10.0;
        double levelAlert = request.LevelAlert ?? 10.0;
        double levelCaution = request.LevelCaution ?? 7.0;
        if (levelAlert * flatLevel > int.MaxValue || levelCaution * flatLevel > int.MaxValue)
        {
            throw InvalidRequest();
        }

        return request.SerialId;
    }

    private static void ValidateTuningValue(double? value)
    {
        if (value is not null &&
            (!double.IsFinite(value.Value) ||
             value.Value < _minimumTuningValue ||
             value.Value > _maximumTuningValue))
        {
            throw InvalidRequest();
        }
    }

    private ConfigRequest CreateConfigRequest(string serialId, ConfigureMeasuringPointRequest request)
    {
        if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
        {
            RvtLogger.Logger.LogInformation("CreateConfigRequest for serialId={SerialId}", serialId);
        }

        VibrationMonitorDto monitor = _monitorQueries.ReadMonitor(serialId);
        SiteTimes siteTimes = _monitorQueries.ReadSiteTimes(monitor.Id);

        double traceSaveLevel = request.TraceSaveLevel ?? 10.0;
        double tracePreTrigger = request.TracePreTrigger ?? 3.0;
        double tracePostTrigger = request.TracePostTrigger ?? 3.0;
        double flatLevel = request.FlatLevel ?? 10.0;
        double levelAlert = request.LevelAlert ?? 10.0;
        double levelCaution = request.LevelCaution ?? 7.0;

        return new ConfigRequest
        {
            Name = monitor.CustomerDisplayName,
            SensorName = monitor.Sensor!.Name,
            DisableLed = monitor.MonitorStatus.DisableLed,
            LogFlushInterval = monitor.MonitorStatus.LogFlushInterval,
            Timezone = monitor.TimeZone,
            GuideLine = "BS7385_250Hz",
            BuildingLevel = "unspecified",
            FlatLevel = flatLevel,
            MeasuringType = "Limited",
            VibrationType = "Continuous",
            DataSaveLevel = monitor.MonitorStatus.DataSaveLevel,
            MeasurementDuration = monitor.MonitorStatus.MeasurementDuration,
            TraceSaveLevel = traceSaveLevel,
            TracePreTrigger = tracePreTrigger,
            TracePostTrigger = tracePostTrigger,
            EmailDelay = 60 * 5,
            EnableTime0 = siteTimes.GetSundayStart(),
            DisableTime0 = siteTimes.GetSundayEnd(),
            EnableTime1 = siteTimes.GetWeekdayStart(),
            DisableTime1 = siteTimes.GetWeekdayEnd(),
            EnableTime2 = siteTimes.GetWeekdayStart(),
            DisableTime2 = siteTimes.GetWeekdayEnd(),
            EnableTime3 = siteTimes.GetWeekdayStart(),
            DisableTime3 = siteTimes.GetWeekdayEnd(),
            EnableTime4 = siteTimes.GetWeekdayStart(),
            DisableTime4 = siteTimes.GetWeekdayEnd(),
            EnableTime5 = siteTimes.GetWeekdayStart(),
            DisableTime5 = siteTimes.GetWeekdayEnd(),
            EnableTime6 = siteTimes.GetSaturdayStart(),
            DisableTime6 = siteTimes.GetSaturdayEnd(),
            AlarmLevel1 = 0,
            AlarmLevel2 = (int)(levelCaution * flatLevel),
            AlarmLevel3 = (int)(levelAlert * flatLevel),
            WebhookRecipient = new WebhookRecipient
            {
                AlarmLevel1 = false,
                AlarmLevel2 = true,
                AlarmLevel3 = true,
                Url = _securityOptions.WebhookUrl,
                Secret = _securityOptions.WebhookSecret,
                MeasuringPointAdministrator = true
            }
        };
    }

    private static JsonException InvalidRequest() => new(_invalidRequestMessage);
}
