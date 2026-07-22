using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Rules
{
    public class RvtAlertRuleDto
    {
        public static readonly int RULE_ALERT_DELAY_MINUTES = 5;
        public Guid RuleId { get; }
        public string? SerialId { get; }
        public string Field { get; }
        public int AveragingPeriod { get; }
        public double LimitOn { get; }
        public double LimitOff { get; }
        public AlertActivityTimeDto RuleActiveTime { get; }
        public AlertType AlertType { get; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; }
        public DateTime Created { get; }
        public DateTime? Accessed { get; set; }

        public RvtAlertRuleDto(Guid ruleId, string? serialId, string field, double limitOn, double limitOff,
                            int averagingPeriod, AlertActivityTimeDto ruleActivityTime,
                            AlertType alertType, bool isActive, bool isDeleted,
                            DateTime created, DateTime? accessed)
        {
            RuleId = ruleId;
            SerialId = serialId;
            Field = field;
            LimitOn = limitOn;
            LimitOff = limitOff;
            AveragingPeriod = averagingPeriod;
            RuleActiveTime = ruleActivityTime;
            AlertType = alertType;
            IsActive = isActive;
            IsDeleted = isDeleted;
            Created = created;
            Accessed = accessed;
        }

        public override string ToString()
        {
            return string.Format(@"Alert Rule Field={0} AveragingPeriod={1} LimitOn={2} LimitOff={3} AlertType={4} IsActive={5} IsDeleted={6}",
                                Field, AveragingPeriod, LimitOn, LimitOff, AlertType, IsActive, IsDeleted);
        }
    }
}
