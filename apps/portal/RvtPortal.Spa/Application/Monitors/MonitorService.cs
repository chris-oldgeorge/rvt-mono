// File summary: Coordinates business-layer operations for monitor service workflows.
// Major updates:
// - 2026-07-30 pending Folded the single-method monitor/deployment repository hops into direct domain-context reads.
// - 2026-07-23 Applied the explicit UTC/plain-timestamp conversion at non-date SampleTime query boundaries.
// - 2026-07-22 Read vibration traces through the mapped OmnidotsTrace EF entity.
// - 2026-07-09 pending Routed daily-average date conversion through the injected date-time provider.
// - 2026-06-26 pending Aligned service implementation defaults and parameter names for Sonar cleanup.
// - 2026-06-25 pending Narrowed local order-by builders to concrete lists for CA1859 cleanup.
// - 2026-06-25 pending Aligned nullable repository results/paging/return types and removed unreachable GetAllActive return.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-10 pending Removed redundant async/await from repository pass-through service methods.
// - 2026-06-10 pending Removed stale commented-out search methods for Sonar maintainability.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.Entities.Ports.Persistence;
using RVT.Entities.Querying;
using RvtPortal.Application.Time;
using Monitor = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Monitors;


public interface IMonitorService
{



    Task<Monitor?> ReadOneAsync(Guid id, CancellationToken cancellationToken = default);

    //Deployments
    Task<Deployment?> DeploymentReadOneAsync(Guid deploymentId, CancellationToken cancellationToken = default);

    //Dust data
    [SuppressMessage("Maintainability", "S107:Methods should not have too many parameters", Justification = "The interface preserves the established monitor search contract used by portal workflows.")]
    Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels(string serialId, DateTime fromDate, DateTime toDate, int avrgDuration, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
    [SuppressMessage("Maintainability", "S107:Methods should not have too many parameters", Justification = "The interface preserves the established monitor search contract used by portal workflows.")]
    Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels8hourAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);

    //Noise data
    [SuppressMessage("Maintainability", "S107:Methods should not have too many parameters", Justification = "The interface preserves the established monitor search contract used by portal workflows.")]
    Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
    [SuppressMessage("Maintainability", "S107:Methods should not have too many parameters", Justification = "The interface preserves the established monitor search contract used by portal workflows.")]
    Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1hourAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
    [SuppressMessage("Maintainability", "S107:Methods should not have too many parameters", Justification = "The interface preserves the established monitor search contract used by portal workflows.")]
    Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1dayAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
    [SuppressMessage("Maintainability", "S107:Methods should not have too many parameters", Justification = "The interface preserves the established monitor search contract used by portal workflows.")]
    Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevelsSiteAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);

    //Vibration data
    [SuppressMessage("Maintainability", "S107:Methods should not have too many parameters", Justification = "The interface preserves the established monitor search contract used by portal workflows.")]
    Task<SearchQueryResult<OmnidotsPeakLevel>> GetOmnidotsPeakLevels(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
    Task<OmnidotsMonitorStatus?> GetVibrationMonitorStatusAsync(string serialId, CancellationToken cancellationToken = default);
    Task<SearchQueryResult<OmnidotsTrace>> GetVibrationTraces(Guid traceId, CancellationToken cancellationToken = default);
    Task<OmnidotsTracesIndex?> TracesIndexReadOne(Guid id, CancellationToken cancellationToken = default);
}

public class MonitorService : IMonitorService
{
    private const string SampleTimeProperty = "SampleTime";
    private const string SerialIdProperty = "SerialId";

    private readonly RVTDbContext _domainContext;
    private readonly ISearchQueryReader _timeSeries;
    private readonly RVTSearchContext _searchContext;
    private readonly IRvtDateTimeProvider _dateTimeProvider;
    // Function summary: Initializes this type with the dependencies required by its workflow.
    public MonitorService(RVTDbContext domainContext,
        ISearchQueryReader timeSeries,
        RVTSearchContext searchContext,
        IRvtDateTimeProvider dateTimeProvider)
    {
        _domainContext = domainContext;
        _timeSeries = timeSeries;
        _searchContext = searchContext;
        _dateTimeProvider = dateTimeProvider;
    }

