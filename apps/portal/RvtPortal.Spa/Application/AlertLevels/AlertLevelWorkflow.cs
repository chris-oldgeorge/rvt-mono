// File summary: Provides shared alert-level projection, option, and normalization helpers for alert-level workflows.
// Major updates:
// - 2026-07-09 pending Moved shared alert-level helper logic out of the API controller.

using System.Globalization;
using RVT.Entities;
using RvtPortal.Spa.Api;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.AlertLevels;

internal static class AlertLevelWorkflow
{
    // Function summary: Builds the existing alert-level API item from a domain alert rule.
    public static AlertLevelItem BuildAlertLevelItem(Alertlevel level, MonitorTypeEnum? monitorType = null)
    {
        var resolvedMonitorType = monitorType ?? level.Monitor?.TypeOfMonitor;
        return new AlertLevelItem
        {
            Id = level.Id,
            MonitorId = level.MonitorId,
            SerialId = level.SerialId,
            AlertField = level.AlertField,
            LimitOn = level.LimitOn,
            LimitOff = level.LimitOff,
            AlertType = level.AlertType.ToString(),
            IsActive = level.IsActive,
            AveragingPeriod = level.AveragingPeriod,
            AveragingPeriodLabel = AveragingPeriodLabel(level, resolvedMonitorType),
            Weekdays = level.Weekdays,
            Saturdays = level.Saturdays,
            Sundays = level.Sundays,
            StartTime = FormatTime(level.StartTime),
            EndTime = FormatTime(level.EndTime),
            IsDeleted = level.IsDeleted
        };
    }

    // Function summary: Builds the option lists shown by alert-level forms for a monitor.
    public static AlertLevelOptionsResponse BuildOptions(MonitorEntity monitor)
    {
        return new AlertLevelOptionsResponse
        {
            MonitorId = monitor.Id,
            SerialId = monitor.SerialId,
            TypeOfMonitor = monitor.TypeOfMonitor.ToString(),
            AlertFields = BuildFieldOptions(monitor.TypeOfMonitor),
            AlertTypes =
            [
                new OptionItem { Value = AlertTypeEnum.Alert.ToString(), Label = "Alert" },
                new OptionItem { Value = AlertTypeEnum.Caution.ToString(), Label = "Caution" }
            ],
            AveragingPeriods = BuildAveragingPeriodOptions(monitor.TypeOfMonitor)
        };
    }

    // Function summary: Returns supported alert fields for a monitor type.
    public static List<OptionItem> BuildFieldOptions(MonitorTypeEnum monitorType)
    {
        return monitorType switch
        {
            MonitorTypeEnum.Dust =>
            [
                new OptionItem { Value = "pm1", Label = "pm1" },
                new OptionItem { Value = "pm2.5", Label = "pm2.5" },
                new OptionItem { Value = "pm10", Label = "pm10" },
                new OptionItem { Value = "pmTotal", Label = "pmTotal" }
            ],
            MonitorTypeEnum.Noise =>
            [
                new OptionItem { Value = "LAeq", Label = "LAeq" },
                new OptionItem { Value = "LAmax", Label = "LAmax" },
                new OptionItem { Value = "LA90", Label = "LA90" },
                new OptionItem { Value = "LA10", Label = "LA10" },
                new OptionItem { Value = "LCeq", Label = "LCeq" },
                new OptionItem { Value = "LCmax", Label = "LCmax" },
                new OptionItem { Value = "LC90", Label = "LC90" },
                new OptionItem { Value = "LC10", Label = "LC10" }
            ],
            MonitorTypeEnum.Vibration => [new OptionItem { Value = "Peak", Label = "Peak" }],
            _ => []
        };
    }

    // Function summary: Returns supported averaging-period choices for a monitor type.
    public static List<OptionItem> BuildAveragingPeriodOptions(MonitorTypeEnum monitorType)
    {
        return monitorType switch
        {
            MonitorTypeEnum.Dust => Enum.GetValues<AveragingPeriodsDustEnum>()
                .Select(value => new OptionItem { Value = ((int)value).ToString(CultureInfo.InvariantCulture), Label = EnumLabel(value.ToString()) })
                .ToList(),
            MonitorTypeEnum.Noise => Enum.GetValues<AveragingPeriodsNoiseEnum>()
                .Select(value => new OptionItem { Value = ((int)value).ToString(CultureInfo.InvariantCulture), Label = EnumLabel(value.ToString()) })
                .ToList(),
            MonitorTypeEnum.Vibration => [],
            _ => []
        };
    }

