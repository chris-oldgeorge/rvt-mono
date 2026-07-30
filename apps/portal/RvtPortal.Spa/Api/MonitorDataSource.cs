// File summary: Exposes API endpoints used by the React portal for monitor data source workflows.
// Major updates:
// - 2026-07-30 pending Threaded cancellation into the trace-index reads and bounded the range read.
// - 2026-07-09 pending Injected the business date-time provider into legacy monitor data range calculations.
// - 2026-07-09 pending Refined generated data-source comments after controller workflow cleanup.
// - 2026-06-26 pending Made trace range reads half-open for ownership-window boundaries.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RvtPortal.Application.Time;
using RvtPortal.Spa.Application.Monitors;

namespace RvtPortal.Spa.Api;

public interface IMonitorDataSource
{
    Task<MonitorData> GetDeploymentDataAsync(DeploymentDataQuery request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OmnidotsTracesIndex>> GetTraceIndexesAsync(
        string serialId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    Task<OmnidotsTracesIndex?> GetTraceIndexAsync(Guid traceId, CancellationToken cancellationToken = default);
}


public sealed class MonitorDataSource : IMonitorDataSource
{
    private readonly IMonitorService monitorService;
    private readonly RVTSearchContext searchContext;
    private readonly IRvtDateTimeProvider dateTimeProvider;

    // Function summary: Initializes monitor data reads with legacy monitor calculations and trace search access.
    public MonitorDataSource(IMonitorService monitorService, RVTSearchContext searchContext, IRvtDateTimeProvider dateTimeProvider)
    {
        this.monitorService = monitorService;
        this.searchContext = searchContext;
        this.dateTimeProvider = dateTimeProvider;
    }

    // Function summary: Reads deployment graph/grid data through the legacy monitor calculation service.
    public Task<MonitorData> GetDeploymentDataAsync(DeploymentDataQuery request, CancellationToken cancellationToken = default)
    {
        return MonitorData.GetDeploymentData(
            monitorService,
            dateTimeProvider,
            request.DeploymentId,
            request.TraceId,
            request.FilterOption,
            request.FromDate,
            request.ToDate,
            request.GraphData,
            request.Page,
            request.PageSize,
            request.Sort,
            request.SortDir,
            cancellationToken);
    }

    /// <summary>
    /// The most trace indexes one request may read. A wide date range on a busy vibration monitor is otherwise
    /// unbounded; the sibling sample read (<c>MonitorService.GetVibrationTraces</c>) has always carried the same
    /// bound. Newest first, so the bound drops the oldest traces rather than the ones a caller is looking at.
    /// </summary>
    internal const int MaximumTraceIndexes = 1000000;

    // Function summary: Returns trace indexes for one serial number over a half-open time range.
    public async Task<IReadOnlyList<OmnidotsTracesIndex>> GetTraceIndexesAsync(
        string serialId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        DateTime databaseFromDate = SearchTimestampPolicy.ToDatabase(fromDate);
        DateTime databaseToDate = SearchTimestampPolicy.ToDatabase(toDate);
        return await searchContext.OmnidotsTracesIndices
            .AsNoTracking()
            .Where(trace =>
                trace.SerialId == serialId &&
                trace.StartTime >= databaseFromDate &&
                trace.StartTime < databaseToDate)
            .OrderByDescending(trace => trace.StartTime)
            .Take(MaximumTraceIndexes)
            .ToListAsync(cancellationToken);
    }

    // Function summary: Returns one trace index by id.
    public Task<OmnidotsTracesIndex?> GetTraceIndexAsync(Guid traceId, CancellationToken cancellationToken = default)
    {
        return searchContext.OmnidotsTracesIndices
            .AsNoTracking()
            .SingleOrDefaultAsync(trace => trace.Id == traceId, cancellationToken);
    }
}
