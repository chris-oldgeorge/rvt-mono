// The namespace is retained from the shared-kernel folder this file moved out
// of, so its consumers keep compiling; IDE0130 would force a rename ripple.
#pragma warning disable IDE0130
using Rvt.Monitor.Common.Delivery;

namespace Rvt.Monitor.Common.Data.Entities;

public sealed class MonitorDeliveryOutboxEntity
{
    public Guid Id { get; set; }
    public string Producer { get; set; } = string.Empty;
    public Guid? NotificationId { get; set; }
    public string? CorrelationKey { get; set; }
    public string DeliveryKey { get; set; } = string.Empty;
    public MonitorDeliveryKind Kind { get; set; }
    public string Destination { get; set; } = string.Empty;
    public int PayloadVersion { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? DeadLetteredAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
}
