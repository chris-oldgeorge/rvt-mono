namespace Rvt.Monitor.Common.Data.Entities;

public sealed class SiteEntity
{
    public Guid Id { get; set; }
    public string? SiteName { get; set; }
    public DateTime CreateDate { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    public string? County { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public TimeSpan? SatStartTime { get; set; }
    public TimeSpan? SatEndTime { get; set; }
    public TimeSpan? SunStartTime { get; set; }
    public TimeSpan? SunEndTime { get; set; }
}
