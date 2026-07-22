namespace Rvt.Monitor.Common.Data.Entities;

public sealed class SiteAverageEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid MonitorId { get; set; }
    public string Field { get; set; } = string.Empty;
    public double Level { get; set; }
    public DateTime CollectionTime { get; set; }
}
