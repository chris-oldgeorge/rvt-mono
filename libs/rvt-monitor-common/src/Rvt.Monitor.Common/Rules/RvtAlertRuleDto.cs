using System.Diagnostics.CodeAnalysis;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Rules;

[method: SuppressMessage(
    "Maintainability",
    "S107:Methods should not have too many parameters",
    Justification = "The primary constructor is the explicit materialization contract for the immutable alert-rule DTO.")]
public class RvtAlertRuleDto(Guid ruleId, string? serialId, string field, double limitOn, double limitOff,
                    int averagingPeriod, AlertActivityTimeDto ruleActivityTime,
                    AlertType alertType, bool isActive, bool isDeleted,
                    DateTime created, DateTime? accessed)
{
    public static readonly int RULE_ALERT_DELAY_MINUTES = 5;
    public Guid RuleId { get; } = ruleId;
    public string? SerialId { get; } = serialId;
    public string Field { get; } = field;
    public int AveragingPeriod { get; } = averagingPeriod;
    public double LimitOn { get; } = limitOn;
    public double LimitOff { get; } = limitOff;
    public AlertActivityTimeDto RuleActiveTime { get; } = ruleActivityTime;
    public AlertType AlertType { get; } = alertType;
    public bool IsActive { get; set; } = isActive;
    public bool IsDeleted { get; } = isDeleted;
    public DateTime Created { get; } = created;
    public DateTime? Accessed { get; set; } = accessed;

    public override string ToString()
    {
        return string.Format(@"Alert Rule Field={0} AveragingPeriod={1} LimitOn={2} LimitOff={3} AlertType={4} IsActive={5} IsDeleted={6}",
                            Field, AveragingPeriod, LimitOn, LimitOff, AlertType, IsActive, IsDeleted);
    }
}
