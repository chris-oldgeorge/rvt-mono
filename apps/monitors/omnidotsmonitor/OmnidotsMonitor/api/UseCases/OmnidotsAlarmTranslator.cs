using System.Globalization;
using System.Security.Cryptography;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using static Omnidots.Model.Json.AlarmDataV2;

namespace Omnidots.Api.UseCases;

public sealed class OmnidotsAlarmTranslator
{
    private const string Source = "omnidots.webhook";
    private const string InvalidPayloadMessage = "Invalid alarm payload.";

    public AlertSignal Translate(
        AlarmDataV2 alarm,
        ReadOnlySpan<byte> authenticatedBody,
        TimeSpan suppressionWindow)
    {
        if (alarm.MeasuringPointId <= 0 ||
            alarm.Data1?.Alarms is not { } alarms ||
            alarm.Data1.Axes is not { } axes ||
            axes.X?.Vtop is not { } xVtop ||
            axes.Y?.Vtop is not { } yVtop ||
            axes.Z?.Vtop is not { } zVtop)
        {
            throw InvalidPayload();
        }

        ValidateFinite(xVtop.Value);
        ValidateFinite(yVtop.Value);
        ValidateFinite(zVtop.Value);
        ValidateThreshold(alarms.AlarmLevel1);
        ValidateThreshold(alarms.AlarmLevel2);
        ValidateThreshold(alarms.AlarmLevel3);

        var eventTime = ConvertEventTime(alarm.CreatedAt);
        var (axis, level) = SelectMaximumAxis(xVtop.Value, yVtop.Value, zVtop.Value);
        var (alertType, limit) = SelectSeverity(level, alarms);
        var field = $"vtop {axis}";
        var channels = alertType == AlertType.Ignore
            ? AlertDeliveryChannels.None
            : AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms;
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"Vibration {alertType} {field} level={level} limit={limit}");

        return new AlertSignal(
            Source,
            Convert.ToHexStringLower(SHA256.HashData(authenticatedBody)),
            eventTime,
            alarm.MeasuringPointId.ToString(CultureInfo.InvariantCulture),
            alertType,
            field,
            level,
            limit,
            AveragingPeriod: 0,
            message,
            channels,
            suppressionWindow);
    }

    private static DateTime ConvertEventTime(double unixSeconds)
    {
        ValidateFinite(unixSeconds);

        try
        {
            var unixMilliseconds = checked((long)(unixSeconds * 1000));
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            throw InvalidPayload();
        }
        catch (OverflowException)
        {
            throw InvalidPayload();
        }
    }

    private static (string Axis, double Level) SelectMaximumAxis(double x, double y, double z)
    {
        if (x > y && x > z)
        {
            return ("x", x);
        }

        if (y > z)
        {
            return ("y", y);
        }

        return ("z", z);
    }

    private static (AlertType AlertType, double Limit) SelectSeverity(double level, Alarms alarms)
    {
        if (alarms.AlarmLevel3 > 0 && level >= alarms.AlarmLevel3 / 10)
        {
            return (AlertType.Alert, alarms.AlarmLevel3 / 10);
        }

        if (alarms.AlarmLevel2 > 0 && level >= alarms.AlarmLevel2 / 10)
        {
            return (AlertType.Caution, alarms.AlarmLevel2 / 10);
        }

        if (alarms.AlarmLevel1 > 0 && level >= alarms.AlarmLevel1 / 10)
        {
            return (AlertType.Ignore, alarms.AlarmLevel1 / 10);
        }

        return (AlertType.Ignore, 0);
    }

    private static void ValidateFinite(double value)
    {
        if (!double.IsFinite(value))
        {
            throw InvalidPayload();
        }
    }

    private static void ValidateThreshold(double value)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw InvalidPayload();
        }
    }

    private static AdapterException InvalidPayload() => AdapterException.Of(InvalidPayloadMessage);
}
