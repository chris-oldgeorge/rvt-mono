// File summary: Coordinates business-layer operations for monitor service workflows.
// Major updates:
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
using Azure;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.BusinessLogic;
using RVT.Entities.DTO;
using RVT.Entities.Ports.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RVT.Entities.Querying;
using Monitor = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Monitors
{

    public interface IMonitorService
    {



        Task<IList<Monitor>> ReadAllAsync();
        Task<Monitor?> ReadOneAsync(Guid Id);



        Task<bool> FleetNrExist(string? FleetNr, CancellationToken cancellationToken = default);
        Task<MonitorStatusTimeCheckDto> MonitorStatusTimeCheck(Guid MonitorId);
        Task<List<MonitorStatusForMonthDto>> MonitorStatusForMonth(Guid MonitorId, int Year, int Month);

        //Deployments
        Task<Deployment?> DeploymentReadOneAsync(Guid DeploymentId);
        Task<Deployment?> DeploymentForMonitorAsync(Guid MonitorId);
        Task<Deployment> DeploymentForMonitorAsync(Guid monitorId, DateTime notificationTime);

        //AlertLevels
        Task<SearchQueryResult<Alertlevel>> GetAlertRules(Guid MonitorId, OrderByDirectionEnum sortdir, string Sort, CancellationToken cancellationToken = default);
        Task<IList<Alertlevel>> AlertLevelGetAll(Guid Id);

        Task<Alertlevel> AlertLevelReadOne(Guid AlertLevelId);

        //Dust data
        Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels(string SerialId, DateTime FromDate, DateTime ToDate, int AvrgDuration, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels8hourAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<MyAtmDustLevel?> GetLatestDustValue(string SerialId, AveragingPeriodsDustEnum AvrgDuration, CancellationToken cancellationToken = default);

        //Noise data
        Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1hourAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1dayAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevelsSiteAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<NoiseLevel15minAvg?> GetLatestNoiseValue(string SerialId, CancellationToken cancellationToken = default);
        Task<NoiseLevel15minAvg?> GetLatestNoiseValue1day(string SerialId, CancellationToken cancellationToken = default);
        Task<SvantekBatteryStatus?> GetBatteryLevelSvantekAsync(string SerialId);

        //Vibration data
        Task<SearchQueryResult<OmnidotsPeakLevel>> GetOmnidotsPeakLevels(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<OmnidotsPeakLevel?> GetLatestVibrationValue(string SerialId, CancellationToken cancellationToken = default);
        Task<OmnidotsMonitorStatus?> GetVibrationMonitorStatusAsync(string SerialId);
        Task<SearchQueryResult<OmnidotsPeakLevel1dayPeak>> GetOmnidotsPeakLevel1dayPeak(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default);
        Task<BatteryLevel?> GetBatteryLevelOmnidotsAsync(string SerialId);
        Task<SearchQueryResult<OmnidotsTrace>> GetVibrationTraces(Guid TraceId, CancellationToken cancellationToken = default);
        Task<OmnidotsTracesIndex?> GetVibrationTracesIndex(string SerialId, DateTime Date);
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
        private readonly IOmnidotsSensorRepository omnidotsSensorRepository;
        private readonly ISvantekMonitorStatusRepository svantekMonitorStatusRepository;
        private readonly IRvtDateTimeProvider dateTimeProvider;
        // Function summary: Initializes this type with the dependencies required by its workflow.
        public MonitorService(IMonitorRepository monitorRepository,
            IAlertlevelRepository alertlevelRepository,
            IDeploymentRepository deploymentRepository,
            ISearchQueryReader timeSeries,
            RVTSearchContext searchContext,
            IOmnidotsSensorRepository omnidotsSensorRepository,
            ISvantekMonitorStatusRepository svantekMonitorStatusRepository,
            IRvtDateTimeProvider dateTimeProvider)
        {
            this.monitorRepository = monitorRepository;
            this.alertlevelRepository = alertlevelRepository;
            this.deploymentRepository = deploymentRepository;
            this.timeSeries = timeSeries;
            this.searchContext = searchContext;
            this.omnidotsSensorRepository = omnidotsSensorRepository;
            this.svantekMonitorStatusRepository = svantekMonitorStatusRepository;
            this.dateTimeProvider = dateTimeProvider;
        }

        // Function summary: Handles the fleet nr exist workflow for this module.
        public async Task<bool> FleetNrExist(string? FleetNr, CancellationToken cancellationToken = default)
        {
            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "FleetNr" });

            List<Filter> query = new List<Filter> {
                new SingleFilter{ Operation = Op.Equals, PropertyName = "FleetNr", Value = FleetNr }
            };
            var res = await monitorRepository.ReadFilteredAsync(query, orderBy.ToArray(), 100, new Paging { paged = true, page = (int)1, pageSize = 200 }, cancellationToken);
            return res.RecordCount > 0;
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

        // Function summary: Handles the monitor status time check workflow for this module.
        public Task<MonitorStatusTimeCheckDto> MonitorStatusTimeCheck(Guid MonitorId)
        {
            return monitorRepository.MonitorStatusTimeCheck(MonitorId);
        }
        // Function summary: Handles the monitor status for month workflow for this module.
        public Task<List<MonitorStatusForMonthDto>> MonitorStatusForMonth(Guid MonitorId, int Year, int Month)
        {
            return monitorRepository.MonitorStatusForMonth(MonitorId, Year, Month);
        }

        #region AlertLevel 
        // Function summary: Retrieves alert rules data for callers.
        public Task<SearchQueryResult<Alertlevel>> GetAlertRules(Guid MonitorId, OrderByDirectionEnum sortdir, string Sort, CancellationToken cancellationToken = default)

        {
            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir, OrderByColumn = "AlertField" });
            }
            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "MonitorId", Value = MonitorId },
                 new SingleFilter { Operation = Op.Equals, PropertyName = "IsDeleted", Value = false }
            };

            return alertlevelRepository.ReadFilteredAsync(query, orderBy.ToArray(), 200, new Paging { paged = false, page = 1, pageSize = 200 }, cancellationToken);
        }
        // Function summary: Handles the alert level get all workflow for this module.
        public Task<IList<Alertlevel>> AlertLevelGetAll(Guid Id)
        {
            return alertlevelRepository.ReadAllForMonitorAsync(Id);
        }
        // Function summary: Handles the alert level read one workflow for this module.
        public async Task<Alertlevel> AlertLevelReadOne(Guid AlertLevelId)
        {
            return (await alertlevelRepository.GetByIdAsync(AlertLevelId))!;
        }
        //Return active alert levels for a monitor


        #endregion

        #region Deployment
        //Returns current  Deployment if any
        // Function summary: Handles the deployment read one workflow for this module.
        public Task<Deployment?> DeploymentReadOneAsync(Guid DeploymentId)
        {
            return deploymentRepository.GetByIdAsync(DeploymentId);
        }

        // Function summary: Handles the deployment for monitor workflow for this module.
        public Task<Deployment?> DeploymentForMonitorAsync(Guid MonitorId)
        {
            return deploymentRepository.ReadCurrentForMonitiorAsync(MonitorId);
        }
        // Function summary: Handles the deployment for monitor workflow for this module.
        public async Task<Deployment> DeploymentForMonitorAsync(Guid monitorId, DateTime notificationTime)
        {
            return (await deploymentRepository.ReadCurrentForMonitiorAsync(monitorId, notificationTime))!;
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

            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate },
                new SingleFilter { Operation = Op.Equals, PropertyName = "Avrg", Value = AvrgDuration }
            };

            int pageSize = PageSize ?? 1000000;
            var paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<MyAtmDustLevel, MyAtmDustLevel>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.DustLevel, cancellationToken);
        }

        // Function summary: Retrieves my atm dust levels8hour avg data for callers.
        public Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels8hourAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 1000000;
            var paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<MyAtmDustLevel8hourAvg, MyAtmDustLevel>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.DustLevelFromEightHour, cancellationToken);
        }


        // Function summary: Retrieves latest dust value data for callers.
        public async Task<MyAtmDustLevel?> GetLatestDustValue(string SerialId, AveragingPeriodsDustEnum AvrgDuration, CancellationToken cancellationToken = default)
        {
            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Descending, OrderByColumn = "SampleTime" });
            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.Equals, PropertyName = "Avrg", Value = Convert.ToInt32(AvrgDuration, CultureInfo.InvariantCulture) }
            };
            var res = await timeSeries.ReadFilteredAsync<MyAtmDustLevel, MyAtmDustLevel>(query, orderBy.ToArray(), 1, new Paging { paged = false }, TimeSeriesProjections.DustLevel, cancellationToken);
            if (res.RecordCount > 0)
                return res.Value[0];
            else
                return null;
        }

        #endregion

        #region Noise data
        // Function summary: Retrieves air qnoise levels data for callers.
        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 1000000;
            var paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<NoiseLevel15minAvg, NoiseLevel15minAvg>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.NoiseLevel, cancellationToken);
        }

        // Function summary: Retrieves air qnoise levels1hour avg data for callers.
        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1hourAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 1000000;
            var paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<NoiseLevel1hourAvg, NoiseLevel15minAvg>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.NoiseLevelFromHour, cancellationToken);
        }

        // Function summary: Retrieves air qnoise levels1day avg data for callers.
        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1dayAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            FromDate = FromDate.UtcToLocal(dateTimeProvider).Date;
            ToDate = ToDate.UtcToLocal(dateTimeProvider).Date;

            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 1000000;
            var paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<NoiseLevel1dayAvg, NoiseLevel15minAvg>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.NoiseLevelFromDay, cancellationToken);
        }

        // Function summary: Retrieves air qnoise levels site avg data for callers.
        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevelsSiteAvg(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            FromDate = FromDate.UtcToLocal(dateTimeProvider).Date;
            ToDate = ToDate.UtcToLocal(dateTimeProvider).Date;

            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 1000000;
            var paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<NoiseLevelSiteAvg, NoiseLevel15minAvg>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.NoiseLevelFromSite, cancellationToken);
        }

        // Function summary: Retrieves latest noise value data for callers.
        public async Task<NoiseLevel15minAvg?> GetLatestNoiseValue(string SerialId, CancellationToken cancellationToken = default)
        {
            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Descending, OrderByColumn = "SampleTime" });
            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId }
            };
            var res = await timeSeries.ReadFilteredAsync<NoiseLevel15minAvg, NoiseLevel15minAvg>(query, orderBy.ToArray(), 1, new Paging { paged = false }, TimeSeriesProjections.NoiseLevel, cancellationToken);
            if (res.RecordCount > 0)
                return res.Value[0];
            else
                return null;
        }

        // Function summary: Retrieves latest noise value1day data for callers.
        public async Task<NoiseLevel15minAvg?> GetLatestNoiseValue1day(string SerialId, CancellationToken cancellationToken = default)
        {
            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Descending, OrderByColumn = "SampleTime" });
            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId }
            };
            var res = await timeSeries.ReadFilteredAsync<NoiseLevel1dayAvg, NoiseLevel15minAvg>(query, orderBy.ToArray(), 1, new Paging { paged = false }, TimeSeriesProjections.NoiseLevelFromDay, cancellationToken);
            if (res.RecordCount > 0)
                return res.Value[0];
            else
                return null;
        }

        #endregion


        #region Vibration data
        // Function summary: Retrieves omnidots peak levels data for callers.
        public Task<SearchQueryResult<OmnidotsPeakLevel>> GetOmnidotsPeakLevels(string SerialId, DateTime FromDate, DateTime ToDate, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default)
        {
            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 30000;
            var paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };
            if ((ToDate - FromDate).TotalHours < 1) //Samples every 2 second
                return timeSeries.ReadFilteredAsync<OmnidotsPeakLevel, OmnidotsPeakLevel>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.PeakLevel, cancellationToken);
            else if ((ToDate - FromDate).TotalHours < 4) //Samples every 2 second
                return timeSeries.ReadFilteredAsync<OmnidotsPeakLevel1min, OmnidotsPeakLevel>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.PeakLevelFrom1Min, cancellationToken);
            else if ((ToDate - FromDate).TotalDays < 1) //samples every 5min
                return timeSeries.ReadFilteredAsync<OmnidotsPeakLevel5min, OmnidotsPeakLevel>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.PeakLevelFrom5Min, cancellationToken);
            else if ((ToDate - FromDate).TotalDays < 2) //samples every 15min
                return timeSeries.ReadFilteredAsync<OmnidotsPeakLevel15min, OmnidotsPeakLevel>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.PeakLevelFrom15Min, cancellationToken);
            else //Samples every 20 min
                return timeSeries.ReadFilteredAsync<OmnidotsPeakLevel20min, OmnidotsPeakLevel>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.PeakLevelFrom20Min, cancellationToken);

        }

        // Function summary: Retrieves latest vibration value data for callers.
        public async Task<OmnidotsPeakLevel?> GetLatestVibrationValue(string SerialId, CancellationToken cancellationToken = default)
        {
            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Descending, OrderByColumn = "SampleTime" });
            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId }
            };
            var res = await timeSeries.ReadFilteredAsync<OmnidotsPeakLevel, OmnidotsPeakLevel>(query, orderBy.ToArray(), 1, new Paging { paged = false }, TimeSeriesProjections.PeakLevel, cancellationToken);
            if (res.RecordCount > 0)
                return res.Value[0];
            else
                return null;
        }
        Task<SearchQueryResult<OmnidotsPeakLevel1dayPeak>> IMonitorService.GetOmnidotsPeakLevel1dayPeak(string SerialId, DateTime FromDate, DateTime ToDate, int? Page, int? PageSize, string? Sort, OrderByDirectionEnum? sortdir, CancellationToken cancellationToken)
        {
            List<OrderByProperty> orderBy = new List<OrderByProperty>();
            if (!string.IsNullOrEmpty(Sort))
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = sortdir ?? OrderByDirectionEnum.Ascending, OrderByColumn = Sort });
            }
            else
            {
                orderBy.Add(new OrderByProperty() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "SampleTime" });
            }

            List<Filter> query = new List<Filter> {
                new SingleFilter { Operation = Op.Equals, PropertyName = "SerialId", Value = SerialId },
                new SingleFilter { Operation = Op.GreaterThanOrEqual, PropertyName = "SampleTime", Value = FromDate },
                new SingleFilter { Operation = Op.LessThanOrEqual, PropertyName = "SampleTime", Value = ToDate }
            };

            int pageSize = PageSize ?? 1000000;
            var paging = Page == null ? new Paging { paged = false } : new Paging { paged = true, page = (int)Page, pageSize = pageSize };

            return timeSeries.ReadFilteredAsync<OmnidotsPeakLevel1dayPeak, OmnidotsPeakLevel1dayPeak>(query, orderBy.ToArray(), pageSize, paging, TimeSeriesProjections.PeakLevelDay, cancellationToken);
        }

        // Function summary: Retrieves vibration monitor status data for callers.
        public Task<OmnidotsMonitorStatus?> GetVibrationMonitorStatusAsync(string SerialId)
        {
            return searchContext.Set<OmnidotsMonitorStatus>()
                .Where(status => status.SerialId == SerialId)
                .FirstOrDefaultAsync();
        }
        // Function summary: Retrieves battery level omnidots data for callers.
        public Task<BatteryLevel?> GetBatteryLevelOmnidotsAsync(string SerialId)
        {
            return omnidotsSensorRepository.ReadBatteryLevelAsync(SerialId);
        }
        // Function summary: Retrieves battery level svantek data for callers.
        public Task<SvantekBatteryStatus?> GetBatteryLevelSvantekAsync(string SerialId)
        {
            return svantekMonitorStatusRepository.ReadBatteryLevelAsync(SerialId);
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

        // Function summary: Retrieves vibration traces index data for callers.
        public Task<OmnidotsTracesIndex?> GetVibrationTracesIndex(string SerialId, DateTime Date)
        {
            return searchContext.Set<OmnidotsTracesIndex>()
                .Where(index => index.SerialId == SerialId && index.StartTime <= Date && index.EndTime >= Date)
                .FirstOrDefaultAsync();
        }

        // Function summary: Handles the traces index read one workflow for this module.
        public async Task<OmnidotsTracesIndex?> TracesIndexReadOne(Guid Id)
        {
            return await searchContext.Set<OmnidotsTracesIndex>().FindAsync(Id);
        }

        #endregion


    }
}
