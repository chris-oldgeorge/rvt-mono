// File summary: Provides role-scoped monitor data grid, graph, trace, and CSV workflows for the portal API.
// Major updates:
// - 2026-07-23 Restored plain database telemetry timestamps to UTC before API row and graph serialization.
// - 2026-07-22 Built vibration trace datasets from the mapped OmnidotsTrace entity.
// - 2026-07-09 pending Moved data view workflow logic out of the API controller.

using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
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
    InvalidTimestamp,
    TraceNotFound,
    NoDataToDownload,
    NoTraceDataToDownload
}

public sealed record DataWorkflowFailure(
    DataWorkflowFailureKind Kind,
    Guid? EntityId = null,
    string? RequestedSort = null,
    IReadOnlyCollection<string>? AllowedFields = null,
    IReadOnlyCollection<string>? InvalidFields = null)
{
    // Function summary: Creates a deployment visibility failure.
    public static DataWorkflowFailure DeploymentNotFound(Guid deploymentId)
    {
        return new DataWorkflowFailure(DataWorkflowFailureKind.DeploymentNotFound, deploymentId);
    }

    // Function summary: Creates an unsupported sort failure.
    public static DataWorkflowFailure InvalidSort(string requestedSort, IEnumerable<string> allowedFields)
    {
        return new DataWorkflowFailure(DataWorkflowFailureKind.InvalidSort, RequestedSort: requestedSort, AllowedFields: [.. allowedFields]);
    }

    // Function summary: Creates a failure for request timestamps that are not explicit UTC instants.
    public static DataWorkflowFailure InvalidTimestamp(IEnumerable<string> invalidFields)
    {
        return new DataWorkflowFailure(
            DataWorkflowFailureKind.InvalidTimestamp,
            InvalidFields: [.. invalidFields]);
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

/// <summary>
/// One CSV download, described by the callback that writes it rather than by its finished text. A full-range
/// export is bounded only by the reader's row cap (a million rows), and holding that as a string *and* as its
/// UTF-8 copy cost roughly 100 MB twice per concurrent download; the transport writes the rows straight to the
/// response body instead.
/// </summary>
public sealed record DataDownloadModel(
    Func<Stream, CancellationToken, Task> WriteAsync,
    string ContentType,
    string FileName,
    bool Truncated = false);

public sealed record DataDownloadWorkflowResult(DataDownloadModel? Download, DataWorkflowFailure? Failure)
{
    // Function summary: Wraps a successful download payload.
    public static DataDownloadWorkflowResult Success(
        Func<Stream, CancellationToken, Task> writeAsync,
        string contentType,
        string fileName,
        bool truncated = false)
    {
        return new DataDownloadWorkflowResult(new DataDownloadModel(writeAsync, contentType, fileName, truncated), null);
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
    private static readonly IReadOnlyDictionary<string, string> _sortFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

    private readonly RVTDbContext _domainContext;
    private readonly IMonitorDataSource _dataSource;

    // Function summary: Initializes data workflows with the domain context and monitor time-series source.
    public DataApplicationService(RVTDbContext domainContext, IMonitorDataSource dataSource)
    {
        _domainContext = domainContext;
        _dataSource = dataSource;
    }

    // Function summary: Builds paged grid data for a visible deployment.
    public async Task<DataWorkflowResult<MonitorDataGridResponse>> GetGridAsync(
        Guid deploymentId,
        MonitorDataGridRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        if (ValidateUtcTimestamps((nameof(request.FromDate), request.FromDate), (nameof(request.ToDate), request.ToDate)) is { } timestampFailure)
        {
            return DataWorkflowResult<MonitorDataGridResponse>.Failed(timestampFailure);
        }

        Deployment? deployment = await FindVisibleDeploymentAsync(deploymentId, actor, cancellationToken);
        if (deployment?.Monitor is null)
        {
            return DataWorkflowResult<MonitorDataGridResponse>.Failed(DataWorkflowFailure.DeploymentNotFound(deploymentId));
        }

        string requestedSort = string.IsNullOrWhiteSpace(request.Sort) ? SampleTimeKey : request.Sort.Trim();
        if (!_sortFields.TryGetValue(requestedSort, out string? serviceSort))
        {
            return DataWorkflowResult<MonitorDataGridResponse>.Failed(DataWorkflowFailure.InvalidSort(requestedSort, _sortFields.Keys));
        }

        int page = request.GetNormalizedPage();
        int pageSize = request.GetNormalizedPageSize();
        string sortDir = request.GetNormalizedSortDir();
        DateTime? fromDate = request.FromDate;
        DateTime? toDate = request.ToDate;
        (DateTime From, DateTime To)? clampedWindow = ClampRequestToOwnershipWindow(deployment, fromDate, toDate);
        MonitorData monitorData = clampedWindow is null
            ? BuildEmptyMonitorData(deployment, fromDate, toDate, request.FilterOption)
            : await _dataSource.GetDeploymentDataAsync(new DeploymentDataQuery(
                DeploymentId: deploymentId,
                TraceId: null,
                FilterOption: request.FilterOption,
                FromDate: clampedWindow.Value.From,
                ToDate: clampedWindow.Value.To,
                GraphData: false,
                Page: page,
                PageSize: pageSize,
                Sort: serviceSort,
                SortDir: ToOrderDirection(sortDir)), cancellationToken);

        return DataWorkflowResult<MonitorDataGridResponse>.Success(BuildGridResponse(deployment, monitorData, requestedSort, sortDir, page, pageSize));
    }

    // Function summary: Builds a CSV download for visible deployment data.
    public async Task<DataDownloadWorkflowResult> DownloadAsync(
        Guid deploymentId,
        MonitorDataGridRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        if (ValidateUtcTimestamps((nameof(request.FromDate), request.FromDate), (nameof(request.ToDate), request.ToDate)) is { } timestampFailure)
        {
            return DataDownloadWorkflowResult.Failed(timestampFailure);
        }

        Deployment? deployment = await FindVisibleDeploymentAsync(deploymentId, actor, cancellationToken);
        if (deployment?.Monitor is null)
        {
            return DataDownloadWorkflowResult.Failed(DataWorkflowFailure.DeploymentNotFound(deploymentId));
        }

        DateTime? fromDate = request.FromDate;
        DateTime? toDate = request.ToDate;
        (DateTime From, DateTime To)? clampedWindow = ClampRequestToOwnershipWindow(deployment, fromDate, toDate);
        MonitorData monitorData = clampedWindow is null
            ? BuildEmptyMonitorData(deployment, fromDate, toDate, request.FilterOption)
            : await _dataSource.GetDeploymentDataAsync(new DeploymentDataQuery(
                DeploymentId: deploymentId,
                TraceId: null,
                FilterOption: request.FilterOption,
                FromDate: clampedWindow.Value.From,
                ToDate: clampedWindow.Value.To,
                GraphData: false,
                Sort: SampleTimeSort,
                SortDir: OrderByDirectionEnum.Ascending), cancellationToken);
        MonitorDataGridResponse response = BuildGridResponse(deployment, monitorData, SampleTimeKey, SortDirections.Ascending, 1, Math.Max(RowCount(monitorData), 1));
        if (response.Total == 0)
        {
            return DataDownloadWorkflowResult.Failed(DataWorkflowFailure.NoDataToDownload());
        }

        string fileName = $"{response.MonitorName} ({FilterLabel(response.FilterOption)}).csv";

        // A CSV body cannot carry a flag, so the controller surfaces this as a response header. An export that
        // stopped at the row bound must not look like a complete one.
        return DataDownloadWorkflowResult.Success(
            (stream, streamCancellationToken) => WriteDataCsvAsync(response, stream, streamCancellationToken),
            CsvContentType,
            fileName,
            response.Truncated);
    }

    // Function summary: Builds graph data and alert thresholds for a visible deployment.
    public async Task<DataWorkflowResult<MonitorGraphResponse>> GetGraphAsync(
        Guid deploymentId,
        MonitorGraphRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        if (ValidateUtcTimestamps((nameof(request.FromDate), request.FromDate), (nameof(request.ToDate), request.ToDate)) is { } timestampFailure)
        {
            return DataWorkflowResult<MonitorGraphResponse>.Failed(timestampFailure);
        }

        Deployment? deployment = await FindVisibleDeploymentAsync(deploymentId, actor, cancellationToken);
        if (deployment?.Monitor is null)
        {
            return DataWorkflowResult<MonitorGraphResponse>.Failed(DataWorkflowFailure.DeploymentNotFound(deploymentId));
        }

        DateTime? fromDate = request.FromDate;
        DateTime? toDate = request.ToDate;
        (DateTime From, DateTime To)? clampedWindow = ClampRequestToOwnershipWindow(deployment, fromDate, toDate);
        MonitorData monitorData = clampedWindow is null
            ? BuildEmptyMonitorData(deployment, fromDate, toDate, request.FilterOption)
            : await _dataSource.GetDeploymentDataAsync(new DeploymentDataQuery(
                DeploymentId: deploymentId,
                TraceId: null,
                FilterOption: request.FilterOption,
                FromDate: clampedWindow.Value.From,
                ToDate: clampedWindow.Value.To,
                GraphData: true), cancellationToken);

        return DataWorkflowResult<MonitorGraphResponse>.Success(await BuildGraphResponseAsync(deployment, monitorData, traceId: null, cancellationToken));
    }

    // Function summary: Builds the visible trace list for a vibration deployment.
    public async Task<DataWorkflowResult<TraceListResponse>> GetTracesAsync(
        Guid deploymentId,
        TraceListRequest request,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        if (ValidateUtcTimestamps((nameof(request.FromDate), request.FromDate), (nameof(request.ToDate), request.ToDate)) is { } timestampFailure)
        {
            return DataWorkflowResult<TraceListResponse>.Failed(timestampFailure);
        }

        Deployment? deployment = await FindVisibleDeploymentAsync(deploymentId, actor, cancellationToken);
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

        (DateTime From, DateTime To)? clampedWindow = ClampRequestToOwnershipWindow(deployment, request.FromDate, request.ToDate);
        IReadOnlyList<OmnidotsTracesIndex> traceIndexes = clampedWindow is null
            ? []
            : await _dataSource.GetTraceIndexesAsync(deployment.Monitor.SerialId, clampedWindow.Value.From, clampedWindow.Value.To, cancellationToken);
        return DataWorkflowResult<TraceListResponse>.Success(new TraceListResponse
        {
            DeploymentId = deployment.Id,
            MonitorId = deployment.MonitorId,
            MonitorName = MonitorData.GetMonitorName(deployment.Monitor, traces: true),
            MonitorType = TypeName(deployment.Monitor.TypeOfMonitor),
            Traces = [.. traceIndexes
                .OrderByDescending(trace => trace.StartTime)
                .Select(trace => new TraceSummaryItem
                {
                    Id = trace.Id,
                    StartTime = SearchTimestampPolicy.FromDatabase(trace.StartTime)!.Value,
                    EndTime = SearchTimestampPolicy.FromDatabase(trace.EndTime)!.Value,
                    DurationSeconds = Math.Max(0, (int)(trace.EndTime - trace.StartTime).TotalSeconds)
                })]
        });
    }

    // Function summary: Builds visible trace sample detail for a deployment and trace pair.
    public async Task<DataWorkflowResult<TraceDetailResponse>> GetTraceDetailAsync(
        Guid deploymentId,
        Guid traceId,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        Deployment? deployment = await FindVisibleDeploymentAsync(deploymentId, actor, cancellationToken);
        if (deployment?.Monitor is null)
        {
            return DataWorkflowResult<TraceDetailResponse>.Failed(DataWorkflowFailure.DeploymentNotFound(deploymentId));
        }

        OmnidotsTracesIndex? traceIndex = await _dataSource.GetTraceIndexAsync(traceId, cancellationToken);
        if (traceIndex is null || !string.Equals(traceIndex.SerialId, deployment.Monitor.SerialId, StringComparison.OrdinalIgnoreCase))
        {
            return DataWorkflowResult<TraceDetailResponse>.Failed(DataWorkflowFailure.TraceNotFound(traceId));
        }

        MonitorOwnershipWindow ownershipWindow = MonitorOwnershipWindowResolver.ForDeployment(deployment);
        if (!ownershipWindow.Contains(traceIndex.StartTime))
        {
            return DataWorkflowResult<TraceDetailResponse>.Failed(DataWorkflowFailure.TraceNotFound(traceId));
        }

        MonitorData monitorData = await _dataSource.GetDeploymentDataAsync(new DeploymentDataQuery(
            DeploymentId: deploymentId,
            TraceId: traceId,
            FilterOption: null,
            FromDate: null,
            ToDate: null,
            GraphData: true), cancellationToken);

        return DataWorkflowResult<TraceDetailResponse>.Success(BuildTraceDetailResponse(deployment, traceId, monitorData));
    }

    // Function summary: Builds a CSV download for visible trace samples.
    public async Task<DataDownloadWorkflowResult> DownloadTraceAsync(
        Guid deploymentId,
        Guid traceId,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        DataWorkflowResult<TraceDetailResponse> detail = await GetTraceDetailAsync(deploymentId, traceId, actor, cancellationToken);
        if (detail.Failure is not null)
        {
            return DataDownloadWorkflowResult.Failed(detail.Failure);
        }

        TraceDetailResponse response = detail.Value!;
        if (response.Samples.Count == 0)
        {
            return DataDownloadWorkflowResult.Failed(DataWorkflowFailure.NoTraceDataToDownload());
        }

        return DataDownloadWorkflowResult.Success(
            (stream, streamCancellationToken) => WriteTraceCsvAsync(response, stream, streamCancellationToken),
            CsvContentType,
            $"{response.MonitorName} ({response.TraceId}).csv");
    }

    // Function summary: Returns a deployment only when it is visible to the current actor.
    private async Task<Deployment?> FindVisibleDeploymentAsync(
        Guid deploymentId,
        DataViewActor actor,
        CancellationToken cancellationToken)
    {
        Deployment? deployment = await _domainContext.Deployments
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

        Guid? siteId = deployment.Contract?.SiteiD;
        if (!actor.IsCompanyUser || siteId is null || actor.UserId is null)
        {
            return null;
        }

        DateTime now = DateTime.UtcNow;
        bool canRead = await _domainContext.SiteUsers
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
        List<MonitorDataColumn> columns = DataColumns(deployment.Monitor.TypeOfMonitor);
        List<MonitorDataRow> rows = DataRows(monitorData);
        int total = RowCount(monitorData);
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
        MonitorGraphResponse response = new()
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
        response.Thresholds = await _domainContext.RvtAlertRules
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
        List<OmnidotsTrace> samples = monitorData.VibrationTraces?.Value ?? [];
        return new TraceDetailResponse
        {
            DeploymentId = deployment.Id,
            MonitorId = deployment.MonitorId,
            TraceId = traceId,
            MonitorName = MonitorData.GetMonitorName(deployment.Monitor, traces: true),
            FromDate = SearchTimestampPolicy.FromDatabase(monitorData.FromDate)!.Value,
            ToDate = SearchTimestampPolicy.FromDatabase(monitorData.ToDate)!.Value,
            Samples = [.. samples.Select((sample, index) => new TraceSampleItem { Index = index, X = sample.X, Y = sample.Y, Z = sample.Z })]
        };
    }

    // Function summary: Clamps requested monitor-bound data ranges to the effective deployment/contract ownership window.
    private static (DateTime From, DateTime To)? ClampRequestToOwnershipWindow(Deployment deployment, DateTime? fromDate, DateTime? toDate)
    {
        MonitorOwnershipWindow ownershipWindow = MonitorOwnershipWindowResolver.ForDeployment(deployment);
        DateTime requestedFrom = fromDate ?? ownershipWindow.Start;
        DateTime requestedTo = toDate ?? ownershipWindow.End ?? DateTime.UtcNow.AddDays(1);
        if (requestedTo <= requestedFrom || !ownershipWindow.Intersects(requestedFrom, requestedTo))
        {
            return null;
        }

        (DateTime From, DateTime To) clamped = ownershipWindow.Clamp(requestedFrom, requestedTo);
        return clamped.To > clamped.From ? clamped : null;
    }

    // Function summary: Builds an empty monitor data response for requests outside the ownership window.
    private static MonitorData BuildEmptyMonitorData(Deployment deployment, DateTime? fromDate, DateTime? toDate, string? filterOption)
    {
        MonitorOwnershipWindow ownershipWindow = MonitorOwnershipWindowResolver.ForDeployment(deployment);
        DateTime fallbackTo = ownershipWindow.End ?? DateTime.UtcNow.AddDays(1);
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
        List<MonitorDataColumn> columns = new() { new() { Key = SampleTimeKey, Label = "Date" } };
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
            return [.. data.DustLevels.Value.Select(row => new MonitorDataRow
            {
                SampleTime = SearchTimestampPolicy.FromDatabase(row.SampleTime),
                Values = new Dictionary<string, double?>
                {
                    [Pm1Key] = row.Pm1,
                    [Pm25Key] = row.Pm25,
                    [Pm10Key] = row.Pm10,
                    [PmTotalKey] = row.PmTotal
                }
            })];
        }

        if (data.NoiseLevels is not null)
        {
            return [.. data.NoiseLevels.Value.Select(row => new MonitorDataRow
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
            })];
        }

        if (data.VibrationLevels is not null)
        {
            return [.. data.VibrationLevels.Value.Select(row => new MonitorDataRow
            {
                SampleTime = SearchTimestampPolicy.FromDatabase(row.SampleTime),
                Values = new Dictionary<string, double?>
                {
                    [XvtopKey] = row.Xvtop,
                    [YvtopKey] = row.Yvtop,
                    [ZvtopKey] = row.Zvtop
                }
            })];
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
        return [.. fields.Select(field => new MonitorGraphDataset
        {
            Key = field.Key,
            Label = field.Label,
            Points = [.. rows.Select(row => new MonitorGraphPoint { Time = row.SampleTime, Y = row.Values.GetValueOrDefault(field.Key) })]
        })];
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
                Points = [.. magnitudes.Select(row => new MonitorGraphPoint { X = row.Frequency, Y = row.XVtop })]
            },
            new MonitorGraphDataset
            {
                Key = YvtopKey,
                Label = YvtopLabel,
                Points = [.. magnitudes.Select(row => new MonitorGraphPoint { X = row.Frequency, Y = row.YVtop })]
            },
            new MonitorGraphDataset
            {
                Key = ZvtopKey,
                Label = ZvtopLabel,
                Points = [.. magnitudes.Select(row => new MonitorGraphPoint { X = row.Frequency, Y = row.ZVtop })]
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
                Points = [.. traces.Select((row, index) => new MonitorGraphPoint { X = index, Y = row.X })]
            },
            new MonitorGraphDataset
            {
                Key = "y",
                Label = "Y",
                Points = [.. traces.Select((row, index) => new MonitorGraphPoint { X = index, Y = row.Y })]
            },
            new MonitorGraphDataset
            {
                Key = "z",
                Label = "Z",
                Points = [.. traces.Select((row, index) => new MonitorGraphPoint { X = index, Y = row.Z })]
            }
        ];
    }

    // Function summary: Builds monitor grid CSV content.
    private static async Task WriteDataCsvAsync(
        MonitorDataGridResponse response,
        Stream stream,
        CancellationToken cancellationToken)
    {
        await using StreamWriter writer = CreateCsvWriter(stream);
        await writer.WriteLineAsync(
            string.Join(",", response.Columns.Select(column => CsvCell(CsvHeaderLabel(column.Key, column.Label)))).AsMemory(),
            cancellationToken);
        foreach (MonitorDataRow row in response.Rows)
        {
            List<string> cells = new() { CsvCell(FormatCsvDate(row.SampleTime, response.FilterOption)) };
            cells.AddRange(response.Columns.Skip(1).Select(column => CsvCell(FormatNumber(row.Values.GetValueOrDefault(column.Key), response.MonitorType))));
            await writer.WriteLineAsync(string.Join(",", cells).AsMemory(), cancellationToken);
        }
    }

    // Function summary: Writes vibration trace CSV content to the caller's stream.
    private static async Task WriteTraceCsvAsync(
        TraceDetailResponse response,
        Stream stream,
        CancellationToken cancellationToken)
    {
        await using StreamWriter writer = CreateCsvWriter(stream);
        await writer.WriteLineAsync("Index,X,Y,Z".AsMemory(), cancellationToken);
        foreach (TraceSampleItem sample in response.Samples)
        {
            string line = string.Join(
                ',',
                sample.Index.ToString(CultureInfo.InvariantCulture),
                FormatNumber(sample.X, VibrationMonitorType),
                FormatNumber(sample.Y, VibrationMonitorType),
                FormatNumber(sample.Z, VibrationMonitorType));
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }

    /// <summary>
    /// The writer the streamed exports share. UTF-8 without a byte-order mark and the platform newline keep the
    /// bytes identical to the <c>StringBuilder.AppendLine</c> plus <c>Encoding.UTF8.GetBytes</c> pair this
    /// replaced; <c>leaveOpen</c> because the stream is the response body and the host owns it.
    /// </summary>
    private static StreamWriter CreateCsvWriter(Stream stream)
    {
        return new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 16 * 1024,
            leaveOpen: true);
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

        string format = filterOption is SiteOption or DailyOption ? "dd/MM/yyyy" : "dd/MM/yyyy HH:mm:ss";
        return value.Value.ToString(format, CultureInfo.InvariantCulture);
    }

    // Function summary: Formats a numeric value with monitor-type precision.
    private static string FormatNumber(double? value, string monitorType)
    {
        if (value is null)
        {
            return "";
        }

        string format = monitorType == VibrationMonitorType ? "0.0000" : "0.00";
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

        return [.. options.Select(option => new OptionItem { Value = option.Key, Label = option.Value })];
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

    // Function summary: Rejects ambiguous server timestamps instead of relabeling their ticks as UTC.
    private static DataWorkflowFailure? ValidateUtcTimestamps(params (string Field, DateTime? Value)[] timestamps)
    {
        string[] invalidFields = [.. timestamps
            .Where(timestamp => timestamp.Value.HasValue && timestamp.Value.Value.Kind != DateTimeKind.Utc)
            .Select(timestamp => timestamp.Field)];

        return invalidFields.Length == 0
            ? null
            : DataWorkflowFailure.InvalidTimestamp(invalidFields);
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
