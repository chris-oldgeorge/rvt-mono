using AirQ.Api.Db.EntityFramework;
using AirQ.Api.Db.Mapping;
using AirQ.Model.Dto;
using AirQMonitor.model.dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace AirQ.Api.Db
{

    // Summary: EF Core-backed AirQ database client that preserves the IDBClient contract.
    // Major updates:
    // - 2026-06-20 EF migration: replaced DBUtil SQL calls with provider-aware EF Core operations.
    public class DBClient : IDBClient
    {
        private readonly string ConnectionString;

        public DBClient(string connectionString)
        {
            MonitorDatabaseProviderGuard.EnsureSupported();
            ConnectionString = connectionString;
        }

        public void InsertNoiseDtos(string serialId, List<NoiseDto> dtos)
        {
            if (dtos.Count == 0)
            {
                RvtLogger.Logger.LogWarning("Attempt to insert empty InsertNoiseDtos !");
                return;
            }

            using var context = CreateContext();
            var seen = new HashSet<DateTime>();
            foreach (var dto in dtos)
            {
                if (!seen.Add(dto.SampleTime))
                {
                    continue;
                }

                var exists = context.NoiseLevels.Any(row =>
                    row.SerialId == serialId &&
                    row.SampleTime == dto.SampleTime);
                if (exists)
                {
                    continue;
                }

                context.NoiseLevels.Add(AirQDbMapper.ToNoiseLevelEntity(serialId, dto));
            }

            context.SaveChanges();
        }

        public List<NoiseMonitorDto> ReadMonitorList(DateTime? lastDataTime)
        {
            var cutoff = lastDataTime ?? AirQApi.JAN1_1970;
            using var context = CreateContext();

            var monitors = context.Monitors
                .AsNoTracking()
                .Where(row => row.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE)
                .Where(row => row.Manufacturer == "Turnkey")
                .Where(row => row.FleetNr != null)
                .Where(row => row.LastDataTime15Min == null || row.LastDataTime15Min >= cutoff)
                .ToList();

            var serialIds = monitors
                .Select(row => row.SerialId)
                .ToHashSet(StringComparer.Ordinal);
            var statuses = context.MonitorStatuses
                .AsNoTracking()
                .Where(row => serialIds.Contains(row.SerialId))
                .ToDictionary(row => row.SerialId, StringComparer.Ordinal);

            return monitors
                .Select(row => AirQDbMapper.ToNoiseMonitorDto(row, statuses))
                .ToList();
        }

        public void WriteMonitorList(List<NoiseMonitorDto> monitors)
        {
            using var context = CreateContext();
            foreach (var dto in monitors)
            {
                var entity = context.Monitors.FirstOrDefault(row =>
                    row.SerialId == dto.SerialId &&
                    row.TypeOfMonitor == dto.TypeOfMonitor);

                if (entity == null)
                {
                    context.Monitors.Add(AirQDbMapper.ToMonitorEntity(dto));
                }
                else
                {
                    AirQDbMapper.UpdateMonitorEntity(entity, dto);
                }

                UpsertMonitorStatus(context, dto.SerialId, dto.MonitorStatus);
            }

            context.SaveChanges();
        }

        public void UpdateMonitorStatus(string serialId, NoiseMonitorStatus monitorStatus)
        {
            using var context = CreateContext();
            var status = context.MonitorStatuses.FirstOrDefault(row => row.SerialId == serialId);
            if (status == null)
            {
                return;
            }

            status.ErrorCount = monitorStatus.ErrorCount;
            context.SaveChanges();
        }

        public void HandleException(string message, Exception exception)
        {
            RvtLogger.Logger.LogError("DBClient HandleException message={Value1} exception={Value2}",
                                       message, exception.Message);

            using var context = CreateContext();
            context.AirQErrorMessages.Add(new AirQErrorMessageEntity
            {
                Tag = Truncate(message, 64),
                Error = Truncate(exception.Message, 512),
                ErrorTime = DateTime.UtcNow
            });
            context.SaveChanges();
        }

        public void WriteLatestTimestamp(string serialId, DateTime lastDataTime)
        {
            using var context = CreateContext();
            var monitor = context.Monitors.FirstOrDefault(row =>
                row.SerialId == serialId &&
                row.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE);
            if (monitor == null)
            {
                return;
            }

            monitor.LastDataTime15Min = lastDataTime;
            context.SaveChanges();
        }

        public List<RvtAlertRuleDto> ReadRules(string? serialNumber)
        {
            using var context = CreateContext();

            IQueryable<RvtAlertRuleEntity> query;
            if (serialNumber == null)
            {
                query = context.AlertRules.AsNoTracking().Where(row => row.SerialId == null);
            }
            else
            {
                query = from rule in context.AlertRules.AsNoTracking()
                        join monitor in context.Monitors.AsNoTracking() on rule.MonitorId equals monitor.Id
                        where monitor.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE &&
                              rule.SerialId == serialNumber
                        select rule;
            }

            return query
                .AsEnumerable()
                .Select(rule => ToRuleDto(rule, serialNumber))
                .ToList();
        }

        public List<RvtContactDto> ReadAlertContacts(Guid monitorId, out Guid siteId)
        {
            using var context = CreateContext();

            var contactRows = (from deployment in context.Deployments.AsNoTracking()
                               join contract in context.Contracts.AsNoTracking() on deployment.ContractId equals contract.Id
                               join siteUser in context.SiteUsers.AsNoTracking() on contract.SiteId equals siteUser.SiteId
                               join setting in context.NotificationSettings.AsNoTracking() on siteUser.Id equals setting.SiteUserId
                               join site in context.Sites.AsNoTracking() on siteUser.SiteId equals site.Id
                               where deployment.MonitorId == monitorId &&
                                     deployment.EndDate == null &&
                                     (setting.Email || setting.SMS)
                               select new
                               {
                                   siteUser.UserId,
                                   setting.Email,
                                   setting.SMS,
                                   setting.StartTime,
                                   setting.EndTime,
                                   SiteId = site.Id
                               }).ToList();

            siteId = contactRows.FirstOrDefault()?.SiteId ?? Guid.Empty;

            var userIds = contactRows
                .Select(row => row.UserId.ToString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var usersById = context.Users
                .AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionary(user => user.Id, StringComparer.OrdinalIgnoreCase);

            return contactRows
                .Where(row => usersById.ContainsKey(row.UserId.ToString()))
                .Select(row =>
                {
                    var user = usersById[row.UserId.ToString()];
                    return new RvtContactDto(
                        useEmail: row.Email,
                        useSms: row.SMS,
                        emailAddress: user.Email,
                        phoneNumber: user.PhoneNumber,
                        sendStartTime: row.StartTime,
                        sendEndTime: row.EndTime);
                })
                .ToList();
        }

        public List<RvtContactDto> ReadAlertContacts(string serialId, out Guid siteId)
        {
            using var context = CreateContext();
            var monitorId = GetMonitorId(context, serialId);
            return ReadAlertContacts(monitorId, out siteId);
        }

        public void WriteNotification(NotificationDto dto)
        {
            using var context = CreateContext();
            context.Notifications.Add(new NotificationEntity
            {
                Id = dto.Id,
                NotificationTime = dto.NotificationTime,
                LimitOn = dto.LimitOn,
                AveragingPeriod = dto.AveragingPeriod,
                Level = dto.Level,
                ClosedTime = dto.ClosedTime,
                ClosedByUser = dto.ClosedByUser,
                MonitorId = dto.MonitorId,
                AlertType = (int)dto.AlertType,
                AlertField = dto.AlertField
            });
            context.SaveChanges();
        }

        public bool HasOpenNotification(Guid monitorId, string alertField, AlertType alertType)
        {
            using var context = CreateContext();

            return context.Notifications
                .AsNoTracking()
                .Any(row => row.MonitorId == monitorId &&
                            row.AlertField == alertField &&
                            row.AlertType == (int)alertType &&
                            row.ClosedTime == null);
        }

        public void UpdateAlertRule(RvtAlertRuleDto dto)
        {
            using var context = CreateContext();
            var rule = context.AlertRules.FirstOrDefault(row => row.Id == dto.RuleId);
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
            using var context = CreateContext();
            var field = AirQAggregateFields.Resolve(columnName);
            var query = context.NoiseLevels
                .Where(row => row.SerialId == serialNumber)
                .Where(row => row.SampleTime > start && row.SampleTime <= end);

            return (field.UseMaximum ? query.Max(field.Selector) : query.Average(field.Selector)) ?? 0.0;
        }

        public void WriteNotificationAudit(Guid notificationId, string address, string message)
        {
            RvtLogger.Logger.LogInformation("WriteNotificationAudit address={Value1}, message={Value2}",
                SensitiveLogRedactor.Redact(address), message);

            using var context = CreateContext();
            context.NotificationAudits.Add(new NotificationSentEntity
            {
                Id = Guid.NewGuid(),
                SendTime = DateTime.UtcNow,
                Address = address,
                ErrorMessage = message,
                NotificationId = notificationId
            });
            context.SaveChanges();
        }

        public void SetMonitorOffline(Guid monitorId, bool offline)
        {
            using var context = CreateContext();
            var monitor = context.Monitors.FirstOrDefault(row => row.Id == monitorId);
            if (monitor == null)
            {
                return;
            }

            monitor.Offline = offline;
            context.SaveChanges();
        }

        public void ClearErrorMessages(DateTime before)
        {
            using var context = CreateContext();
            var messages = context.AirQErrorMessages
                .Where(row => row.ErrorTime < before)
                .ToList();

            context.AirQErrorMessages.RemoveRange(messages);
            context.SaveChanges();
        }

        public SiteInfoDto ReadSiteInfo(Guid siteId)
        {
            using var context = CreateContext();
            var site = context.Sites
                .AsNoTracking()
                .FirstOrDefault(row => row.Id == siteId);
            if (site == null)
            {
                throw AdapterException.Of(string.Format("No site info for site Id={0}", siteId));
            }

            return new SiteInfoDto(
                siteId: siteId,
                startTime: site.StartTime,
                endTime: site.EndTime,
                satStartTime: site.SatStartTime,
                satEndTime: site.SatEndTime,
                sunStartTime: site.SunStartTime,
                sunEndTime: site.SunEndTime);
        }

        public List<SiteMonitorsWithSiteHoursDto> ReadSiteMonitorsWithSiteHours(DateTime Day)
        {
            using var context = CreateContext();

            var query = from monitor in context.Monitors.AsNoTracking()
                        join deployment in context.Deployments.AsNoTracking() on monitor.Id equals deployment.MonitorId
                        join contract in context.Contracts.AsNoTracking() on deployment.ContractId equals contract.Id
                        join site in context.Sites.AsNoTracking() on contract.SiteId equals site.Id
                        where monitor.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE &&
                              monitor.Offline == false &&
                              deployment.EndDate == null
                        select new
                        {
                            Monitor = monitor,
                            Site = site
                        };

            if (Day.DayOfWeek == DayOfWeek.Saturday)
            {
                query = query.Where(row => row.Site.SatStartTime != null && row.Site.SatEndTime != null);
                return query
                    .AsEnumerable()
                    .Select(row => ToSiteMonitorDto(row.Monitor, row.Site, row.Site.SatStartTime!.Value, row.Site.SatEndTime!.Value))
                    .ToList();
            }

            if (Day.DayOfWeek == DayOfWeek.Sunday)
            {
                query = query.Where(row => row.Site.SunStartTime != null && row.Site.SunEndTime != null);
                return query
                    .AsEnumerable()
                    .Select(row => ToSiteMonitorDto(row.Monitor, row.Site, row.Site.SunStartTime!.Value, row.Site.SunEndTime!.Value))
                    .ToList();
            }

            query = query.Where(row => row.Site.StartTime != null && row.Site.EndTime != null);
            return query
                .AsEnumerable()
                .Select(row => ToSiteMonitorDto(row.Monitor, row.Site, row.Site.StartTime!.Value, row.Site.EndTime!.Value))
                .ToList();
        }

        public void WriteDailyAverage(Guid siteId, Guid monitorId, string field, double level, DateTime timestamp)
        {
            using var context = CreateContext();
            context.SiteAverages.Add(new SiteAverageEntity
            {
                Id = Guid.NewGuid(),
                SiteId = siteId,
                MonitorId = monitorId,
                Field = field,
                Level = level,
                CollectionTime = timestamp
            });
            context.SaveChanges();
        }

        public void Create8hourAverage(string serialId, DateTime SampleTime)
        {
            using var context = CreateContext();
            var existing = context.Noise8HourAverages.FirstOrDefault(row =>
                row.SerialId == serialId &&
                row.SampleTime == SampleTime);
            if (existing != null && !AirQMonitorDbOptions.Current.IsPostgreSql)
            {
                return;
            }

            var samples = context.NoiseLevels
                .Where(row => row.SerialId == serialId)
                .Where(row => row.SampleTime > SampleTime.AddHours(-8) && row.SampleTime <= SampleTime);

            var sampleCount = samples.Count();
            if (sampleCount == 0)
            {
                return;
            }

            var average = new AirQNoise8HourAverageEntity
            {
                SerialId = serialId,
                SampleTime = SampleTime,
                LAeq = samples.Average(row => row.LAeq),
                LAmax = samples.Average(row => row.LAmax),
                LA90 = samples.Average(row => row.LA90),
                LA10 = samples.Average(row => row.LA10),
                LCeq = samples.Average(row => row.LCeq),
                LCmax = samples.Average(row => row.LCmax),
                LC90 = samples.Average(row => row.LC90),
                LC10 = samples.Average(row => row.LC10),
                NumberOfSamples = sampleCount
            };

            if (existing == null)
            {
                context.Noise8HourAverages.Add(average);
            }
            else
            {
                Apply8HourAverage(existing, average);
            }

            context.SaveChanges();
        }

        private AirQMonitorContext CreateContext()
        {
            var monitorOptions = AirQMonitorDbOptions.Current;
            var options = MonitorDbContextOptionsFactory.CreateOptions<AirQMonitorContext>(ConnectionString, monitorOptions);
            return new AirQMonitorContext(options, monitorOptions);
        }

        private static Guid GetMonitorId(AirQMonitorContext context, string serialId)
        {
            var monitorId = context.Monitors
                .AsNoTracking()
                .Where(row => row.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE)
                .Where(row => row.SerialId == serialId)
                .Select(row => row.Id)
                .FirstOrDefault();

            if (monitorId == Guid.Empty)
            {
                throw AdapterException.Of(string.Format("No monitor with serialId={0}", serialId));
            }

            return monitorId;
        }

        private static void UpsertMonitorStatus(
            AirQMonitorContext context,
            string serialId,
            NoiseMonitorStatus dto)
        {
            var entity = context.MonitorStatuses.FirstOrDefault(row => row.SerialId == serialId);
            if (entity == null)
            {
                entity = new AirQMonitorStatusEntity
                {
                    Id = serialId,
                    SerialId = serialId
                };
                context.MonitorStatuses.Add(entity);
            }

            AirQDbMapper.UpdateMonitorStatusEntity(entity, dto);
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

        private static SiteMonitorsWithSiteHoursDto ToSiteMonitorDto(
            MonitorEntity monitor,
            SiteEntity site,
            TimeSpan startTime,
            TimeSpan endTime)
        {
            return new SiteMonitorsWithSiteHoursDto(
                monitorId: monitor.Id,
                fleetnr: monitor.FleetNr ?? string.Empty,
                serialId: monitor.SerialId,
                typeOfMonitor: monitor.TypeOfMonitor,
                offline: monitor.Offline ?? false,
                siteId: site.Id,
                siteName: site.SiteName ?? string.Empty,
                startTime: startTime,
                endTime: endTime);
        }

        private static void Apply8HourAverage(
            AirQNoise8HourAverageEntity target,
            AirQNoise8HourAverageEntity source)
        {
            target.LAeq = source.LAeq;
            target.LAmax = source.LAmax;
            target.LA90 = source.LA90;
            target.LA10 = source.LA10;
            target.LCeq = source.LCeq;
            target.LCmax = source.LCmax;
            target.LC90 = source.LC90;
            target.LC10 = source.LC10;
            target.NumberOfSamples = source.NumberOfSamples;
        }

        private static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
