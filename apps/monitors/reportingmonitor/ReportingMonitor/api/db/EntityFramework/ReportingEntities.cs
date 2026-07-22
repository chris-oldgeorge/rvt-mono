namespace ReportingMonitor.Api.Db.EntityFramework;

public sealed class ReportRuleEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid UserId { get; set; }
    public int Frequency { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public DateTimeOffset? LastGenerated { get; set; }
    public string? ReportName { get; set; }
    public bool Deleted { get; set; }
    public bool IsHiddenSystemRule { get; set; }
}

public sealed class ReportEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid? ReportRuleId { get; set; }
    public int Frequency { get; set; }
    public DateTimeOffset ReportDate { get; set; }
    public DateTimeOffset ReportFrom { get; set; }
    public DateTimeOffset ReportTo { get; set; }
    public string ReportLink { get; set; } = string.Empty;
}

public sealed class ReportSentEntity
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public DateTimeOffset SendTime { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
