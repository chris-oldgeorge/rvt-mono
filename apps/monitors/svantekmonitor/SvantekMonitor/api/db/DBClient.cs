using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Api.Db.EntityFramework;
using Svantek.Api.Db.Mapping;
using Svantek.Model.Dto;
using SvantekMonitor.model.dto;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace Svantek.Api.Db
{

    // Summary: EF Core-backed Svantek database client that preserves the IDBClient contract.
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

        public void InsertNoiseDtos(List<NoiseDto> dtos)
        {
            if (dtos.Count == 0)
            {
                RvtLogger.Logger.LogWarning("Attempt to insert empty InsertNoiseDtos !");
                return;
            }

            using var context = CreateContext();
            InsertNoiseDtos(context, dtos);
            context.SaveChanges();
        }

        public void InsertNoiseDtos(string serialId, List<NoiseDto> dtos)
        {
            foreach (var dto in dtos)
            {
                dto.SerialId = serialId;
            }

            InsertNoiseDtos(dtos);
        }

        public void InsertNoiseRecordsTable(DataTable table)
        {
            InsertNoiseDtos(ToNoiseDtos(table));
        }

        public async Task InsertNoiseRecordsTableAsync(
            DataTable table,
            CancellationToken cancellationToken = default)
        {
            var dtos = ToNoiseDtos(table);
            if (dtos.Count == 0)
            {
                RvtLogger.Logger.LogWarning("Attempt to insert empty InsertNoiseDtos !");
                return;
            }

            await using var context = CreateContext();
            await InsertNoiseDtosAsync(context, dtos, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public List<NoiseMonitorReadDto> ReadMonitorList(DateTime? lastDataTime)
        {
            var minLastDataTime = lastDataTime ?? SvantekApi.JAN1_1970;
            using var context = CreateContext();

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

            return rows
                .Select(row => SvantekDbMapper.ToNoiseMonitorReadDto(row.Monitor, row.Status, row.Deployment))
                .ToList();
        }

        public async Task<List<NoiseMonitorReadDto>> ReadMonitorListAsync(
            DateTime? lastDataTime,
            CancellationToken cancellationToken = default)
        {
            var minLastDataTime = lastDataTime ?? SvantekApi.JAN1_1970;
            await using var context = CreateContext();

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

            return rows
                .Select(row => SvantekDbMapper.ToNoiseMonitorReadDto(row.Monitor, row.Status, row.Deployment))
                .ToList();
        }

        public void WriteMonitorList(List<NoiseMonitorDto> monitors)
        {
            using var context = CreateContext();

            foreach (var dto in monitors)
            {
                var monitor = context.Monitors.FirstOrDefault(row =>
                    row.SerialId == dto.SerialId &&
                    row.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE);

                if (monitor == null)
                {
                    context.Monitors.Add(SvantekDbMapper.ToMonitorEntity(dto));
                }
                else
                {
                    SvantekDbMapper.UpdateMonitorEntity(monitor, dto);
                }

                var status = context.SvantekMonitorStatus.FirstOrDefault(row => row.SerialId == dto.SerialId);
                if (status == null)
                {
                    context.SvantekMonitorStatus.Add(SvantekDbMapper.ToStatusEntity(dto));
                }
                else
                {
                    SvantekDbMapper.UpdateStatusEntity(status, dto);
                }
            }

            context.SaveChanges();
        }

        public async Task WriteMonitorListAsync(
            IReadOnlyList<NoiseMonitorDto> monitors,
            CancellationToken cancellationToken = default)
        {
            await using var context = CreateContext();

            foreach (var dto in monitors)
            {
                var monitor = await context.Monitors.FirstOrDefaultAsync(row =>
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

                var status = await context.SvantekMonitorStatus.FirstOrDefaultAsync(
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

        public void UpdateMonitorStatus(string serialId, int errorCount)
        {
            using var context = CreateContext();
            var status = context.SvantekMonitorStatus.FirstOrDefault(row => row.SerialId == serialId);
            if (status == null)
            {
                return;
            }

            status.ErrorCount = errorCount;
            context.SaveChanges();
        }

        public void HandleException(string message, Exception exception)
        {
            RvtLogger.Logger.LogError("DBClient HandleException message={Value1} exception={Value2}",
                                       message, exception.Message);

            using var context = CreateContext();
            context.SvantekErrorMessages.Add(new SvantekErrorMessageEntity
            {
                Tag = Truncate(message, 64),
                Error = Truncate(exception.Message, 512),
                ErrorTime = DateTime.UtcNow
            });
            context.SaveChanges();
        }

        public void WriteLatestTimestamp(string serialId, DateTime lastDataTime)
        {
            var normalizedLastDataTime = NormalizeSampleTimeForCurrentProvider(lastDataTime);
            using var context = CreateContext();
            var monitor = context.Monitors.FirstOrDefault(row =>
                row.SerialId == serialId &&
                row.TypeOfMonitor == NoiseMonitorDto.MONITOR_TYPE_NOISE);
            if (monitor == null)
            {
                return;
            }

            monitor.LastDataTime15Min = normalizedLastDataTime;
            context.SaveChanges();
        }

        public async Task WriteLatestTimestampAsync(
            string serialId,
            DateTime lastDataTime,
            CancellationToken cancellationToken = default)
        {
            var normalizedLastDataTime = NormalizeSampleTimeForCurrentProvider(lastDataTime);
            await using var context = CreateContext();
            var monitor = await context.Monitors.FirstOrDefaultAsync(row =>
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
                              rule.SerialId == serialNumber &&
                              !rule.IsDeleted
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
                               where deployment.MonitorId == monitorId &&
                                     deployment.EndDate == null &&
                                     (setting.Email || setting.SMS)
                               select new
                               {
                                   siteUser.UserId,
                                   siteUser.SiteId,
                                   setting.Email,
                                   setting.SMS,
                                   setting.StartTime,
                                   setting.EndTime
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
            var field = SvantekAggregateFields.Resolve(columnName);
            var normalizedStart = NormalizeSampleTimeForCurrentProvider(start);
            var normalizedEnd = NormalizeSampleTimeForCurrentProvider(end);
            var query = context.NoiseLevels
                .Where(row => row.SerialId == serialNumber)
                .Where(row => row.SampleTime > normalizedStart && row.SampleTime <= normalizedEnd);

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

        public async Task SetMonitorOfflineAsync(
            Guid monitorId,
            bool offline,
            CancellationToken cancellationToken = default)
        {
            await using var context = CreateContext();
            var monitor = await context.Monitors.FirstOrDefaultAsync(
                row => row.Id == monitorId,
                cancellationToken).ConfigureAwait(false);
            if (monitor == null)
            {
                return;
            }

            monitor.Offline = offline;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public void ClearErrorMessages(DateTime before)
        {
            using var context = CreateContext();
            var messages = context.SvantekErrorMessages
                .Where(row => row.ErrorTime < before)
                .ToList();

            context.SvantekErrorMessages.RemoveRange(messages);
            context.SaveChanges();
        }

        public SiteInfoDto ReadSiteInfo(Guid siteId)
        {
            using var context = CreateContext();
            var site = context.Sites.AsNoTracking().FirstOrDefault(row => row.Id == siteId);
            if (site == null)
            {
                throw AdapterException.Of($"No site info for site Id={siteId}");
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
                              deployment.EndDate == null &&
                              monitor.Offline == false
                        select new
                        {
                            Monitor = monitor,
                            Contract = contract,
                            Site = site
                        };

            query = Day.DayOfWeek switch
            {
                DayOfWeek.Saturday => query.Where(row => row.Site.SatStartTime != null && row.Site.SatEndTime != null),
                DayOfWeek.Sunday => query.Where(row => row.Site.SunStartTime != null && row.Site.SunEndTime != null),
                _ => query.Where(row => row.Site.StartTime != null && row.Site.EndTime != null)
            };

            return query
                .AsEnumerable()
                .Select(row =>
                {
                    var startTime = Day.DayOfWeek switch
                    {
                        DayOfWeek.Saturday => row.Site.SatStartTime!.Value,
                        DayOfWeek.Sunday => row.Site.SunStartTime!.Value,
                        _ => row.Site.StartTime!.Value
                    };
                    var endTime = Day.DayOfWeek switch
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
                })
                .ToList();
        }

        public async Task<List<SiteMonitorsWithSiteHoursDto>> ReadSiteMonitorsWithSiteHoursAsync(
            DateTime day,
            CancellationToken cancellationToken = default)
        {
            await using var context = CreateContext();

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
            return rows.Select(row =>
            {
                var startTime = day.DayOfWeek switch
                {
                    DayOfWeek.Saturday => row.Site.SatStartTime!.Value,
                    DayOfWeek.Sunday => row.Site.SunStartTime!.Value,
                    _ => row.Site.StartTime!.Value
                };
                var endTime = day.DayOfWeek switch
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
            }).ToList();
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

        public async Task WriteDailyAverageAsync(
            Guid siteId,
            Guid monitorId,
            string field,
            double level,
            DateTime timestamp,
            CancellationToken cancellationToken = default)
        {
            await using var context = CreateContext();
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

        public void Create8hourAverage(string serialId, DateTime SampleTime)
        {
            var normalizedSampleTime = NormalizeSampleTimeForCurrentProvider(SampleTime);
            using var context = CreateContext();

            var exists = context.Noise8HourAverages.Any(row =>
                row.SerialId == serialId &&
                row.SampleTime == normalizedSampleTime);
            if (exists)
            {
                return;
            }

            var rows = context.NoiseLevels
                .AsNoTracking()
                .Where(row => row.SerialId == serialId)
                .Where(row => row.SampleTime > normalizedSampleTime.AddHours(-8) && row.SampleTime <= normalizedSampleTime)
                .ToList();
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
            context.SaveChanges();
        }

        public async Task Create8hourAverageAsync(
            string serialId,
            DateTime sampleTime,
            CancellationToken cancellationToken = default)
        {
            var normalizedSampleTime = NormalizeSampleTimeForCurrentProvider(sampleTime);
            await using var context = CreateContext();

            var exists = await context.Noise8HourAverages.AnyAsync(row =>
                row.SerialId == serialId &&
                row.SampleTime == normalizedSampleTime,
                cancellationToken).ConfigureAwait(false);
            if (exists)
            {
                return;
            }

            var rows = await context.NoiseLevels
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

        public void SetMonitorBatteryStatus(Guid monitorId, byte batteryStatus)
        {
            using var context = CreateContext();
            var monitor = context.Monitors.FirstOrDefault(row => row.Id == monitorId);
            if (monitor == null)
            {
                return;
            }

            monitor.BatteryStatus = batteryStatus;
            context.SaveChanges();
        }

        public async Task SetMonitorBatteryStatusAsync(
            Guid monitorId,
            byte batteryStatus,
            CancellationToken cancellationToken = default)
        {
            await using var context = CreateContext();
            var monitor = await context.Monitors.FirstOrDefaultAsync(
                row => row.Id == monitorId,
                cancellationToken).ConfigureAwait(false);
            if (monitor == null)
            {
                return;
            }

            monitor.BatteryStatus = batteryStatus;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public List<NoiseNotificationLatest> ReadLatestNotification()
        {
            using var context = CreateContext();
            var cutoff = DateTime.Now.AddHours(-12);

            var rows = (from notification in context.Notifications.AsNoTracking()
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
                        }).ToList();

            return rows
                .Select(row => new NoiseNotificationLatest(
                    NotificationId: row.Notification.Id,
                    MonitorId: row.Monitor.Id,
                    FleetNr: row.Monitor.FleetNr ?? string.Empty,
                    SerialId: row.Monitor.SerialId,
                    ProjectId: row.Status.ProjectId ?? 0,
                    PointId: row.Status.PointId ?? 0,
                    NotificationTime: row.Notification.NotificationTime,
                    AvgPeriod: row.Notification.AveragingPeriod))
                .ToList();
        }

        public async Task<List<NoiseNotificationLatest>> ReadLatestNotificationAsync(
            CancellationToken cancellationToken = default)
        {
            await using var context = CreateContext();
            var cutoff = DateTime.Now.AddHours(-12);

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

            return rows.Select(row => new NoiseNotificationLatest(
                NotificationId: row.Notification.Id,
                MonitorId: row.Monitor.Id,
                FleetNr: row.Monitor.FleetNr ?? string.Empty,
                SerialId: row.Monitor.SerialId,
                ProjectId: row.Status.ProjectId ?? 0,
                PointId: row.Status.PointId ?? 0,
                NotificationTime: row.Notification.NotificationTime,
                AvgPeriod: row.Notification.AveragingPeriod)).ToList();
        }

        public bool WriteSoundFile(Guid notificationId, string fileName)
        {
            using var context = CreateContext();
            var notification = context.Notifications.FirstOrDefault(row => row.Id == notificationId);
            if (notification == null)
            {
                return true;
            }

            context.Entry(notification).Property("RecordingLink").CurrentValue = fileName;
            context.SaveChanges();
            return true;
        }

        public async Task<bool> WriteSoundFileAsync(
            Guid notificationId,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            await using var context = CreateContext();
            var notification = await context.Notifications.FirstOrDefaultAsync(
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
            var monitorOptions = SvantekMonitorDbOptions.Current;
            var options = MonitorDbContextOptionsFactory.CreateOptions<SvantekMonitorContext>(ConnectionString, monitorOptions);
            return new SvantekMonitorContext(options, monitorOptions);
        }

        private static List<NoiseDto> ToNoiseDtos(DataTable table) =>
            table.Rows
                .Cast<DataRow>()
                .Select(row => new NoiseDto
                {
                    SerialId = Convert.ToString(row["SerialId"]) ?? string.Empty,
                    SampleTime = Convert.ToDateTime(row["SampleTime"]),
                    LAeq = Convert.ToDouble(row["LAeq"]),
                    LAmax = Convert.ToDouble(row["LAmax"]),
                    LA90 = Convert.ToDouble(row["LA90"]),
                    LA10 = Convert.ToDouble(row["LA10"]),
                    LCeq = Convert.ToDouble(row["LCeq"]),
                    LCmax = Convert.ToDouble(row["LCmax"]),
                    LC90 = Convert.ToDouble(row["LC90"]),
                    LC10 = Convert.ToDouble(row["LC10"])
                })
                .ToList();

        private static void InsertNoiseDtos(SvantekMonitorContext context, IEnumerable<NoiseDto> dtos)
        {
            var seen = new HashSet<(string SerialId, DateTime SampleTime)>();

            foreach (var dto in dtos)
            {
                var sampleTime = NormalizeSampleTimeForCurrentProvider(dto.SampleTime);
                if (!seen.Add((dto.SerialId, sampleTime)))
                {
                    continue;
                }

                var exists = context.NoiseLevels.Any(row =>
                    row.SerialId == dto.SerialId &&
                    row.SampleTime == sampleTime);
                if (exists)
                {
                    continue;
                }

                var entity = SvantekDbMapper.ToNoiseLevelEntity(dto.SerialId, dto);
                entity.SampleTime = sampleTime;
                context.NoiseLevels.Add(entity);
            }
        }

        private static async Task InsertNoiseDtosAsync(
            SvantekMonitorContext context,
            IEnumerable<NoiseDto> dtos,
            CancellationToken cancellationToken)
        {
            var seen = new HashSet<(string SerialId, DateTime SampleTime)>();

            foreach (var dto in dtos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sampleTime = NormalizeSampleTimeForCurrentProvider(dto.SampleTime);
                if (!seen.Add((dto.SerialId, sampleTime)))
                {
                    continue;
                }

                var exists = await context.NoiseLevels.AnyAsync(row =>
                    row.SerialId == dto.SerialId &&
                    row.SampleTime == sampleTime,
                    cancellationToken).ConfigureAwait(false);
                if (exists)
                {
                    continue;
                }

                var entity = SvantekDbMapper.ToNoiseLevelEntity(dto.SerialId, dto);
                entity.SampleTime = sampleTime;
                context.NoiseLevels.Add(entity);
            }
        }

        private static DateTime NormalizeSampleTimeForCurrentProvider(DateTime sampleTime)
        {
            return SvantekMonitorDbOptions.Current.IsPostgreSql
                ? SvantekDbMapper.NormalizeSampleTimeForPostgreSql(sampleTime)
                : sampleTime;
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

            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
