// File summary: Provides role-scoped monitor data grid, graph, trace, and CSV workflows for the portal API.
// Major updates:
// - 2026-07-23 Restored plain database telemetry timestamps to UTC before API row and graph serialization.
// - 2026-07-22 Built vibration trace datasets from the mapped OmnidotsTrace entity.
// - 2026-07-09 pending Moved data view workflow logic out of the API controller.

using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.Entities.Querying;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Monitors;

namespace RvtPortal.Spa.Application.Data;

public interface IDataApplicationService
{
    // Function summary: Returns a role-scoped monitor data grid for the requested deployment.
    Task<DataWorkflowResult<MonitorDataGridResponse>> GetGridAsync(
        Guid deploymentId,
        MonitorDataGridRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken);

    // Function summary: Builds a role-scoped monitor data CSV download for the requested deployment.
    Task<DataDownloadWorkflowResult> DownloadAsync(
        Guid deploymentId,
        MonitorDataGridRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken);

    // Function summary: Returns role-scoped graph data for the requested deployment.
    Task<DataWorkflowResult<MonitorGraphResponse>> GetGraphAsync(
        Guid deploymentId,
        MonitorGraphRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken);

    // Function summary: Returns role-scoped vibration trace indexes for the requested deployment.
    Task<DataWorkflowResult<TraceListResponse>> GetTracesAsync(
        Guid deploymentId,
        TraceListRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken);

    // Function summary: Returns role-scoped vibration trace samples for the requested deployment and trace.
    Task<DataWorkflowResult<TraceDetailResponse>> GetTraceDetailAsync(
        Guid deploymentId,
        Guid traceId,
        DataViewActor actor,
        CancellationToken cancellationToken);

    // Function summary: Builds a role-scoped vibration trace CSV download.
    Task<DataDownloadWorkflowResult> DownloadTraceAsync(
        Guid deploymentId,
        Guid traceId,
        DataViewActor actor,
        CancellationToken cancellationToken);
}

public sealed record DataViewActor(Guid? UserId, bool IsAdmin, bool IsCompanyUser);

public enum DataWorkflowFailureKind
{
    DeploymentNotFound,
    InvalidSort,
    TraceNotFound,
    NoDataToDownload,
    NoTraceDataToDownload
}

public sealed record DataWorkflowFailure(
    DataWorkflowFailureKind Kind,
    Guid? EntityId = null,
    string? RequestedSort = null,
    IReadOnlyCollection<string>? AllowedFields = null)
{
    // Function summary: Creates a deployment visibility failure.
    public static DataWorkflowFailure DeploymentNotFound(Guid deploymentId)
    {
        return new DataWorkflowFailure(DataWorkflowFailureKind.DeploymentNotFound, deploymentId);
    }

    // Function summary: Creates an unsupported sort failure.
    public static DataWorkflowFailure InvalidSort(string requestedSort, IEnumerable<string> allowedFields)
    {
        return new DataWorkflowFailure(DataWorkflowFailureKind.InvalidSort, RequestedSort: requestedSort, AllowedFields: allowedFields.ToArray());
    }

    // Function summary: Creates a trace visibility failure.
    public static DataWorkflowFailure TraceNotFound(Guid traceId)
    {
        return new DataWorkflowFailure(DataWorkflowFailureKind.TraceNotFound, traceId);
    }

    // Function summary: Creates an empty data-download failure.
    public static DataWorkflowFailure NoDataToDownload()
    {
        return new DataWorkflowFailure(DataWorkflowFailureKind.NoDataToDownload);
    }

    // Function summary: Creates an empty trace-download failure.
    public static DataWorkflowFailure NoTraceDataToDownload()
    {
        return new DataWorkflowFailure(DataWorkflowFailureKind.NoTraceDataToDownload);
    }
}

public sealed record DataWorkflowResult<T>(T? Value, DataWorkflowFailure? Failure)
{
    // Function summary: Wraps a successful data workflow value.
    public static DataWorkflowResult<T> Success(T value)
    {
        return new DataWorkflowResult<T>(value, null);
    }

    // Function summary: Wraps a data workflow failure.
    public static DataWorkflowResult<T> Failed(DataWorkflowFailure failure)
    {
        return new DataWorkflowResult<T>(default, failure);
    }
}

public sealed record DataDownloadModel(string Content, string ContentType, string FileName, bool Truncated = false);

public sealed record DataDownloadWorkflowResult(DataDownloadModel? Download, DataWorkflowFailure? Failure)
{
    // Function summary: Wraps a successful download payload.
    public static DataDownloadWorkflowResult Success(string content, string contentType, string fileName, bool truncated = false)
    {
        return new DataDownloadWorkflowResult(new DataDownloadModel(content, contentType, fileName, truncated), null);
    }

