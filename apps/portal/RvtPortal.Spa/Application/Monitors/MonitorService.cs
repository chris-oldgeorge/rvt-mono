// File summary: Coordinates business-layer operations for monitor service workflows.
// Major updates:
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

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic;
using RVT.DataAccess;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.Entities.Ports.Persistence;
using RVT.Entities.Querying;
using Monitor = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Monitors
{

    public interface IMonitorService
    {



        Task<IList<Monitor>> ReadAllAsync();
        Task<Monitor?> ReadOneAsync(Guid Id);




        //Deployments
        Task<Deployment?> DeploymentReadOneAsync(Guid DeploymentId);

        //AlertLevels


        //Dust data
        Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels(string SerialId, DateTime FromDate, DateTime ToDate, int AvrgDuration, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels8hourAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);

        //Noise data
        Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1hourAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1dayAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevelsSiteAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);

        //Vibration data
        Task<SearchQueryResult<OmnidotsPeakLevel>> GetOmnidotsPeakLevels(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<OmnidotsMonitorStatus?> GetVibrationMonitorStatusAsync(string SerialId);
        Task<SearchQueryResult<OmnidotsTrace>> GetVibrationTraces(Guid TraceId, CancellationToken cancellationToken = default);
        Task<OmnidotsTracesIndex?> TracesIndexReadOne(Guid Id);

        //  Data services
    }

    public class MonitorDataSearchFilters
    {
        public Guid? MonitorId { get; set; }
        public Guid DeploymentId { get; set; }
        public string? FilterOption { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class MonitorService : IMonitorService
    {
        private readonly IMonitorRepository monitorRepository;
        private readonly ISearchQueryReader timeSeries;
        private readonly RVTSearchContext searchContext;
        private readonly IAlertlevelRepository alertlevelRepository;
        private readonly IDeploymentRepository deploymentRepository;
        private readonly IRvtDateTimeProvider dateTimeProvider;
        // Function summary: Initializes this type with the dependencies required by its workflow.
        public MonitorService(IMonitorRepository monitorRepository,
            IAlertlevelRepository alertlevelRepository,
            IDeploymentRepository deploymentRepository,
            ISearchQueryReader timeSeries,
            RVTSearchContext searchContext,
            IRvtDateTimeProvider dateTimeProvider)
        {
            this.monitorRepository = monitorRepository;
            this.alertlevelRepository = alertlevelRepository;
            this.deploymentRepository = deploymentRepository;
            this.timeSeries = timeSeries;
            this.searchContext = searchContext;
            this.dateTimeProvider = dateTimeProvider;
        }

        // Function summary: Retrieves one data for callers.
        public Task<Monitor?> ReadOneAsync(Guid Id)
        {
            return monitorRepository.GetByIdAsync(Id);
        }

        // Function summary: Retrieves all data for callers.
        public Task<IList<Monitor>> ReadAllAsync()
        {
            return monitorRepository.ReadAllAsync();
        }

        #region AlertLevel 
        //Return active alert levels for a monitor


        #endregion

        #region Deployment
        //Returns current  Deployment if any
        // Function summary: Handles the deployment read one workflow for this module.
        public Task<Deployment?> DeploymentReadOneAsync(Guid DeploymentId)
        {
            return deploymentRepository.GetByIdAsync(DeploymentId);
        }
        #endregion

        #region Dust data
        // Function summary: Retrieves my atm dust levels data for callers.
        public Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels(string SerialId, DateTime FromDate, DateTime ToDate, int AvrgDuration, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            if (AvrgDuration >= 86400)
            {
                // For 1 day averages, we need to use just the date and ignore the time
                FromDate = FromDate.UtcToLocal(dateTimeProvider).Date;
                ToDate = ToDate.UtcToLocal(dateTimeProvider).Date;
            }
            else
            {
                FromDate = SearchTimestampPolicy.ToDatabase(FromDate);
                ToDate = SearchTimestampPolicy.ToDatabase(ToDate);
            }

            List<OrderByProperty> orderBy = new();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new()
            {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate },
                new SingleFilter { Operation = Op.Equals, PropertyName = "Avrg", Value = AvrgDuration }
            };

            int pageSize = PageSize ?? 1000000;
            Paging paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<MyAtmDustLevel, MyAtmDustLevel>(query, [.. orderBy], pageSize, paging, TimeSeriesProjections.DustLevel, cancellationToken);
        }

        // Function summary: Retrieves my atm dust levels8hour avg data for callers.
        public Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels8hourAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            FromDate = SearchTimestampPolicy.ToDatabase(FromDate);
            ToDate = SearchTimestampPolicy.ToDatabase(ToDate);

            List<OrderByProperty> orderBy = new();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new()
            {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 1000000;
            Paging paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<MyAtmDustLevel8hourAvg, MyAtmDustLevel>(query, [.. orderBy], pageSize, paging, TimeSeriesProjections.DustLevelFromEightHour, cancellationToken);
        }


        #endregion

        #region Noise data
        // Function summary: Retrieves air qnoise levels data for callers.
        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            FromDate = SearchTimestampPolicy.ToDatabase(FromDate);
            ToDate = SearchTimestampPolicy.ToDatabase(ToDate);

            List<OrderByProperty> orderBy = new();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new()
            {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 1000000;
            Paging paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<NoiseLevel15minAvg, NoiseLevel15minAvg>(query, [.. orderBy], pageSize, paging, TimeSeriesProjections.NoiseLevel, cancellationToken);
        }

        // Function summary: Retrieves air qnoise levels1hour avg data for callers.
        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1hourAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            FromDate = SearchTimestampPolicy.ToDatabase(FromDate);
            ToDate = SearchTimestampPolicy.ToDatabase(ToDate);

            List<OrderByProperty> orderBy = new();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new()
            {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 1000000;
            Paging paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<NoiseLevel1hourAvg, NoiseLevel15minAvg>(query, [.. orderBy], pageSize, paging, TimeSeriesProjections.NoiseLevelFromHour, cancellationToken);
        }

        // Function summary: Retrieves air qnoise levels1day avg data for callers.
        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1dayAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            FromDate = FromDate.UtcToLocal(dateTimeProvider).Date;
            ToDate = ToDate.UtcToLocal(dateTimeProvider).Date;

            List<OrderByProperty> orderBy = new();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new()
            {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 1000000;
            Paging paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<NoiseLevel1dayAvg, NoiseLevel15minAvg>(query, [.. orderBy], pageSize, paging, TimeSeriesProjections.NoiseLevelFromDay, cancellationToken);
        }

        // Function summary: Retrieves air qnoise levels site avg data for callers.
        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevelsSiteAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            FromDate = FromDate.UtcToLocal(dateTimeProvider).Date;
            ToDate = ToDate.UtcToLocal(dateTimeProvider).Date;

            List<OrderByProperty> orderBy = new();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new()
            {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 1000000;
            Paging paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<NoiseLevelSiteAvg, NoiseLevel15minAvg>(query, [.. orderBy], pageSize, paging, TimeSeriesProjections.NoiseLevelFromSite, cancellationToken);
        }

        #endregion


        #region Vibration data
        // Function summary: Retrieves omnidots peak levels data for callers.
        public Task<SearchQueryResult<OmnidotsPeakLevel>> GetOmnidotsPeakLevels(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            TimeSpan duration = ToDate - FromDate;
            FromDate = SearchTimestampPolicy.ToDatabase(FromDate);
            ToDate = SearchTimestampPolicy.ToDatabase(ToDate);

            List<OrderByProperty> orderBy = new();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new()
            {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 30000;
            Paging paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };
            if (duration.TotalHours < 1) //Samples every 2 second
            {
                return timeSeries.ReadFilteredAsync<OmnidotsPeakLevel, OmnidotsPeakLevel>(query, [.. orderBy], pageSize, paging, TimeSeriesProjections.PeakLevel, cancellationToken);
            }
            else if (duration.TotalHours < 4) //Samples every 2 second
            {
                return timeSeries.ReadFilteredAsync<OmnidotsPeakLevel1min, OmnidotsPeakLevel>(query, [.. orderBy], pageSize, paging, TimeSeriesProjections.PeakLevelFrom1Min, cancellationToken);
            }
            else if (duration.TotalDays < 1) //samples every 5min
            {
                return timeSeries.ReadFilteredAsync<OmnidotsPeakLevel5min, OmnidotsPeakLevel>(query, [.. orderBy], pageSize, paging, TimeSeriesProjections.PeakLevelFrom5Min, cancellationToken);
            }
            else if (duration.TotalDays < 2) //samples every 15min
            {
                return timeSeries.ReadFilteredAsync<OmnidotsPeakLevel15min, OmnidotsPeakLevel>(query, [.. orderBy], pageSize, paging, TimeSeriesProjections.PeakLevelFrom15Min, cancellationToken);
            }
            else //Samples every 20 min
            {
                return timeSeries.ReadFilteredAsync<OmnidotsPeakLevel20min, OmnidotsPeakLevel>(query, [.. orderBy], pageSize, paging, TimeSeriesProjections.PeakLevelFrom20Min, cancellationToken);
            }
        }

        // Function summary: Retrieves vibration monitor status data for callers.
        public Task<OmnidotsMonitorStatus?> GetVibrationMonitorStatusAsync(string SerialId)
        {
            return searchContext.Set<OmnidotsMonitorStatus>()
                .Where(status => status.SerialId == SerialId)
                .FirstOrDefaultAsync();
        }

        // Function summary: Retrieves vibration traces data for callers.
        public async Task<SearchQueryResult<OmnidotsTrace>> GetVibrationTraces(Guid TraceId, CancellationToken cancellationToken = default)
        {
            // TODO: What do we order traces by?
            List<OmnidotsTrace> records = await searchContext.OmnidotsTraces
                .AsNoTracking()
                .Where(trace => trace.TraceId == TraceId)
                .Take(1000000)
                .ToListAsync(cancellationToken);

            return new SearchQueryResult<OmnidotsTrace>(true, string.Empty, records, records.Count, string.Empty);
        }

        // Function summary: Handles the traces index read one workflow for this module.
        public async Task<OmnidotsTracesIndex?> TracesIndexReadOne(Guid Id)
        {
            return await searchContext.Set<OmnidotsTracesIndex>().FindAsync(Id);
        }

        #endregion


    }
}
