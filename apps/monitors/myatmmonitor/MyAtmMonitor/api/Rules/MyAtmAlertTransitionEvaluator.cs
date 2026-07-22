using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace MyAtm.Api.Rules;

public sealed class MyAtmAlertTransitionEvaluator
{
    public MyAtmAlertTransition Evaluate(
        RvtAlertRuleDto rule,
        bool isActive,
        DustDto sample,
        bool alertForFieldIsActive)
    {
        if (rule.IsDeleted)
        {
            return new MyAtmAlertTransition(false, false, null);
        }

        if (!rule.RuleActiveTime.IsActive(sample.SampleTime))
        {
            return new MyAtmAlertTransition(isActive, false, null);
        }

        var level = ReadLevel(rule.Field, sample);
        if (!level.HasValue)
        {
            return new MyAtmAlertTransition(isActive, false, null);
        }

        if (level.Value <= rule.LimitOff)
        {
            return new MyAtmAlertTransition(false, false, level);
        }

        if (rule.AlertType == AlertType.Caution && alertForFieldIsActive)
        {
            return new MyAtmAlertTransition(isActive, false, level);
        }

        if (isActive || level.Value < rule.LimitOn)
        {
            return new MyAtmAlertTransition(isActive, false, level);
        }

        return new MyAtmAlertTransition(true, true, level);
    }

    public static string NormalizeField(string field) => field.Trim().ToLowerInvariant() switch
    {
        "pm2_5" or "pm2.5" => "pm2.5",
        "pm_total" or "pmtotal" => "pmtotal",
        var normalized => normalized
    };

    private static double? ReadLevel(string field, DustDto sample) => NormalizeField(field) switch
    {
        "pm1" => sample.Pm1,
        "pm2.5" => sample.Pm2_5,
        "pm10" => sample.Pm10,
        "pmtotal" => sample.PmTotal,
        _ => null
    };
}

public sealed record MyAtmAlertTransition(bool IsActive, bool Activated, double? Level);