    // Function summary: Wraps a download workflow failure.
    public static DataDownloadWorkflowResult Failed(DataWorkflowFailure failure)
    {
        return new DataDownloadWorkflowResult(null, failure);
    }
}

public sealed class DataApplicationService : IDataApplicationService
{
    private const string SampleTimeSort = "SampleTime";
    private const string SampleTimeKey = "sampleTime";
    private const string VibrationMonitorType = "Vibration";
    private const string FrequencyOption = "frequency";
    private const string SiteOption = "site";
    private const string DailyOption = "86400";
    private const string Pm1Key = "pm1";
    private const string Pm25Key = "pm25";
    private const string Pm10Key = "pm10";
    private const string PmTotalKey = "pmTotal";
    private const string LaeqKey = "laeq";
    private const string LamaxKey = "lamax";
    private const string La90Key = "la90";
    private const string La10Key = "la10";
    private const string LceqKey = "lceq";
    private const string LcmaxKey = "lcmax";
    private const string Lc90Key = "lc90";
    private const string Lc10Key = "lc10";
    private const string XvtopKey = "xvtop";
    private const string YvtopKey = "yvtop";
    private const string ZvtopKey = "zvtop";
    private const string Pm1CsvLabel = "Pm1";
    private const string Pm10CsvLabel = "Pm10";
    private const string PmTotalCsvLabel = "PmTotal";
    private const string LaeqLabel = "LAeq";
    private const string LamaxLabel = "LAmax";
    private const string La90Label = "LA90";
    private const string La10Label = "LA10";
    private const string LceqLabel = "LCeq";
    private const string LcmaxLabel = "LCmax";
    private const string Lc90Label = "LC90";
    private const string Lc10Label = "LC10";
    private const string XvtopLabel = "Xvtop";
    private const string YvtopLabel = "Yvtop";
    private const string ZvtopLabel = "Zvtop";
    private const string CsvContentType = "text/csv";

