namespace Rvt.Monitor.Common.Data.Entities;

public sealed class NotificationSettingEntity
{
    public Guid Id { get; set; }
    public bool Email { get; set; }
    public bool SMS { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public Guid SiteUserId { get; set; }
}
