
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

namespace Rvt.Monitor.Common.Rules;


public class AlertActivityTimeDto
{
    public bool Weekdays { get; init; }
    public bool Saturdays { get; init; }
    public bool Sundays { get; init; }
    public TimeSpan? StartTime { get; init; }
    public TimeSpan? EndTime { get; init; }

    /// <summary>
    /// The monitor-specific rule behaviour. Defaults to the running
    /// monitor's policy so existing callers are unaffected, but can be set
    /// explicitly, which is what makes this rule testable without global
    /// state.
    /// </summary>
    public MonitorRulePolicy Policy { get; init; } = RvtConfig.RulePolicy;

    public bool IsActive(DateTime dateTime) => IsActive(dateTime, Policy);

    public bool IsActive(DateTime dateTime, MonitorRulePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!policy.AppliesActivityTimeWindow)
        {
            return DoesRuleApplyForDay(dateTime);
        }

        return DoesRuleApplyForDay(dateTime) && DoesRuleApplyForTime(dateTime);
    }


    private bool DoesRuleApplyForDay(DateTime dateTime)
    {
        DayOfWeek dow = dateTime.DayOfWeek;
        if (dow == DayOfWeek.Sunday)
        {
            return Sundays;
        }
        if (dow == DayOfWeek.Saturday)
        {
            return Saturdays;
        }
        return Weekdays;
    }

    private bool DoesRuleApplyForTime(DateTime dateTime)
    {
        if (StartTime == null || EndTime == null)
        {
            return true;
        }

        // Convert given time of day to local time to allow for daylight saving
        TimeSpan localTimeOfDay = DateTimeUtil.UtcToLocal(dateTime.TimeOfDay);

        return TimeSpan.Compare((TimeSpan)StartTime, localTimeOfDay) <= 0 &&
            TimeSpan.Compare((TimeSpan)EndTime, localTimeOfDay) >= 0;
    }
}