    // Function summary: Maps API sort keys to monitor-data source sort fields.
    private static readonly IReadOnlyDictionary<string, string> SortFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [SampleTimeKey] = SampleTimeSort,
        [Pm1Key] = Pm1CsvLabel,
        [Pm25Key] = "Pm25",
        [Pm10Key] = Pm10CsvLabel,
        [PmTotalKey] = PmTotalCsvLabel,
        [LaeqKey] = LaeqLabel,
        [LamaxKey] = LamaxLabel,
        [La90Key] = La90Label,
        [La10Key] = La10Label,
        [LceqKey] = LceqLabel,
        [LcmaxKey] = LcmaxLabel,
        [Lc90Key] = Lc90Label,
        [Lc10Key] = Lc10Label,
        [XvtopKey] = XvtopLabel,
        [YvtopKey] = YvtopLabel,
        [ZvtopKey] = ZvtopLabel
    };

    private readonly RVTDbContext domainContext;
    private readonly IMonitorDataSource dataSource;

    // Function summary: Initializes data workflows with the domain context and monitor time-series source.
    public DataApplicationService(RVTDbContext domainContext, IMonitorDataSource dataSource)
    {
        this.domainContext = domainContext;
        this.dataSource = dataSource;
    }

    // Function summary: Builds paged grid data for a visible deployment.
    public async Task<DataWorkflowResult<MonitorDataGridResponse>> GetGridAsync(
        Guid deploymentId,
        MonitorDataGridRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        var deployment = await FindVisibleDeploymentAsync(deploymentId, actor, cancellationToken);
        if (deployment?.Monitor is null)
        {
            return DataWorkflowResult<MonitorDataGridResponse>.Failed(DataWorkflowFailure.DeploymentNotFound(deploymentId));
        }

        var requestedSort = string.IsNullOrWhiteSpace(request.Sort) ? SampleTimeKey : request.Sort.Trim();
        if (!SortFields.TryGetValue(requestedSort, out var serviceSort))
        {
            return DataWorkflowResult<MonitorDataGridResponse>.Failed(DataWorkflowFailure.InvalidSort(requestedSort, SortFields.Keys));
        }

        var page = request.GetNormalizedPage();
        var pageSize = request.GetNormalizedPageSize();
        var sortDir = request.GetNormalizedSortDir();
        var fromDate = NormalizeUtc(request.FromDate);
        var toDate = NormalizeUtc(request.ToDate);
        var clampedWindow = ClampRequestToOwnershipWindow(deployment, fromDate, toDate);
        var monitorData = clampedWindow is null
            ? BuildEmptyMonitorData(deployment, fromDate, toDate, request.FilterOption)
            : await dataSource.GetDeploymentDataAsync(new DeploymentDataQuery(
                DeploymentId: deploymentId,
                TraceId: null,
                FilterOption: request.FilterOption,
                FromDate: clampedWindow.Value.From,
                ToDate: clampedWindow.Value.To,
                GraphData: false,
                Page: page,
                PageSize: pageSize,
                Sort: serviceSort,
                SortDir: ToOrderDirection(sortDir)));

        return DataWorkflowResult<MonitorDataGridResponse>.Success(BuildGridResponse(deployment, monitorData, requestedSort, sortDir, page, pageSize));
    }

    // Function summary: Builds a CSV download for visible deployment data.
    public async Task<DataDownloadWorkflowResult> DownloadAsync(
        Guid deploymentId,
        MonitorDataGridRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        var deployment = await FindVisibleDeploymentAsync(deploymentId, actor, cancellationToken);
        if (deployment?.Monitor is null)
        {
            return DataDownloadWorkflowResult.Failed(DataWorkflowFailure.DeploymentNotFound(deploymentId));
        }

        var fromDate = NormalizeUtc(request.FromDate);
        var toDate = NormalizeUtc(request.ToDate);
        var clampedWindow = ClampRequestToOwnershipWindow(deployment, fromDate, toDate);
        var monitorData = clampedWindow is null
            ? BuildEmptyMonitorData(deployment, fromDate, toDate, request.FilterOption)
            : await dataSource.GetDeploymentDataAsync(new DeploymentDataQuery(
                DeploymentId: deploymentId,
                TraceId: null,
                FilterOption: request.FilterOption,
                FromDate: clampedWindow.Value.From,
                ToDate: clampedWindow.Value.To,
                GraphData: false,
                Sort: SampleTimeSort,
                SortDir: OrderByDirectionEnum.Ascending));
        var response = BuildGridResponse(deployment, monitorData, SampleTimeKey, SortDirections.Ascending, 1, Math.Max(RowCount(monitorData), 1));
        if (response.Total == 0)
        {
            return DataDownloadWorkflowResult.Failed(DataWorkflowFailure.NoDataToDownload());
        }

        var csv = BuildDataCsv(response);
        var fileName = $"{response.MonitorName} ({FilterLabel(response.FilterOption)}).csv";

        // A CSV body cannot carry a flag, so the controller surfaces this as a response header. An export that
        // stopped at the row bound must not look like a complete one.
        return DataDownloadWorkflowResult.Success(csv, CsvContentType, fileName, response.Truncated);
    }

    // Function summary: Builds graph data and alert thresholds for a visible deployment.
    public async Task<DataWorkflowResult<MonitorGraphResponse>> GetGraphAsync(
        Guid deploymentId,
        MonitorGraphRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        var deployment = await FindVisibleDeploymentAsync(deploymentId, actor, cancellationToken);
        if (deployment?.Monitor is null)
        {
            return DataWorkflowResult<MonitorGraphResponse>.Failed(DataWorkflowFailure.DeploymentNotFound(deploymentId));
        }

        var fromDate = NormalizeUtc(request.FromDate);
        var toDate = NormalizeUtc(request.ToDate);
        var clampedWindow = ClampRequestToOwnershipWindow(deployment, fromDate, toDate);
        var monitorData = clampedWindow is null
            ? BuildEmptyMonitorData(deployment, fromDate, toDate, request.FilterOption)
            : await dataSource.GetDeploymentDataAsync(new DeploymentDataQuery(
                DeploymentId: deploymentId,
                TraceId: null,
                FilterOption: request.FilterOption,
                FromDate: clampedWindow.Value.From,
                ToDate: clampedWindow.Value.To,
                GraphData: true));

        return DataWorkflowResult<MonitorGraphResponse>.Success(await BuildGraphResponseAsync(deployment, monitorData, traceId: null, cancellationToken));
    }

    // Function summary: Builds the visible trace list for a vibration deployment.
    public async Task<DataWorkflowResult<TraceListResponse>> GetTracesAsync(
        Guid deploymentId,
        TraceListRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        var deployment = await FindVisibleDeploymentAsync(deploymentId, actor, cancellationToken);
        if (deployment?.Monitor is null)
        {
            return DataWorkflowResult<TraceListResponse>.Failed(DataWorkflowFailure.DeploymentNotFound(deploymentId));
        }

        if (deployment.Monitor.TypeOfMonitor != MonitorTypeEnum.Vibration)
        {
            return DataWorkflowResult<TraceListResponse>.Success(new TraceListResponse
            {
                DeploymentId = deployment.Id,
                MonitorId = deployment.MonitorId,
                MonitorName = MonitorData.GetMonitorName(deployment.Monitor, traces: true),
                MonitorType = TypeName(deployment.Monitor.TypeOfMonitor)
            });
        }

        var clampedWindow = ClampRequestToOwnershipWindow(deployment, NormalizeUtc(request.FromDate), NormalizeUtc(request.ToDate));
        var traceIndexes = clampedWindow is null
            ? []
            : await dataSource.GetTraceIndexesAsync(deployment.Monitor.SerialId, clampedWindow.Value.From, clampedWindow.Value.To);
        return DataWorkflowResult<TraceListResponse>.Success(new TraceListResponse
        {
            DeploymentId = deployment.Id,
            MonitorId = deployment.MonitorId,
            MonitorName = MonitorData.GetMonitorName(deployment.Monitor, traces: true),
            MonitorType = TypeName(deployment.Monitor.TypeOfMonitor),
            Traces = traceIndexes
                .OrderByDescending(trace => trace.StartTime)
                .Select(trace => new TraceSummaryItem
                {
                    Id = trace.Id,
                    StartTime = trace.StartTime,
                    EndTime = trace.EndTime,
                    DurationSeconds = Math.Max(0, (int)(trace.EndTime - trace.StartTime).TotalSeconds)
                })
                .ToList()
        });
    }

    // Function summary: Builds visible trace sample detail for a deployment and trace pair.
    public async Task<DataWorkflowResult<TraceDetailResponse>> GetTraceDetailAsync(
        Guid deploymentId,
        Guid traceId,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        var deployment = await FindVisibleDeploymentAsync(deploymentId, actor, cancellationToken);
        if (deployment?.Monitor is null)
        {
            return DataWorkflowResult<TraceDetailResponse>.Failed(DataWorkflowFailure.DeploymentNotFound(deploymentId));
        }

        var traceIndex = await dataSource.GetTraceIndexAsync(traceId);
        if (traceIndex is null || !string.Equals(traceIndex.SerialId, deployment.Monitor.SerialId, StringComparison.OrdinalIgnoreCase))
        {
            return DataWorkflowResult<TraceDetailResponse>.Failed(DataWorkflowFailure.TraceNotFound(traceId));
        }

        var ownershipWindow = MonitorOwnershipWindowResolver.ForDeployment(deployment);
        if (!ownershipWindow.Contains(traceIndex.StartTime))
        {
            return DataWorkflowResult<TraceDetailResponse>.Failed(DataWorkflowFailure.TraceNotFound(traceId));
        }

        var monitorData = await dataSource.GetDeploymentDataAsync(new DeploymentDataQuery(
            DeploymentId: deploymentId,
            TraceId: traceId,
            FilterOption: null,
            FromDate: null,
            ToDate: null,
            GraphData: true));

        return DataWorkflowResult<TraceDetailResponse>.Success(BuildTraceDetailResponse(deployment, traceId, monitorData));
    }

    // Function summary: Builds a CSV download for visible trace samples.
    public async Task<DataDownloadWorkflowResult> DownloadTraceAsync(
        Guid deploymentId,
        Guid traceId,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        var detail = await GetTraceDetailAsync(deploymentId, traceId, actor, cancellationToken);
        if (detail.Failure is not null)
        {
            return DataDownloadWorkflowResult.Failed(detail.Failure);
        }

        var response = detail.Value!;
        if (response.Samples.Count == 0)
        {
            return DataDownloadWorkflowResult.Failed(DataWorkflowFailure.NoTraceDataToDownload());
        }

        var csv = BuildTraceCsv(response);
        return DataDownloadWorkflowResult.Success(csv, CsvContentType, $"{response.MonitorName} ({response.TraceId}).csv");
    }

    // Function summary: Returns a deployment only when it is visible to the current actor.
    private async Task<Deployment?> FindVisibleDeploymentAsync(
        Guid deploymentId,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        var deployment = await domainContext.Deployments
            .AsNoTracking()
            .Include(item => item.Monitor)
            .Include(item => item.Contract)
            .ThenInclude(contract => contract.Site)
            .SingleOrDefaultAsync(item => item.Id == deploymentId, cancellationToken);
        if (deployment is null)
        {
            return null;
        }

        if (actor.IsAdmin)
        {
            return deployment;
        }

        var siteId = deployment.Contract?.SiteiD;
        if (!actor.IsCompanyUser || siteId is null || actor.UserId is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var canRead = await domainContext.SiteUsers
            .AsNoTracking()
            .AnyAsync(siteUser =>
                siteUser.UserId == actor.UserId &&
                siteUser.SiteId == siteId.Value &&
                siteUser.StartDate <= now &&
                (siteUser.EndDate == null || siteUser.EndDate >= now), cancellationToken);
        return canRead ? deployment : null;
    }

    // Function summary: Builds a paged monitor data grid response.
    private static MonitorDataGridResponse BuildGridResponse(
        Deployment deployment,
        MonitorData monitorData,
        string requestedSort,
        string sortDir,
        int page,
        int pageSize)
    {
        var columns = DataColumns(deployment.Monitor.TypeOfMonitor);
        var rows = DataRows(monitorData);
        var total = RowCount(monitorData);
        return new MonitorDataGridResponse
        {
            DeploymentId = deployment.Id,
            MonitorId = deployment.MonitorId,
            MonitorName = MonitorData.GetMonitorName(deployment.Monitor),
            MonitorType = TypeName(deployment.Monitor.TypeOfMonitor),
            MinDate = monitorData.MinDate,
            MaxDate = monitorData.MaxDate,
            FromDate = monitorData.FromDate,
            ToDate = monitorData.ToDate,
            FromDateChanged = monitorData.FromDateChanged,
            ToDateChanged = monitorData.ToDateChanged,
            MaxDuration = FormatDuration(monitorData.MaxDuration),
            FilterOption = monitorData.FilterOption ?? "",
            FilterOptions = ToOptions(monitorData.FilterOptions),
            Columns = columns,
            Rows = rows,
            Truncated = IsTruncated(monitorData),
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
            HasPreviousPage = page > 1 && total > 0,
            HasNextPage = page * pageSize < total,
            Sort = requestedSort,
            SortDir = sortDir
        };
    }

    // Function summary: Builds graph data and thresholds for the requested monitor data.
    private async Task<MonitorGraphResponse> BuildGraphResponseAsync(
        Deployment deployment,
        MonitorData monitorData,
        Guid? traceId,
        CancellationToken cancellationToken)
    {
        var response = new MonitorGraphResponse
        {
            DeploymentId = deployment.Id,
            MonitorId = deployment.MonitorId,
            MonitorName = MonitorData.GetMonitorName(deployment.Monitor),
            MonitorType = TypeName(deployment.Monitor.TypeOfMonitor),
            GraphName = MonitorData.GetMonitorName(deployment.Monitor, traceId is not null),
            MinDate = monitorData.MinDate,
            MaxDate = monitorData.MaxDate,
            FromDate = monitorData.FromDate,
            ToDate = monitorData.ToDate,
            FromDateChanged = monitorData.FromDateChanged,
            ToDateChanged = monitorData.ToDateChanged,
            MaxDuration = FormatDuration(monitorData.MaxDuration),
            FilterOption = monitorData.FilterOption ?? "",
            FilterOptions = ToOptions(monitorData.FilterOptions),
            Truncated = IsTruncated(monitorData),
            DecimalPlaces = DecimalPlaces(deployment.Monitor.TypeOfMonitor),
            YAxisLabel = YAxisLabel(deployment.Monitor.TypeOfMonitor)
        };

        if (deployment.Monitor.TypeOfMonitor == MonitorTypeEnum.Vibration && response.FilterOption == FrequencyOption)
        {
            response.XAxisLabel = "Frequency (Hz)";
            response.XAxisField = "frequency";
            response.XAxisUnit = "Hz";
            response.XAxisNumeric = true;
        }

        response.Datasets = GraphDatasets(deployment.Monitor.TypeOfMonitor, monitorData, traceId is not null);
        response.Thresholds = await domainContext.RvtAlertRules
            .AsNoTracking()
            .Where(rule => rule.MonitorId == deployment.MonitorId && rule.IsActive)
            .OrderBy(rule => rule.AlertField)
            .ThenBy(rule => rule.AlertType)
            .Select(rule => new MonitorGraphThreshold
            {
                Id = rule.Id,
                Field = rule.AlertField,
                AlertType = rule.AlertType.ToString(),
                LimitOn = rule.LimitOn,
                LimitOff = rule.LimitOff,
                AveragingPeriod = rule.AveragingPeriod
            })
            .ToListAsync(cancellationToken);

        return response;
    }

    // Function summary: Builds trace detail response data for callers.
    private static TraceDetailResponse BuildTraceDetailResponse(Deployment deployment, Guid traceId, MonitorData monitorData)
    {
        var samples = monitorData.VibrationTraces?.Value ?? [];
        return new TraceDetailResponse
        {
            DeploymentId = deployment.Id,
            MonitorId = deployment.MonitorId,
            TraceId = traceId,
            MonitorName = MonitorData.GetMonitorName(deployment.Monitor, traces: true),
            FromDate = monitorData.FromDate,
            ToDate = monitorData.ToDate,
            Samples = samples
                .Select((sample, index) => new TraceSampleItem { Index = index, X = sample.X, Y = sample.Y, Z = sample.Z })
                .ToList()
        };
    }

    // Function summary: Clamps requested monitor-bound data ranges to the effective deployment/contract ownership window.
    private static (DateTime From, DateTime To)? ClampRequestToOwnershipWindow(Deployment deployment, DateTime? fromDate, DateTime? toDate)
    {
        var ownershipWindow = MonitorOwnershipWindowResolver.ForDeployment(deployment);
        var requestedFrom = fromDate ?? ownershipWindow.Start;
        var requestedTo = toDate ?? ownershipWindow.End ?? DateTime.UtcNow.AddDays(1);
        if (requestedTo <= requestedFrom || !ownershipWindow.Intersects(requestedFrom, requestedTo))
        {
            return null;
        }

        var clamped = ownershipWindow.Clamp(requestedFrom, requestedTo);
        return clamped.To > clamped.From ? clamped : null;
    }

    // Function summary: Builds an empty monitor data response for requests outside the ownership window.
    private static MonitorData BuildEmptyMonitorData(Deployment deployment, DateTime? fromDate, DateTime? toDate, string? filterOption)
    {
        var ownershipWindow = MonitorOwnershipWindowResolver.ForDeployment(deployment);
        var fallbackTo = ownershipWindow.End ?? DateTime.UtcNow.AddDays(1);
        return new MonitorData
        {
            Monitor = deployment.Monitor,
            MinDate = ownershipWindow.Start,
            MaxDate = fallbackTo,
            FromDate = fromDate ?? ownershipWindow.Start,
            ToDate = toDate ?? fallbackTo,
            FilterOption = filterOption,
            FilterOptions = []
        };
    }

    // Function summary: Returns data-grid columns for the monitor type.
    private static List<MonitorDataColumn> DataColumns(MonitorTypeEnum type)
    {
        var columns = new List<MonitorDataColumn> { new() { Key = SampleTimeKey, Label = "Date" } };
        if (type == MonitorTypeEnum.Dust)
        {
            columns.AddRange([
                new MonitorDataColumn { Key = Pm1Key, Label = "PM1" },
                new MonitorDataColumn { Key = Pm25Key, Label = "PM2.5" },
                new MonitorDataColumn { Key = Pm10Key, Label = "PM10" },
                new MonitorDataColumn { Key = PmTotalKey, Label = "PM Total" }
            ]);
        }
        else if (type == MonitorTypeEnum.Noise)
        {
            columns.AddRange([
                new MonitorDataColumn { Key = LaeqKey, Label = LaeqLabel },
                new MonitorDataColumn { Key = LamaxKey, Label = LamaxLabel },
                new MonitorDataColumn { Key = La90Key, Label = La90Label },
                new MonitorDataColumn { Key = La10Key, Label = La10Label },
                new MonitorDataColumn { Key = LceqKey, Label = LceqLabel },
                new MonitorDataColumn { Key = LcmaxKey, Label = LcmaxLabel },
                new MonitorDataColumn { Key = Lc90Key, Label = Lc90Label },
                new MonitorDataColumn { Key = Lc10Key, Label = Lc10Label }
            ]);
        }
        else if (type == MonitorTypeEnum.Vibration)
        {
            columns.AddRange([
                new MonitorDataColumn { Key = XvtopKey, Label = XvtopLabel },
                new MonitorDataColumn { Key = YvtopKey, Label = YvtopLabel },
                new MonitorDataColumn { Key = ZvtopKey, Label = ZvtopLabel }
            ]);
        }

        return columns;
    }

    // Function summary: Maps monitor data rows into API grid rows.
    private static List<MonitorDataRow> DataRows(MonitorData data)
    {
        if (data.DustLevels is not null)
        {
            return data.DustLevels.Value.Select(row => new MonitorDataRow
            {
                SampleTime = SearchTimestampPolicy.FromDatabase(row.SampleTime),
                Values = new Dictionary<string, double?>
                {
                    [Pm1Key] = row.Pm1,
                    [Pm25Key] = row.Pm25,
                    [Pm10Key] = row.Pm10,
                    [PmTotalKey] = row.PmTotal
                }
            }).ToList();
        }

        if (data.NoiseLevels is not null)
        {
            return data.NoiseLevels.Value.Select(row => new MonitorDataRow
            {
                SampleTime = SearchTimestampPolicy.FromDatabase(row.SampleTime),
                Values = new Dictionary<string, double?>
                {
                    [LaeqKey] = row.Laeq,
                    [LamaxKey] = row.Lamax,
                    [La90Key] = row.La90,
                    [La10Key] = row.La10,
                    [LceqKey] = row.Lceq,
                    [LcmaxKey] = row.Lcmax,
                    [Lc90Key] = row.Lc90,
                    [Lc10Key] = row.Lc10
                }
            }).ToList();
        }

        if (data.VibrationLevels is not null)
        {
            return data.VibrationLevels.Value.Select(row => new MonitorDataRow
            {
                SampleTime = SearchTimestampPolicy.FromDatabase(row.SampleTime),
                Values = new Dictionary<string, double?>
                {
                    [XvtopKey] = row.Xvtop,
                    [YvtopKey] = row.Yvtop,
                    [ZvtopKey] = row.Zvtop
                }
            }).ToList();
        }

        return [];
    }

    // Function summary: Builds graph datasets for time-series, frequency, or trace data.
    private static List<MonitorGraphDataset> GraphDatasets(MonitorTypeEnum type, MonitorData data, bool trace)
    {
        if (type == MonitorTypeEnum.Dust)
        {
            return BuildTimeDatasets(DataRows(data), [
                (Pm1Key, "PM1"),
                (Pm25Key, "PM2.5"),
                (Pm10Key, "PM10"),
                (PmTotalKey, "PM Total")
            ]);
        }

        if (type == MonitorTypeEnum.Noise)
        {
            return BuildTimeDatasets(DataRows(data), [
                (LaeqKey, LaeqLabel),
                (LamaxKey, LamaxLabel),
                (La90Key, La90Label),
                (La10Key, La10Label),
                (LceqKey, LceqLabel),
                (LcmaxKey, LcmaxLabel),
                (Lc90Key, Lc90Label),
                (Lc10Key, Lc10Label)
            ]);
        }

        if (trace)
        {
            return BuildTraceDatasets(data.VibrationTraces?.Value ?? []);
        }

        if (data.VibrationFrequencyMagnitudes is not null)
        {
            return BuildFrequencyDatasets(data.VibrationFrequencyMagnitudes);
        }

        return BuildTimeDatasets(DataRows(data), [
            (XvtopKey, XvtopLabel),
            (YvtopKey, YvtopLabel),
            (ZvtopKey, ZvtopLabel)
        ]);
    }

    // Function summary: Builds time-based graph datasets.
    private static List<MonitorGraphDataset> BuildTimeDatasets(
        IReadOnlyList<MonitorDataRow> rows,
        IReadOnlyList<(string Key, string Label)> fields)
    {
        return fields.Select(field => new MonitorGraphDataset
        {
            Key = field.Key,
            Label = field.Label,
            Points = rows.Select(row => new MonitorGraphPoint { Time = row.SampleTime, Y = row.Values.GetValueOrDefault(field.Key) }).ToList()
        }).ToList();
    }

    // Function summary: Builds frequency graph datasets for vibration monitors.
    private static List<MonitorGraphDataset> BuildFrequencyDatasets(IReadOnlyList<OmnidotsFrequencyMagnitudes> magnitudes)
    {
        return
        [
            new MonitorGraphDataset
            {
                Key = XvtopKey,
                Label = XvtopLabel,
                Points = magnitudes.Select(row => new MonitorGraphPoint { X = row.Frequency, Y = row.XVtop }).ToList()
            },
            new MonitorGraphDataset
            {
                Key = YvtopKey,
                Label = YvtopLabel,
                Points = magnitudes.Select(row => new MonitorGraphPoint { X = row.Frequency, Y = row.YVtop }).ToList()
            },
            new MonitorGraphDataset
            {
                Key = ZvtopKey,
                Label = ZvtopLabel,
                Points = magnitudes.Select(row => new MonitorGraphPoint { X = row.Frequency, Y = row.ZVtop }).ToList()
            }
        ];
    }

    // Function summary: Builds vibration trace graph datasets.
    private static List<MonitorGraphDataset> BuildTraceDatasets(IReadOnlyList<OmnidotsTrace> traces)
    {
        return
        [
            new MonitorGraphDataset
            {
                Key = "x",
                Label = "X",
                Points = traces.Select((row, index) => new MonitorGraphPoint { X = index, Y = row.X }).ToList()
            },
            new MonitorGraphDataset
            {
                Key = "y",
                Label = "Y",
                Points = traces.Select((row, index) => new MonitorGraphPoint { X = index, Y = row.Y }).ToList()
            },
            new MonitorGraphDataset
            {
                Key = "z",
                Label = "Z",
                Points = traces.Select((row, index) => new MonitorGraphPoint { X = index, Y = row.Z }).ToList()
            }
        ];
    }

    // Function summary: Builds monitor grid CSV content.
    private static string BuildDataCsv(MonitorDataGridResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", response.Columns.Select(column => CsvCell(CsvHeaderLabel(column.Key, column.Label)))));
        foreach (var row in response.Rows)
        {
            var cells = new List<string> { CsvCell(FormatCsvDate(row.SampleTime, response.FilterOption)) };
            cells.AddRange(response.Columns.Skip(1).Select(column => CsvCell(FormatNumber(row.Values.GetValueOrDefault(column.Key), response.MonitorType))));
            builder.AppendLine(string.Join(",", cells));
        }

        return builder.ToString();
    }

    // Function summary: Builds vibration trace CSV content.
    private static string BuildTraceCsv(TraceDetailResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Index,X,Y,Z");
        foreach (var sample in response.Samples)
        {
            builder.Append(sample.Index.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatNumber(sample.X, VibrationMonitorType));
            builder.Append(',');
            builder.Append(FormatNumber(sample.Y, VibrationMonitorType));
            builder.Append(',');
            builder.AppendLine(FormatNumber(sample.Z, VibrationMonitorType));
        }

        return builder.ToString();
    }

    // Function summary: Returns CSV-specific labels for data-grid columns.
    private static string CsvHeaderLabel(string key, string fallback)
    {
        if (key == Pm1Key)
        {
            return Pm1CsvLabel;
        }
        if (key == Pm25Key)
        {
            return "Pm2.5";
        }
        if (key == Pm10Key)
        {
            return Pm10CsvLabel;
        }
        if (key == PmTotalKey)
        {
            return PmTotalCsvLabel;
        }
        if (key == XvtopKey)
        {
            return "XVtop";
        }
        if (key == YvtopKey)
        {
            return "YVtop";
        }
        if (key == ZvtopKey)
        {
            return "ZVtop";
        }

        return fallback;
    }

    // Function summary: Escapes a single CSV cell.
    private static string CsvCell(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }

    // Function summary: Formats a data timestamp for CSV output.
    private static string FormatCsvDate(DateTime? value, string filterOption)
    {
        if (value is null)
        {
            return "";
        }

        var format = filterOption == SiteOption || filterOption == DailyOption ? "dd/MM/yyyy" : "dd/MM/yyyy HH:mm:ss";
        return value.Value.ToString(format, CultureInfo.InvariantCulture);
    }

    // Function summary: Formats a numeric value with monitor-type precision.
    private static string FormatNumber(double? value, string monitorType)
    {
        if (value is null)
        {
            return "";
        }

        var format = monitorType == VibrationMonitorType ? "0.0000" : "0.00";
        return value.Value.ToString(format, CultureInfo.InvariantCulture);
    }

    // Function summary: Returns the record count from the active monitor data shape.
    private static int RowCount(MonitorData data)
    {
        if (data.DustLevels is not null)
        {
            return data.DustLevels.RecordCount;
        }
        if (data.NoiseLevels is not null)
        {
            return data.NoiseLevels.RecordCount;
        }
        if (data.VibrationLevels is not null)
        {
            return data.VibrationLevels.RecordCount;
        }

        return 0;
    }

    // Function summary: Reports whether the active monitor data shape stopped at its row bound.
    private static bool IsTruncated(MonitorData data)
    {
        // Unpaged reads (graph and CSV export) are capped at maximumRecords. The reader knows when it stopped
        // short; this carries that fact to the caller instead of handing back a silently partial series.
        return (data.DustLevels?.HasMore ?? false)
            || (data.NoiseLevels?.HasMore ?? false)
            || (data.VibrationLevels?.HasMore ?? false)
            || (data.VibrationTraces?.HasMore ?? false);
    }

    // Function summary: Maps monitor data source options into API option items.
    private static List<OptionItem> ToOptions(Dictionary<string, string>? options)
    {
        if (options is null)
        {
            return [];
        }

        return options.Select(option => new OptionItem { Value = option.Key, Label = option.Value }).ToList();
    }

    // Function summary: Formats an optional duration for API output.
    private static string? FormatDuration(TimeSpan? duration)
    {
        return duration?.ToString();
    }

    // Function summary: Maps the API sort direction to the monitor data-source sort direction.
    private static OrderByDirectionEnum ToOrderDirection(string sortDir)
    {
        return sortDir == SortDirections.Descending ? OrderByDirectionEnum.Descending : OrderByDirectionEnum.Ascending;
    }

    // Function summary: Treats request dates as UTC for monitor data-source queries.
    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
    }

    // Function summary: Returns the API-facing monitor type name.
    private static string TypeName(MonitorTypeEnum type)
    {
        return type.ToString();
    }

    // Function summary: Returns graph decimal precision for the monitor type.
    private static int DecimalPlaces(MonitorTypeEnum type)
    {
        return type == MonitorTypeEnum.Vibration ? 4 : 2;
    }

    // Function summary: Returns the graph Y-axis label for the monitor type.
    private static string YAxisLabel(MonitorTypeEnum type)
    {
        if (type == MonitorTypeEnum.Dust)
        {
            return "Concentrations";
        }
        if (type == MonitorTypeEnum.Noise)
        {
            return "Sound Levels";
        }

        return "Peak vibration velocity";
    }

    // Function summary: Returns the CSV filename label for a filter option.
    private static string FilterLabel(string filterOption)
    {
        if (filterOption == "900")
        {
            return "15 Min Averages";
        }
        if (filterOption == "3600")
        {
            return "Hourly Averages";
        }
        if (filterOption == "28800")
        {
            return "8 Hour Averages";
        }
        if (filterOption == DailyOption)
        {
            return "Daily Averages";
        }
        if (filterOption == SiteOption)
        {
            return "Site Averages";
        }

        return "All Readings";
    }
}