    // Function summary: Normalizes alert-field input for monitor-specific special cases.
    public static string NormalizeAlertField(MonitorEntity monitor, AlertLevelMutationRequest request)
    {
        return monitor.TypeOfMonitor == MonitorTypeEnum.Noise && request.AveragingPeriod == (int)AveragingPeriodsNoiseEnum._Site_hours
            ? "LAeq"
            : (request.AlertField ?? "").Trim();
    }

    // Function summary: Clears day/time fields for noise site-hours alert levels.
    public static void NormalizeNoiseSiteHours(MonitorEntity monitor, Alertlevel level)
    {
        if (monitor.TypeOfMonitor == MonitorTypeEnum.Noise && level.AveragingPeriod == (int)AveragingPeriodsNoiseEnum._Site_hours)
        {
            level.AlertField = "LAeq";
            level.Weekdays = false;
            level.Saturdays = false;
            level.Sundays = false;
            level.StartTime = null;
            level.EndTime = null;
        }
    }

    // Function summary: Builds one persisted vibration alert-level row for the alert/caution pair.
    public static Alertlevel BuildVibrationLevel(MonitorEntity monitor, AlertTypeEnum alertType, double limitOn, double limitOff)
    {
        return new Alertlevel
        {
            MonitorId = monitor.Id,
            LimitOn = limitOn,
            LimitOff = limitOff,
            AlertType = alertType,
            AlertField = "Peak",
            IsActive = false,
            IsDeleted = false,
            SerialId = monitor.SerialId,
            AveragingPeriod = (int)AveragingPeriodsVibrationEnum._1_min,
            Weekdays = true,
            Saturdays = false,
            Sundays = false
        };
    }

    // Function summary: Parses supported alert-type values from names or enum numbers.
    public static bool TryParseAlertType(string? value, out AlertTypeEnum alertType)
    {
        if (Enum.TryParse(value, true, out alertType))
        {
            return alertType is AlertTypeEnum.Alert or AlertTypeEnum.Caution;
        }
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) && Enum.IsDefined(typeof(AlertTypeEnum), numeric))
        {
            alertType = (AlertTypeEnum)numeric;
            return alertType is AlertTypeEnum.Alert or AlertTypeEnum.Caution;
        }

        return false;
    }

    // Function summary: Parses optional HH:mm-style time input used by noise alert levels.
    public static bool TryParseOptionalTime(string? value, out TimeSpan? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }

    // Function summary: Formats optional persisted time spans for API responses.
    private static string? FormatTime(TimeSpan? value)
    {
        return value?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    // Function summary: Resolves an averaging-period display label for the monitor type.
    private static string AveragingPeriodLabel(Alertlevel level, MonitorTypeEnum? monitorType)
    {
        if (monitorType == MonitorTypeEnum.Vibration)
        {
            return "";
        }

        if (Enum.IsDefined(typeof(AveragingPeriodsDustEnum), level.AveragingPeriod))
        {
            return EnumLabel(((AveragingPeriodsDustEnum)level.AveragingPeriod).ToString());
        }
        if (Enum.IsDefined(typeof(AveragingPeriodsNoiseEnum), level.AveragingPeriod))
        {
            return EnumLabel(((AveragingPeriodsNoiseEnum)level.AveragingPeriod).ToString());
        }
        if (Enum.IsDefined(typeof(AveragingPeriodsVibrationEnum), level.AveragingPeriod))
        {
            return EnumLabel(((AveragingPeriodsVibrationEnum)level.AveragingPeriod).ToString());
        }

        return level.Monitor?.TypeOfMonitor switch
        {
            MonitorTypeEnum.Dust => Enum.IsDefined(typeof(AveragingPeriodsDustEnum), level.AveragingPeriod)
                ? EnumLabel(((AveragingPeriodsDustEnum)level.AveragingPeriod).ToString())
                : level.AveragingPeriod.ToString(CultureInfo.InvariantCulture),
            MonitorTypeEnum.Noise => Enum.IsDefined(typeof(AveragingPeriodsNoiseEnum), level.AveragingPeriod)
                ? EnumLabel(((AveragingPeriodsNoiseEnum)level.AveragingPeriod).ToString())
                : level.AveragingPeriod.ToString(CultureInfo.InvariantCulture),
            _ => level.AveragingPeriod.ToString(CultureInfo.InvariantCulture)
        };
    }

    // Function summary: Converts enum token names into existing UI labels.
    private static string EnumLabel(string value)
    {
        return value.TrimStart('_').Replace('_', ' ');
    }
}