    // Function summary: Retrieves one data for callers.
    public async Task<Monitor?> ReadOneAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _domainContext.MonitorsList.FindAsync([id], cancellationToken);
    }


    #region Deployment
    //Returns current  Deployment if any
    // Function summary: Handles the deployment read one workflow for this module.
    public async Task<Deployment?> DeploymentReadOneAsync(Guid deploymentId, CancellationToken cancellationToken = default)
    {
        return await _domainContext.Deployments.FindAsync([deploymentId], cancellationToken);
    }
    #endregion

    #region Dust data
    // Function summary: Retrieves my atm dust levels data for callers.
    public Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels(string serialId, DateTime fromDate, DateTime toDate, int avrgDuration, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
    {
        if (avrgDuration >= 86400)
        {
            // For 1 day averages, we need to use just the date and ignore the time
            fromDate = fromDate.UtcToLocal(_dateTimeProvider).Date;
            toDate = toDate.UtcToLocal(_dateTimeProvider).Date;
        }
        else
        {
            fromDate = SearchTimestampPolicy.ToDatabase(fromDate);
            toDate = SearchTimestampPolicy.ToDatabase(toDate);
        }

        List<OrderByProperty> orderBy = new();
        if (!string.IsNullOrEmpty(sort))
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = sort });
        }
        else
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = SampleTimeProperty });
        }

        List<Filter> query = new()
        {
            new SingleFilter { Operation = Op.Equals, PropertyName = SerialIdProperty, Value = serialId },
            new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = SampleTimeProperty, Value = fromDate },
            new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = SampleTimeProperty, Value = toDate },
            new SingleFilter { Operation = Op.Equals, PropertyName = "Avrg", Value = avrgDuration }
        };

        int effectivePageSize = pageSize ?? 1000000;
        Paging paging = page == null ? new Paging { Paged = false } : new Paging { Paged = true, Page = (int)page, PageSize = effectivePageSize };

        return _timeSeries.ReadFilteredAsync<MyAtmDustLevel, MyAtmDustLevel>(query, [.. orderBy], effectivePageSize, paging, TimeSeriesProjections.DustLevel, cancellationToken);
    }

    // Function summary: Retrieves my atm dust levels8hour avg data for callers.
    public Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels8hourAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
    {
        fromDate = SearchTimestampPolicy.ToDatabase(fromDate);
        toDate = SearchTimestampPolicy.ToDatabase(toDate);

        List<OrderByProperty> orderBy = new();
        if (!string.IsNullOrEmpty(sort))
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = sort });
        }
        else
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = SampleTimeProperty });
        }

        List<Filter> query = new()
        {
            new SingleFilter { Operation = Op.Equals, PropertyName = SerialIdProperty, Value = serialId },
            new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = SampleTimeProperty, Value = fromDate },
            new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = SampleTimeProperty, Value = toDate }
        };

        int effectivePageSize = pageSize ?? 1000000;
        Paging paging = page == null ? new Paging { Paged = false } : new Paging { Paged = true, Page = (int)page, PageSize = effectivePageSize };

        return _timeSeries.ReadFilteredAsync<MyAtmDustLevel8hourAvg, MyAtmDustLevel>(query, [.. orderBy], effectivePageSize, paging, TimeSeriesProjections.DustLevelFromEightHour, cancellationToken);
    }


    #endregion

    #region Noise data
    // Function summary: Retrieves air qnoise levels data for callers.
    public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
    {
        fromDate = SearchTimestampPolicy.ToDatabase(fromDate);
        toDate = SearchTimestampPolicy.ToDatabase(toDate);

        List<OrderByProperty> orderBy = new();
        if (!string.IsNullOrEmpty(sort))
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = sort });
        }
        else
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = SampleTimeProperty });
        }

        List<Filter> query = new()
        {
            new SingleFilter { Operation = Op.Equals, PropertyName = SerialIdProperty, Value = serialId },
            new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = SampleTimeProperty, Value = fromDate },
            new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = SampleTimeProperty, Value = toDate }
        };

        int effectivePageSize = pageSize ?? 1000000;
        Paging paging = page == null ? new Paging { Paged = false } : new Paging { Paged = true, Page = (int)page, PageSize = effectivePageSize };

        return _timeSeries.ReadFilteredAsync<NoiseLevel15minAvg, NoiseLevel15minAvg>(query, [.. orderBy], effectivePageSize, paging, TimeSeriesProjections.NoiseLevel, cancellationToken);
    }

    // Function summary: Retrieves air qnoise levels1hour avg data for callers.
    public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1hourAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
    {
        fromDate = SearchTimestampPolicy.ToDatabase(fromDate);
        toDate = SearchTimestampPolicy.ToDatabase(toDate);

        List<OrderByProperty> orderBy = new();
        if (!string.IsNullOrEmpty(sort))
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = sort });
        }
        else
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = SampleTimeProperty });
        }

        List<Filter> query = new()
        {
            new SingleFilter { Operation = Op.Equals, PropertyName = SerialIdProperty, Value = serialId },
            new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = SampleTimeProperty, Value = fromDate },
            new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = SampleTimeProperty, Value = toDate }
        };

        int effectivePageSize = pageSize ?? 1000000;
        Paging paging = page == null ? new Paging { Paged = false } : new Paging { Paged = true, Page = (int)page, PageSize = effectivePageSize };

        return _timeSeries.ReadFilteredAsync<NoiseLevel1hourAvg, NoiseLevel15minAvg>(query, [.. orderBy], effectivePageSize, paging, TimeSeriesProjections.NoiseLevelFromHour, cancellationToken);
    }

    // Function summary: Retrieves air qnoise levels1day avg data for callers.
    public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1dayAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
    {
        fromDate = fromDate.UtcToLocal(_dateTimeProvider).Date;
        toDate = toDate.UtcToLocal(_dateTimeProvider).Date;

        List<OrderByProperty> orderBy = new();
        if (!string.IsNullOrEmpty(sort))
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = sort });
        }
        else
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = SampleTimeProperty });
        }

        List<Filter> query = new()
        {
            new SingleFilter { Operation = Op.Equals, PropertyName = SerialIdProperty, Value = serialId },
            new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = SampleTimeProperty, Value = fromDate },
            new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = SampleTimeProperty, Value = toDate }
        };

        int effectivePageSize = pageSize ?? 1000000;
        Paging paging = page == null ? new Paging { Paged = false } : new Paging { Paged = true, Page = (int)page, PageSize = effectivePageSize };

        return _timeSeries.ReadFilteredAsync<NoiseLevel1dayAvg, NoiseLevel15minAvg>(query, [.. orderBy], effectivePageSize, paging, TimeSeriesProjections.NoiseLevelFromDay, cancellationToken);
    }

    // Function summary: Retrieves air qnoise levels site avg data for callers.
    public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevelsSiteAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
    {
        fromDate = fromDate.UtcToLocal(_dateTimeProvider).Date;
        toDate = toDate.UtcToLocal(_dateTimeProvider).Date;

        List<OrderByProperty> orderBy = new();
        if (!string.IsNullOrEmpty(sort))
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = sort });
        }
        else
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = SampleTimeProperty });
        }

        List<Filter> query = new()
        {
            new SingleFilter { Operation = Op.Equals, PropertyName = SerialIdProperty, Value = serialId },
            new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = SampleTimeProperty, Value = fromDate },
            new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = SampleTimeProperty, Value = toDate }
        };

        int effectivePageSize = pageSize ?? 1000000;
        Paging paging = page == null ? new Paging { Paged = false } : new Paging { Paged = true, Page = (int)page, PageSize = effectivePageSize };

        return _timeSeries.ReadFilteredAsync<NoiseLevelSiteAvg, NoiseLevel15minAvg>(query, [.. orderBy], effectivePageSize, paging, TimeSeriesProjections.NoiseLevelFromSite, cancellationToken);
    }

    #endregion


    #region Vibration data
    // Function summary: Retrieves omnidots peak levels data for callers.
    public Task<SearchQueryResult<OmnidotsPeakLevel>> GetOmnidotsPeakLevels(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
    {
        TimeSpan duration = toDate - fromDate;
        fromDate = SearchTimestampPolicy.ToDatabase(fromDate);
        toDate = SearchTimestampPolicy.ToDatabase(toDate);

        List<OrderByProperty> orderBy = new();
        if (!string.IsNullOrEmpty(sort))
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = sort });
        }
        else
        {
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = SampleTimeProperty });
        }

        List<Filter> query = new()
        {
            new SingleFilter { Operation = Op.Equals, PropertyName = SerialIdProperty, Value = serialId },
            new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = SampleTimeProperty, Value = fromDate },
            new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = SampleTimeProperty, Value = toDate }
        };

        int effectivePageSize = pageSize ?? 30000;
        Paging paging = page == null ? new Paging { Paged = false } : new Paging { Paged = true, Page = (int)page, PageSize = effectivePageSize };
        if (duration.TotalHours < 1) //Samples every 2 second
        {
            return _timeSeries.ReadFilteredAsync<OmnidotsPeakLevel, OmnidotsPeakLevel>(query, [.. orderBy], effectivePageSize, paging, TimeSeriesProjections.PeakLevel, cancellationToken);
        }
        else if (duration.TotalHours < 4) //Samples every 2 second
        {
            return _timeSeries.ReadFilteredAsync<OmnidotsPeakLevel1min, OmnidotsPeakLevel>(query, [.. orderBy], effectivePageSize, paging, TimeSeriesProjections.PeakLevelFrom1Min, cancellationToken);
        }
        else if (duration.TotalDays < 1) //samples every 5min
        {
            return _timeSeries.ReadFilteredAsync<OmnidotsPeakLevel5min, OmnidotsPeakLevel>(query, [.. orderBy], effectivePageSize, paging, TimeSeriesProjections.PeakLevelFrom5Min, cancellationToken);
        }
        else if (duration.TotalDays < 2) //samples every 15min
        {
            return _timeSeries.ReadFilteredAsync<OmnidotsPeakLevel15min, OmnidotsPeakLevel>(query, [.. orderBy], effectivePageSize, paging, TimeSeriesProjections.PeakLevelFrom15Min, cancellationToken);
        }
        else //Samples every 20 min
        {
            return _timeSeries.ReadFilteredAsync<OmnidotsPeakLevel20min, OmnidotsPeakLevel>(query, [.. orderBy], effectivePageSize, paging, TimeSeriesProjections.PeakLevelFrom20Min, cancellationToken);
        }
    }

    // Function summary: Retrieves vibration monitor status data for callers.
    public Task<OmnidotsMonitorStatus?> GetVibrationMonitorStatusAsync(string serialId, CancellationToken cancellationToken = default)
    {
        return _searchContext.Set<OmnidotsMonitorStatus>()
            .Where(status => status.SerialId == serialId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Function summary: Retrieves vibration traces data for callers.
    public async Task<SearchQueryResult<OmnidotsTrace>> GetVibrationTraces(Guid traceId, CancellationToken cancellationToken = default)
    {
        // TODO: What do we order traces by?
        List<OmnidotsTrace> records = await _searchContext.OmnidotsTraces
            .AsNoTracking()
            .Where(trace => trace.TraceId == traceId)
            .Take(1000000)
            .ToListAsync(cancellationToken);

        return new SearchQueryResult<OmnidotsTrace>(true, string.Empty, records, records.Count, string.Empty);
    }

    // Function summary: Handles the traces index read one workflow for this module.
    public async Task<OmnidotsTracesIndex?> TracesIndexReadOne(Guid id, CancellationToken cancellationToken = default)
    {
        return await _searchContext.Set<OmnidotsTracesIndex>().FindAsync([id], cancellationToken);
    }

    #endregion


}
