namespace Rvt.Monitor.Common.Data.Entities;

public sealed class SiteUserEntity
{
    public Guid Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid UserId { get; set; }
    public Guid SiteId { get; set; }
}
