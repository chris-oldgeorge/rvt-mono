namespace Rvt.Monitor.Common.Data.Entities;

public sealed class AlertOccurrenceEntity
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public byte[] SourceKeyHash { get; set; } = [];
    public Guid? NotificationId { get; set; }
    public Guid MonitorId { get; set; }
    public string SerialId { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public int AlertType { get; set; }
    public string AlertField { get; set; } = string.Empty;
    public double Level { get; set; }
    public double LimitOn { get; set; }
    public int AveragingPeriod { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
