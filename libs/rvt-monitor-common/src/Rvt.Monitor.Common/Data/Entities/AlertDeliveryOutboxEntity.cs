namespace Rvt.Monitor.Common.Data.Entities;

public sealed class AlertDeliveryOutboxEntity
{
    public Guid Id { get; set; }
    public Guid OccurrenceId { get; set; }
    public string DeliveryKey { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
}
