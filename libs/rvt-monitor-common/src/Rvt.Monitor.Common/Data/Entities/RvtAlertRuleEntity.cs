namespace Rvt.Monitor.Common.Data.Entities;

public sealed class RvtAlertRuleEntity
{
    public Guid Id { get; set; }
    public Guid? MonitorId { get; set; }
    public string? SerialId { get; set; }
    public string AlertField { get; set; } = string.Empty;
    public double LimitOn { get; set; }
    public double LimitOff { get; set; }
    public int AlertType { get; set; }
    public bool IsActive { get; set; }
    public int AveragingPeriod { get; set; }
    public bool Weekdays { get; set; }
    public bool Saturdays { get; set; }
    public bool Sundays { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Accessed { get; set; }
}
