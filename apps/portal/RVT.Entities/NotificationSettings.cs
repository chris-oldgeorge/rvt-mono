// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

namespace RVT.Entities;

public class NotificationSettings : BaseEntity
{
    public Guid SiteUserId { get; set; }
    public bool Email { get; set; }
    public bool SMS { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
}
