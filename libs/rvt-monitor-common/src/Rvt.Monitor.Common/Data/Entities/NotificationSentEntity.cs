namespace Rvt.Monitor.Common.Data.Entities;

public sealed class NotificationSentEntity
{
    public Guid Id { get; set; }
    public DateTime SendTime { get; set; }
    public string Address { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public Guid NotificationId { get; set; }
}
