using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Data.Queries;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Api.Db.EntityFramework;
using Svantek.Api.Db.Mapping;
using Svantek.Model.Dto;
using SvantekMonitor.model.dto;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
namespace Svantek.Api.Db
{

    // Summary: EF Core-backed Svantek database client that preserves the IDBClient contract.
    // Major updates:
    // - 2026-06-20 EF migration: replaced DBUtil SQL calls with provider-aware EF Core operations.
    public class DBClient : IDBClient
    {
        private readonly string _connectionString;

        public DBClient(string connectionString)
        {
            MonitorDb.ValidateLegacyProvider(
                Environment.GetEnvironmentVariable("RVT__DATABASE_PROVIDER"),
                Environment.GetEnvironmentVariable("DatabaseProvider"));
            _connectionString = connectionString;
        }

        public async Task InsertNoiseRecordsTableAsync(
            DataTable table,
            CancellationToken cancellationToken = default)
        {
            List<NoiseDto> dtos = ToNoiseDtos(table);
            if (dtos.Count == 0)
            {
                RvtLogger.Logger.LogWarning("Attempt to insert empty InsertNoiseDtos !");
                return;
            }

            await using SvantekMonitorContext context = CreateContext();
            await InsertNoiseDtosAsync(context, dtos, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public List<NoiseMonitorReadDto> ReadMonitorList(DateTime? lastDataTime)
        {
            DateTime minLastDataTime = lastDataTime ?? SvantekApi.JAN1_1970;
            using SvantekMonitorContext context = CreateContext();

            var rows = (from monitor in context.Monitors.AsNoTracking()
                        join status in context.SvantekMonitorStatus.AsNoTracking() on monitor.SerialId equals status.SerialId
                        join deployment in context.Deployments.AsNoTracking() on monitor.Id equals deployment.MonitorId
                        where monitor.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE &&
                              monitor.FleetNr != null &&
                              deployment.EndDate == null &&
                              (monitor.LastDataTime15Min == null || monitor.LastDataTime15Min >= minLastDataTime)
                        select new
                        {
                            Monitor = monitor,
                            Status = status,
                            Deployment = deployment
                        }).ToList();

            return [.. rows.Select(row => SvantekDbMapper.ToNoiseMonitorReadDto(row.Monitor, row.Status, row.Deployment))];
        }

        public async Task<List<NoiseMonitorReadDto>> ReadMonitorListAsync(
            DateTime? lastDataTime,
            CancellationToken cancellationToken = default)
        {
            DateTime minLastDataTime = lastDataTime ?? SvantekApi.JAN1_1970;
            await using SvantekMonitorContext context = CreateContext();

            var rows = await (from monitor in context.Monitors.AsNoTracking()
                              join status in context.SvantekMonitorStatus.AsNoTracking() on monitor.SerialId equals status.SerialId
                              join deployment in context.Deployments.AsNoTracking() on monitor.Id equals deployment.MonitorId
                              where monitor.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE &&
                                    monitor.FleetNr != null &&
                                    deployment.EndDate == null &&
                                    (monitor.LastDataTime15Min == null || monitor.LastDataTime15Min >= minLastDataTime)
                              select new
                              {
                                  Monitor = monitor,
                                  Status = status,
                                  Deployment = deployment
                              })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(row => SvantekDbMapper.ToNoiseMonitorReadDto(row.Monitor, row.Status, row.Deployment))];
        }

        public async Task WriteMonitorListAsync(
            IReadOnlyList<NoiseMonitorDto> monitors,
            CancellationToken cancellationToken = default)
        {
            await using SvantekMonitorContext context = CreateContext();

            foreach (NoiseMonitorDto dto in monitors)
            {
                MonitorEntity? monitor = await context.Monitors.FirstOrDefaultAsync(row =>
                    row.SerialId == dto.SerialId &&
                    row.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE,
                    cancellationToken).ConfigureAwait(false);

                if (monitor == null)
                {
                    context.Monitors.Add(SvantekDbMapper.ToMonitorEntity(dto));
                }
                else
                {
                    SvantekDbMapper.UpdateMonitorEntity(monitor, dto);
                }

                SvantekMonitorStatusEntity? status = await context.SvantekMonitorStatus.FirstOrDefaultAsync(
                    row => row.SerialId == dto.SerialId,
                    cancellationToken).ConfigureAwait(false);
                if (status == null)
                {
                    context.SvantekMonitorStatus.Add(SvantekDbMapper.ToStatusEntity(dto));
                }
                else
                {
                    SvantekDbMapper.UpdateStatusEntity(status, dto);
                }
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public void HandleException(string message, Exception exception)
        {
            RvtLogger.Logger.LogError("DBClient HandleException message={Value1} exception={Value2}",
                                       message, exception.Message);

            using SvantekMonitorContext context = CreateContext();
            context.SvantekErrorMessages.Add(new SvantekErrorMessageEntity
            {
                Tag = Truncate(message, 64),
                Error = Truncate(exception.Message, 512),
                ErrorTime = DateTime.UtcNow
            });
            context.SaveChanges();
        }

        public async Task WriteLatestTimestampAsync(
            string serialId,
            DateTime lastDataTime,
            CancellationToken cancellationToken = default)
        {
            DateTime normalizedLastDataTime = SvantekDbMapper.NormalizeSampleTimeForPostgreSql(lastDataTime);
            await using SvantekMonitorContext context = CreateContext();
            MonitorEntity? monitor = await context.Monitors.FirstOrDefaultAsync(row =>
                row.SerialId == serialId &&
                row.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE,
                cancellationToken).ConfigureAwait(false);
            if (monitor == null)
            {
                return;
            }

            monitor.LastDataTime15Min = normalizedLastDataTime;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public List<RvtAlertRuleDto> ReadRules(string? serialNumber)
        {
            using SvantekMonitorContext context = CreateContext();

            IQueryable<RvtAlertRuleEntity> query;
            if (serialNumber == null)
            {
                query = context.AlertRules.AsNoTracking().Where(row => row.SerialId == null && !row.IsDeleted);
            }
            else
            {
                query = from rule in context.AlertRules.AsNoTracking()
                        join monitor in context.Monitors.AsNoTracking() on rule.MonitorId equals monitor.Id
                        where monitor.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE &&
                              rule.SerialId == serialNumber &&
                              !rule.IsDeleted
                        select rule;
            }

            return [.. query
                .AsEnumerable()
                .Select(rule => ToRuleDto(rule, serialNumber))];
        }

        public void UpdateAlertRule(RvtAlertRuleDto dto)
        {
            using SvantekMonitorContext context = CreateContext();
            RvtAlertRuleEntity? rule = context.AlertRules.FirstOrDefault(row => row.Id == dto.RuleId);
            if (rule == null)
            {
                return;
            }

            rule.IsActive = dto.IsActive;
            rule.Accessed = dto.Accessed;
            context.SaveChanges();
        }

        public double GetAverageNoiseLevel(string serialNumber, string columnName, DateTime start, DateTime end)
        {
            using SvantekMonitorContext context = CreateContext();
            MonitorAggregateField<SvantekNoiseLevelEntity> field = SvantekAggregateFields.Resolve(columnName);
            DateTime normalizedStart = SvantekDbMapper.NormalizeSampleTimeForPostgreSql(start);
            DateTime normalizedEnd = SvantekDbMapper.NormalizeSampleTimeForPostgreSql(end);
            IQueryable<SvantekNoiseLevelEntity> query = context.NoiseLevels
                .Where(row => row.SerialId == serialNumber)
                .Where(row => row.SampleTime > normalizedStart && row.SampleTime <= normalizedEnd);

            return (field.UseMaximum ? query.Max(field.Selector) : query.Average(field.Selector)) ?? 0.0;
        }

        public async Task SetMonitorOfflineAsync(
            Guid monitorId,
            bool offline,
            CancellationToken cancellationToken = default)
        {
            await using SvantekMonitorContext context = CreateContext();
            MonitorEntity? monitor = await context.Monitors.FirstOrDefaultAsync(
                row => row.Id == monitorId,
                cancellationToken).ConfigureAwait(false);
            if (monitor == null)
            {
                return;
            }

            monitor.Offline = offline;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<List<SiteMonitorsWithSiteHoursDto>> ReadSiteMonitorsWithSiteHoursAsync(
            DateTime day,
            CancellationToken cancellationToken = default)
        {
            await using SvantekMonitorContext context = CreateContext();

            var query = from monitor in context.Monitors.AsNoTracking()
                        join deployment in context.Deployments.AsNoTracking() on monitor.Id equals deployment.MonitorId
                        join contract in context.Contracts.AsNoTracking() on deployment.ContractId equals contract.Id
                        join site in context.Sites.AsNoTracking() on contract.SiteId equals site.Id
                        where monitor.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE &&
                              deployment.EndDate == null &&
                              monitor.Offline == false
                        select new
                        {
                            Monitor = monitor,
                            Site = site
                        };

            query = day.DayOfWeek switch
            {
                DayOfWeek.Saturday => query.Where(row => row.Site.SatStartTime != null && row.Site.SatEndTime != null),
                DayOfWeek.Sunday => query.Where(row => row.Site.SunStartTime != null && row.Site.SunEndTime != null),
                _ => query.Where(row => row.Site.StartTime != null && row.Site.EndTime != null)
            };

            var rows = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            return [.. rows.Select(row =>
            {
                TimeSpan startTime = day.DayOfWeek switch
                {
                    DayOfWeek.Saturday => row.Site.SatStartTime!.Value,
                    DayOfWeek.Sunday => row.Site.SunStartTime!.Value,
                    _ => row.Site.StartTime!.Value
                };
                TimeSpan endTime = day.DayOfWeek switch
                {
                    DayOfWeek.Saturday => row.Site.SatEndTime!.Value,
                    DayOfWeek.Sunday => row.Site.SunEndTime!.Value,
                    _ => row.Site.EndTime!.Value
                };

                return new SiteMonitorsWithSiteHoursDto(
                    monitorId: row.Monitor.Id,
                    fleetnr: row.Monitor.FleetNr ?? string.Empty,
                    serialId: row.Monitor.SerialId,
                    typeOfMonitor: row.Monitor.TypeOfMonitor,
                    offline: row.Monitor.Offline ?? false,
                    siteId: row.Site.Id,
                    siteName: row.Site.SiteName ?? string.Empty,
                    startTime: startTime,
                    endTime: endTime);
            })];
        }

        public async Task WriteDailyAverageAsync(
            Guid siteId,
            Guid monitorId,
            string field,
            double level,
            DateTime timestamp,
            CancellationToken cancellationToken = default)
        {
            await using SvantekMonitorContext context = CreateContext();
            context.SiteAverages.Add(new SiteAverageEntity
            {
                Id = Guid.NewGuid(),
                SiteId = siteId,
                MonitorId = monitorId,
                Field = field,
                Level = level,
                CollectionTime = timestamp
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task Create8hourAverageAsync(
            string serialId,
            DateTime sampleTime,
            CancellationToken cancellationToken = default)
        {
            DateTime normalizedSampleTime = SvantekDbMapper.NormalizeSampleTimeForPostgreSql(sampleTime);
            await using SvantekMonitorContext context = CreateContext();

            bool exists = await context.Noise8HourAverages.AnyAsync(row =>
                row.SerialId == serialId &&
                row.SampleTime == normalizedSampleTime,
                cancellationToken).ConfigureAwait(false);
            if (exists)
            {
                return;
            }

            List<SvantekNoiseLevelEntity> rows = await context.NoiseLevels
                .AsNoTracking()
                .Where(row => row.SerialId == serialId)
                .Where(row => row.SampleTime > normalizedSampleTime.AddHours(-8) && row.SampleTime <= normalizedSampleTime)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (rows.Count == 0)
            {
                return;
            }

            context.Noise8HourAverages.Add(new SvantekNoise8HourAverageEntity
            {
                SerialId = serialId,
                SampleTime = normalizedSampleTime,
                LAeq = rows.Average(row => row.LAeq),
                LAmax = rows.Average(row => row.LAmax),
                LA90 = rows.Average(row => row.LA90),
                LA10 = rows.Average(row => row.LA10),
                LCeq = rows.Average(row => row.LCeq),
                LCmax = rows.Average(row => row.LCmax),
                LC90 = rows.Average(row => row.LC90),
                LC10 = rows.Average(row => row.LC10),
                NumberOfSamples = rows.Count
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task SetMonitorBatteryStatusAsync(
            Guid monitorId,
            byte batteryStatus,
            CancellationToken cancellationToken = default)
        {
            await using SvantekMonitorContext context = CreateContext();
            MonitorEntity? monitor = await context.Monitors.FirstOrDefaultAsync(
                row => row.Id == monitorId,
                cancellationToken).ConfigureAwait(false);
            if (monitor == null)
            {
                return;
            }

            monitor.BatteryStatus = batteryStatus;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<List<NoiseNotificationLatest>> ReadLatestNotificationAsync(
            CancellationToken cancellationToken = default)
        {
            await using SvantekMonitorContext context = CreateContext();
            DateTime cutoff = DateTime.Now.AddHours(-12);

            var rows = await (from notification in context.Notifications.AsNoTracking()
                              join monitor in context.Monitors.AsNoTracking() on notification.MonitorId equals monitor.Id
                              join status in context.SvantekMonitorStatus.AsNoTracking() on monitor.SerialId equals status.SerialId
                              where EF.Property<string?>(notification, "RecordingLink") == null &&
                                    monitor.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE &&
                                    notification.AlertType == 0 &&
                                    notification.NotificationTime >= cutoff
                              select new
                              {
                                  Notification = notification,
                                  Monitor = monitor,
                                  Status = status
                              })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(row => new NoiseNotificationLatest(
                NotificationId: row.Notification.Id,
                MonitorId: row.Monitor.Id,
                FleetNr: row.Monitor.FleetNr ?? string.Empty,
                SerialId: row.Monitor.SerialId,
                ProjectId: row.Status.ProjectId ?? 0,
                PointId: row.Status.PointId ?? 0,
                NotificationTime: row.Notification.NotificationTime,
                AvgPeriod: row.Notification.AveragingPeriod))];
        }

        public async Task<bool> WriteSoundFileAsync(
            Guid notificationId,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            await using SvantekMonitorContext context = CreateContext();
            NotificationEntity? notification = await context.Notifications.FirstOrDefaultAsync(
                row => row.Id == notificationId,
                cancellationToken).ConfigureAwait(false);
            if (notification == null)
            {
                return true;
            }

            context.Entry(notification).Property("RecordingLink").CurrentValue = fileName;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        private SvantekMonitorContext CreateContext()
        {
            MonitorDbOptions monitorOptions = SvantekMonitorDbOptions.Current;
            DbContextOptions<SvantekMonitorContext> options = MonitorDbContextOptionsFactory.CreateOptions<SvantekMonitorContext>(_connectionString);
            return new SvantekMonitorContext(options, monitorOptions);
        }

        private static List<NoiseDto> ToNoiseDtos(DataTable table) =>
            [.. table.Rows
                .Cast<DataRow>()
                .Select(row => new NoiseDto
                {
                    SerialId = Convert.ToString(row["SerialId"]) ?? string.Empty,
                    SampleTime = Convert.ToDateTime(row["SampleTime"]),
                    LAeq = row.Field<double?>("LAeq"),
                    LAmax = row.Field<double?>("LAmax"),
                    LA90 = row.Field<double?>("LA90"),
                    LA10 = row.Field<double?>("LA10"),
                    LCeq = row.Field<double?>("LCeq"),
                    LCmax = row.Field<double?>("LCmax"),
                    LC90 = row.Field<double?>("LC90"),
                    LC10 = row.Field<double?>("LC10")
                })];

        private static async Task InsertNoiseDtosAsync(
            SvantekMonitorContext context,
            IEnumerable<NoiseDto> dtos,
            CancellationToken cancellationToken)
        {
            HashSet<(string SerialId, DateTime SampleTime)> seen = [];

            foreach (NoiseDto dto in dtos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DateTime sampleTime = SvantekDbMapper.NormalizeSampleTimeForPostgreSql(dto.SampleTime);
                if (!seen.Add((dto.SerialId, sampleTime)))
                {
                    continue;
                }

                bool exists = await context.NoiseLevels.AnyAsync(row =>
                    row.SerialId == dto.SerialId &&
                    row.SampleTime == sampleTime,
                    cancellationToken).ConfigureAwait(false);
                if (exists)
                {
                    continue;
                }

                SvantekNoiseLevelEntity entity = SvantekDbMapper.ToNoiseLevelEntity(dto.SerialId, dto);
                entity.SampleTime = sampleTime;
                context.NoiseLevels.Add(entity);
            }
        }

        private static RvtAlertRuleDto ToRuleDto(RvtAlertRuleEntity rule, string? serialId)
        {
            return new RvtAlertRuleDto(
                ruleId: rule.Id,
                serialId: serialId,
                field: rule.AlertField,
                limitOn: rule.LimitOn,
                limitOff: rule.LimitOff,
                averagingPeriod: rule.AveragingPeriod,
                ruleActivityTime: new AlertActivityTimeDto
                {
                    Weekdays = rule.Weekdays,
                    Saturdays = rule.Saturdays,
                    Sundays = rule.Sundays,
                    StartTime = rule.StartTime,
                    EndTime = rule.EndTime
                },
                alertType: (AlertType)rule.AlertType,
                isActive: rule.IsActive,
                isDeleted: rule.IsDeleted,
                created: rule.Created,
                accessed: rule.Accessed);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
