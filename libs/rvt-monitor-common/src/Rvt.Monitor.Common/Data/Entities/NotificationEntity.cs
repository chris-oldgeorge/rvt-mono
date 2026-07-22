namespace Rvt.Monitor.Common.Data.Entities;

public sealed class NotificationEntity
{
    public Guid Id { get; set; }
    public DateTime NotificationTime { get; set; }
    public double LimitOn { get; set; }
    public int AveragingPeriod { get; set; }
    public double Level { get; set; }
    public DateTime? ClosedTime { get; set; }
    public Guid? ClosedByUser { get; set; }
    public string? ClosedByNote { get; set; }
    public Guid MonitorId { get; set; }
    public string AlertField { get; set; } = string.Empty;
    public int AlertType { get; set; }
}
