// File summary: Exposes API endpoints used by the React portal for data view api contracts workflows.
// Major updates:
// - 2026-07-09 pending Refined generated DTO comments after controller workflow cleanup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using RVT.Entities.Querying;

namespace RvtPortal.Spa.Api;

public class MonitorDataGridRequest : PagedQueryRequest
{
    public string? FilterOption { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class MonitorGraphRequest
{
    public string? FilterOption { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class TraceListRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class MonitorDataGridResponse
{
    public Guid DeploymentId { get; set; }
    public Guid MonitorId { get; set; }
    public string MonitorName { get; set; } = "";
    public string MonitorType { get; set; } = "";
    public DateTime MinDate { get; set; }
    public DateTime MaxDate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public bool FromDateChanged { get; set; }
    public bool ToDateChanged { get; set; }
    public string? MaxDuration { get; set; }
    public string FilterOption { get; set; } = "";
    public List<OptionItem> FilterOptions { get; set; } = [];
    public List<MonitorDataColumn> Columns { get; set; } = [];
    public List<MonitorDataRow> Rows { get; set; } = [];

    /// <summary>
    /// True when the reader hit its row bound and stopped, so this is only part of the requested range.
    /// Without it a capped result is indistinguishable from a complete one.
    /// </summary>
    public bool Truncated { get; set; }
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
    public string Sort { get; set; } = "";
    public string SortDir { get; set; } = SortDirections.Ascending;
}

public class MonitorDataColumn
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
}

public class MonitorDataRow
{
    public DateTime? SampleTime { get; set; }
    public Dictionary<string, double?> Values { get; set; } = [];
}

public class MonitorGraphResponse
{
    public Guid DeploymentId { get; set; }
    public Guid MonitorId { get; set; }
    public string MonitorName { get; set; } = "";
    public string MonitorType { get; set; } = "";
    public string GraphName { get; set; } = "";
    public DateTime MinDate { get; set; }
    public DateTime MaxDate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public bool FromDateChanged { get; set; }
    public bool ToDateChanged { get; set; }
    public string? MaxDuration { get; set; }
    public string FilterOption { get; set; } = "";
    public List<OptionItem> FilterOptions { get; set; } = [];

    /// <summary>
    /// True when the reader hit its row bound and stopped, so the series below covers only part of the
    /// requested range. Without it a capped graph is indistinguishable from a complete one.
    /// </summary>
    public bool Truncated { get; set; }
    public string XAxisLabel { get; set; } = "Date";
    public string XAxisField { get; set; } = "sampleTime";
    public string XAxisUnit { get; set; } = "";
    public bool XAxisNumeric { get; set; }
    public string YAxisLabel { get; set; } = "";
    public int DecimalPlaces { get; set; }
    public List<MonitorGraphDataset> Datasets { get; set; } = [];
    public List<MonitorGraphThreshold> Thresholds { get; set; } = [];
}

public class MonitorGraphDataset
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public List<MonitorGraphPoint> Points { get; set; } = [];
}

public class MonitorGraphPoint
{
    public DateTime? Time { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
}

public class MonitorGraphThreshold
{
    public Guid Id { get; set; }
    public string Field { get; set; } = "";
    public string AlertType { get; set; } = "";
    public double? LimitOn { get; set; }
    public double? LimitOff { get; set; }
    public int? AveragingPeriod { get; set; }
}

public class TraceListResponse
{
    public Guid DeploymentId { get; set; }
    public Guid MonitorId { get; set; }
    public string MonitorName { get; set; } = "";
    public string MonitorType { get; set; } = "";
    public List<TraceSummaryItem> Traces { get; set; } = [];
}

public class TraceSummaryItem
{
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationSeconds { get; set; }
}

public class TraceDetailResponse
{
    public Guid DeploymentId { get; set; }
    public Guid MonitorId { get; set; }
    public Guid TraceId { get; set; }
    public string MonitorName { get; set; } = "";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<TraceSampleItem> Samples { get; set; } = [];
}

public class TraceSampleItem
{
    public int Index { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Z { get; set; }
}

// Function summary: Carries deployment data query parameters into the monitor data source.
public sealed record DeploymentDataQuery(
    Guid DeploymentId,
    Guid? TraceId,
    string? FilterOption,
    DateTime? FromDate,
    DateTime? ToDate,
    bool GraphData,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null,
    OrderByDirectionEnum? SortDir = null);
