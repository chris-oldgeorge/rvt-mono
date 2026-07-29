using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using Omnidots.Api.Db.EntityFramework;
using Omnidots.Api.Db.Mapping;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Data.Queries;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using AlertActivityTimeDto = Rvt.Monitor.Common.Notifications.AlertActivityTimeDto;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Notifications.RvtContactDto;
namespace Omnidots.Api.Db;

// Summary: EF Core-backed Omnidots database client that preserves the IDBClient contract.
// Major updates:
// - 2026-06-20 EF migration: replaced DBUtil SQL calls with provider-aware EF Core operations.
public class DBClient :
    IDBClient,
    IOmnidotsImportCursorQueries,
    IOmnidotsMeasurementImportCommands,
    IOmnidotsTraceQueries
{
    private readonly string _connectionString;
    private readonly Action<OmnidotsMeasurementSeries, int>? _beforeImportSave;

    public DBClient(string connectionString)
        : this(connectionString, null)
    {
    }

    internal DBClient(
        string connectionString,
        Action<OmnidotsMeasurementSeries, int>? beforeImportSave)
    {
        MonitorDb.ValidateLegacyProvider(
            Environment.GetEnvironmentVariable("RVT__DATABASE_PROVIDER"),
            Environment.GetEnvironmentVariable("DatabaseProvider"));
        _connectionString = connectionString;
        _beforeImportSave = beforeImportSave;
    }

    public void WriteMonitorList(List<VibrationMonitorDto> monitors)
    {
        using OmnidotsMonitorContext context = CreateContext();

        foreach (VibrationMonitorDto dto in monitors)
        {
            MonitorEntity? monitor = context.Monitors.FirstOrDefault(row =>
                row.SerialId == dto.SerialId &&
                row.TypeOfMonitor == VibrationMonitorDto.MONITOR_TYPE_VIBRATION);

            if (monitor == null)
            {
                context.Monitors.Add(OmnidotsDbMapper.ToMonitorEntity(dto));
            }
            else
            {
                OmnidotsDbMapper.UpdateMonitorEntity(monitor, dto);
            }

            context.SaveChanges();
            UpsertMonitorStatus(context, dto.MonitorStatus);
            if (dto.Sensor != null)
            {
                UpsertMonitorSensor(context, dto.Sensor);
            }

            context.SaveChanges();
        }
    }

    public void SetMonitorOffline(Guid monitorId, bool offline)
    {
        using OmnidotsMonitorContext context = CreateContext();
        MonitorEntity? monitor = context.Monitors.FirstOrDefault(row => row.Id == monitorId);
        if (monitor == null)
        {
            return;
        }

        monitor.Offline = offline;
        context.SaveChanges();
    }

    public void SetMonitorBatteryStatus(Guid monitorId, byte batteryStatus)
    {
        using OmnidotsMonitorContext context = CreateContext();
        MonitorEntity? monitor = context.Monitors.FirstOrDefault(row => row.Id == monitorId);
        if (monitor == null)
        {
            return;
        }

        monitor.BatteryStatus = batteryStatus;
        context.SaveChanges();
    }

    public List<VibrationMonitorDto> ReadMonitorList(DateTime? lastDataTime)
    {
        using OmnidotsMonitorContext context = CreateContext();

        var rows = (from monitor in context.Monitors.AsNoTracking()
                    join deployment in context.Deployments.AsNoTracking() on monitor.Id equals deployment.MonitorId
                    join sensor in context.Sensors.AsNoTracking() on monitor.SerialId equals sensor.SerialId
                    where monitor.FleetNr != null &&
                          monitor.TypeOfMonitor == VibrationMonitorDto.MONITOR_TYPE_VIBRATION &&
                          deployment.EndDate == null
                    select new
                    {
                        Monitor = monitor,
                        Sensor = sensor,
                        DeployDate = (DateTime?)deployment.StartDate
                    }).ToList();

        Dictionary<string, OmnidotsMonitorStatusEntity> statuses = context.MonitorStatuses
            .AsNoTracking()
            .Where(status => rows.Select(row => row.Monitor.SerialId).Contains(status.SerialId))
            .ToDictionary(status => status.SerialId, StringComparer.OrdinalIgnoreCase);

        return [.. rows
            .Select(row => OmnidotsDbMapper.ToVibrationMonitorDto(
                row.Monitor,
                statuses[row.Monitor.SerialId],
                row.Sensor,
                row.Sensor.Lastseen,
                row.DeployDate))];
    }

    public IReadOnlyDictionary<string, DateTime> ReadLatestTraceEndTimes(
        IReadOnlyCollection<string> serialIds)
    {
        string[] requestedSerialIds = [.. serialIds
            .Where(serialId => !string.IsNullOrWhiteSpace(serialId))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        if (requestedSerialIds.Length == 0)
        {
            return new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }

        using OmnidotsMonitorContext context = CreateContext();
        return context.TraceIndexes
            .AsNoTracking()
            .Where(trace => trace.SerialId != null && requestedSerialIds.Contains(trace.SerialId))
            .GroupBy(trace => trace.SerialId!)
            .Select(group => new { SerialId = group.Key, EndTime = group.Max(trace => trace.EndTime) })
            .ToDictionary(trace => trace.SerialId, trace => trace.EndTime, StringComparer.OrdinalIgnoreCase);
    }

    public VibrationMonitorDto ReadMonitor(string serialId)
    {
        using OmnidotsMonitorContext context = CreateContext();

        MonitorEntity? monitor = context.Monitors
            .AsNoTracking()
            .FirstOrDefault(row =>
                row.SerialId == serialId &&
                row.TypeOfMonitor == VibrationMonitorDto.MONITOR_TYPE_VIBRATION) ?? throw AdapterException.Of($"No monitor with SerialId='{serialId}'");

        OmnidotsMonitorStatusEntity? status = context.MonitorStatuses
            .AsNoTracking()
            .FirstOrDefault(row => row.SerialId == serialId) ?? throw AdapterException.Of($"Missing VibrationMonitorStatus for serialId={serialId}");
        OmnidotsSensorEntity? sensor = context.Sensors
            .AsNoTracking()
            .FirstOrDefault(row => row.SerialId == serialId);
        return OmnidotsDbMapper.ToVibrationMonitorDto(monitor, status, sensor, lastSeen: null, deployDate: null);
    }

    public DateTime ReadDeployStartDate(Guid monitorId)
    {
        using OmnidotsMonitorContext context = CreateContext();
        DateTime? startDate = context.Deployments
            .AsNoTracking()
            .Where(row => row.MonitorId == monitorId && row.EndDate == null)
            .Select(row => (DateTime?)row.StartDate)
            .FirstOrDefault();

        return startDate ?? throw AdapterException.Of($"No deployment for monitor='{monitorId}'");
    }

    public void HandleException(string message, Exception exception)
    {
        RvtLogger.Logger.LogError("DBClient HandleException message={Value1} exception={Value2}", message, exception.Message);

        using OmnidotsMonitorContext context = CreateContext();
        string error = exception.ToString();
        if (error.Length > 1024)
        {
            error = error[..1024];
        }

        context.OmnidotsErrorMessages.Add(new OmnidotsErrorMessageEntity
        {
            Tag = message.Length > 64 ? message[..64] : message,
            Error = error,
            ErrorTime = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    public void WriteLatestTimestamp(string serialId, DateTime lastDataTime)
    {
        if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug)) { RvtLogger.Logger.LogDebug("WriteLatestTimestamp for serialId={Value1} lastDataTime={Value2}", serialId, lastDataTime); }

        using OmnidotsMonitorContext context = CreateContext();
        MonitorEntity? monitor = context.Monitors.FirstOrDefault(row =>
            row.SerialId == serialId &&
            row.TypeOfMonitor == VibrationMonitorDto.MONITOR_TYPE_VIBRATION);
        if (monitor == null)
        {
            return;
        }

        monitor.LastDataTime1Min = lastDataTime;
        context.SaveChanges();
    }

    public void InsertPeakRecords(string serialId, List<PeakRecordDto> dtos)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        DataTable table = CreatePeakRecordsTable(serialId, dtos);
        ImportPeakRecords(serialId, table, dtos.Max(dto => dto.SampleTime));
    }

    public void InsertPeakRecordsTable(DataTable table)
    {
        if (table.Rows.Count == 0)
        {
            return;
        }

        foreach (IGrouping<string, DataRow> serialGroup in table.Rows
                     .Cast<DataRow>()
                     .GroupBy(row => RequiredString(row, "SerialId"), StringComparer.Ordinal))
        {
            DataTable serialTable = table.Clone();
            foreach (DataRow? row in serialGroup)
            {
                serialTable.ImportRow(row);
            }

            DateTime newestSampleAt = serialGroup.Max(row => RequiredDateTime(row, "SampleTime"));
            ImportPeakRecords(serialGroup.Key, serialTable, newestSampleAt);
        }
    }

    public void InsertVeffRecords(string serialId, List<VeffRecordDto> dtos)
    {
        if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug)) { RvtLogger.Logger.LogDebug("Inserting VEFF Records serialId={Value1} number of records={Value2}", serialId, dtos.Count); }

        if (dtos.Count == 0)
        {
            return;
        }

        ImportVeffRecords(serialId, dtos, dtos.Max(dto => dto.SampleTime));
    }

    public void InsertVdvRecords(string serialId, List<VdvRecordDto> dtos)
    {
        if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug)) { RvtLogger.Logger.LogDebug("Inserting VDV Records serialId={Value1} number of records={Value2}", serialId, dtos.Count); }

        if (dtos.Count == 0)
        {
            return;
        }

        ImportVdvRecords(serialId, dtos, dtos.Max(dto => dto.SampleTime));
    }

    public DateTime? ReadImportCursor(string serialId, OmnidotsMeasurementSeries series)
    {
        using OmnidotsMonitorContext context = CreateContext();
        string seriesName = SeriesName(series);
        return context.ImportCursors
            .AsNoTracking()
            .Where(row => row.SerialId == serialId && row.Series == seriesName)
            .Select(row => (DateTime?)row.LastSampleAt)
            .FirstOrDefault();
    }

    public DateTime? ReadLatestMeasurementTime(string serialId, OmnidotsMeasurementSeries series)
    {
        using OmnidotsMonitorContext context = CreateContext();
        DateTime? latest = series switch
        {
            OmnidotsMeasurementSeries.Peak => context.PeakLevels
                .AsNoTracking()
                .Where(row => row.SerialId == serialId)
                .Select(row => (DateTime?)row.SampleTime)
                .Max(),
            OmnidotsMeasurementSeries.Veff => context.VeffLevels
                .AsNoTracking()
                .Where(row => row.SerialId == serialId)
                .Select(row => (DateTime?)row.SampleTime)
                .Max(),
            OmnidotsMeasurementSeries.Vdv => context.VdvLevels
                .AsNoTracking()
                .Where(row => row.SerialId == serialId)
                .Select(row => (DateTime?)row.SampleTime)
                .Max(),
            _ => throw new ArgumentOutOfRangeException(nameof(series), series, "Unsupported Omnidots measurement series.")
        };
        return NormalizeLatestMeasurementTime(latest);
    }

    public void ImportPeakRecords(string serialId, DataTable records, DateTime newestSampleAt)
    {
        if (records.Rows.Count == 0)
        {
            return;
        }

        DateTime normalizedNewestSampleAt = ValidatePeakImport(serialId, records, newestSampleAt);
        ExecuteImportWithRetry(OmnidotsMeasurementSeries.Peak, context =>
        {
            HashSet<DateTime> seen = [];
            foreach (DataRow row in records.Rows)
            {
                OmnidotsPeakLevelEntity entity = ToPeakLevelEntity(row);
                entity.SampleTime = NormalizeUtc(entity.SampleTime);
                if (!seen.Add(entity.SampleTime) ||
                    context.PeakLevels.Any(existing =>
                        existing.SerialId == serialId &&
                        existing.SampleTime == entity.SampleTime))
                {
                    continue;
                }

                context.PeakLevels.Add(entity);
            }

            bool cursorAdvanced = AdvanceCursor(
                context,
                serialId,
                OmnidotsMeasurementSeries.Peak,
                normalizedNewestSampleAt);

            if (cursorAdvanced)
            {
                MonitorEntity? monitor = context.Monitors.FirstOrDefault(row =>
                    row.SerialId == serialId &&
                    row.TypeOfMonitor == VibrationMonitorDto.MONITOR_TYPE_VIBRATION);
                if (monitor != null &&
                    (monitor.LastDataTime1Min == null ||
                     normalizedNewestSampleAt > NormalizeUtc(monitor.LastDataTime1Min.Value)))
                {
                    monitor.LastDataTime1Min = normalizedNewestSampleAt;
                }
            }
        });
    }

    public void ImportVeffRecords(
        string serialId,
        IReadOnlyCollection<VeffRecordDto> records,
        DateTime newestSampleAt)
    {
        if (records.Count == 0)
        {
            return;
        }

        DateTime normalizedNewestSampleAt = ValidateNewestSampleAt(
            records.Max(dto => NormalizeUtc(dto.SampleTime)),
            newestSampleAt);
        ExecuteImportWithRetry(OmnidotsMeasurementSeries.Veff, context =>
        {
            HashSet<DateTime> seen = [];
            foreach (VeffRecordDto dto in records)
            {
                DateTime sampleTime = NormalizeUtc(dto.SampleTime);
                if (!seen.Add(sampleTime) ||
                    context.VeffLevels.Any(row => row.SerialId == serialId && row.SampleTime == sampleTime))
                {
                    continue;
                }

                OmnidotsVeffLevelEntity entity = OmnidotsDbMapper.ToVeffLevelEntity(serialId, dto);
                entity.SampleTime = sampleTime;
                context.VeffLevels.Add(entity);
            }

            AdvanceCursor(context, serialId, OmnidotsMeasurementSeries.Veff, normalizedNewestSampleAt);
        });
    }

    public void ImportVdvRecords(
        string serialId,
        IReadOnlyCollection<VdvRecordDto> records,
        DateTime newestSampleAt)
    {
        if (records.Count == 0)
        {
            return;
        }

        DateTime normalizedNewestSampleAt = ValidateNewestSampleAt(
            records.Max(dto => NormalizeUtc(dto.SampleTime)),
            newestSampleAt);
        ExecuteImportWithRetry(OmnidotsMeasurementSeries.Vdv, context =>
        {
            HashSet<DateTime> seen = [];
            foreach (VdvRecordDto dto in records)
            {
                DateTime sampleTime = NormalizeUtc(dto.SampleTime);
                if (!seen.Add(sampleTime) ||
                    context.VdvLevels.Any(row => row.SerialId == serialId && row.SampleTime == sampleTime))
                {
                    continue;
                }

                OmnidotsVdvLevelEntity entity = OmnidotsDbMapper.ToVdvLevelEntity(serialId, dto);
                entity.SampleTime = sampleTime;
                context.VdvLevels.Add(entity);
            }

            AdvanceCursor(context, serialId, OmnidotsMeasurementSeries.Vdv, normalizedNewestSampleAt);
        });
    }

    public List<RvtAlertRuleDto> ReadRules(string? serialId)
    {
        using OmnidotsMonitorContext context = CreateContext();

        IQueryable<RvtAlertRuleEntity> query;
        if (serialId == null)
        {
            query = context.AlertRules.AsNoTracking().Where(row => row.SerialId == null);
        }
        else
        {
            query = from rule in context.AlertRules.AsNoTracking()
                    join monitor in context.Monitors.AsNoTracking() on rule.MonitorId equals monitor.Id
                    where monitor.TypeOfMonitor == VibrationMonitorDto.MONITOR_TYPE_VIBRATION &&
                          rule.SerialId == serialId
                    select rule;
        }

        return [.. query
            .AsEnumerable()
            .Select(rule => ToRuleDto(rule, serialId))];
    }

    public List<RvtContactDto> ReadAlertContacts(Guid monitorId)
    {
        using OmnidotsMonitorContext context = CreateContext();

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
                               setting.Email,
                               setting.SMS,
                               setting.StartTime,
                               setting.EndTime
                           }).ToList();

        HashSet<string> userIds = contactRows
            .Select(row => row.UserId.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, AspNetUserEntity> usersById = context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionary(user => user.Id, StringComparer.OrdinalIgnoreCase);

        return [.. contactRows
            .Where(row => usersById.ContainsKey(row.UserId.ToString()))
            .Select(row =>
            {
                AspNetUserEntity user = usersById[row.UserId.ToString()];
                return new RvtContactDto(
                    useEmail: row.Email,
                    useSms: row.SMS,
                    emailAddress: user.Email,
                    phoneNumber: user.PhoneNumber,
                    sendStartTime: row.StartTime,
                    sendEndTime: row.EndTime);
            })];
    }

    public void WriteNotification(NotificationDto dto)
    {
        using OmnidotsMonitorContext context = CreateContext();
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

    public List<NotificationDto> ReadNotifications(Guid monitorId, DateTime after)
    {
        using OmnidotsMonitorContext context = CreateContext();

        return [.. context.Notifications
            .AsNoTracking()
            .Where(row => row.MonitorId == monitorId && row.NotificationTime >= after)
            .AsEnumerable()
            .Select(row => ToNotificationDto(row))];
    }

    public void UpdateAlertRule(RvtAlertRuleDto dto)
    {
        using OmnidotsMonitorContext context = CreateContext();
        RvtAlertRuleEntity? rule = context.AlertRules.FirstOrDefault(row => row.Id == dto.RuleId);
        if (rule == null)
        {
            return;
        }

        rule.IsActive = dto.IsActive;
        rule.Accessed = dto.Accessed;
        context.SaveChanges();
    }

    public double GetAveragePeakLevels(string serialId, string columnName, DateTime start, DateTime end)
    {
        using OmnidotsMonitorContext context = CreateContext();
        MonitorAggregateField<OmnidotsPeakLevelEntity> field = OmnidotsAggregateFields.Resolve(columnName);
        IQueryable<OmnidotsPeakLevelEntity> query = context.PeakLevels
            .Where(row => row.SerialId == serialId)
            .Where(row => row.SampleTime >= start && row.SampleTime < end);

        return query.Average(field.Selector) ?? -1.0;
    }

    public void WriteNotificationAudit(Guid notificationId, string address, string message)
    {
        using OmnidotsMonitorContext context = CreateContext();
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

    public void WriteTraces(string serialId, IReadOnlyList<TraceData> traces)
    {
        foreach (TraceData traceData in traces)
        {
            using OmnidotsMonitorContext context = CreateContext();
            using IDbContextTransaction transaction = context.Database.BeginTransaction();
            Guid traceId = Guid.NewGuid();
            DateTime startTime = DateTimeUtil.FromMillis(traceData.StartTime);
            DateTime endTime = DateTimeUtil.FromMillis(traceData.EndTime);
            try
            {
                context.TraceIndexes.Add(new OmnidotsTraceIndexEntity
                {
                    Id = traceId,
                    SerialId = serialId,
                    StartTime = startTime,
                    EndTime = endTime
                });

                int xCount = traceData.X?.Count ?? 0;
                int yCount = traceData.Y?.Count ?? 0;
                int zCount = traceData.Z?.Count ?? 0;
                int sampleCount = Math.Max(xCount, Math.Max(yCount, zCount));
                List<OmnidotsTraceEntity> samples = new(sampleCount);

                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    samples.Add(new OmnidotsTraceEntity
                    {
                        TraceId = traceId,
                        SampleIndex = sampleIndex,
                        X = traceData.X != null && sampleIndex < traceData.X.Count
                            ? traceData.X[sampleIndex]
                            : null,
                        Y = traceData.Y != null && sampleIndex < traceData.Y.Count
                            ? traceData.Y[sampleIndex]
                            : null,
                        Z = traceData.Z != null && sampleIndex < traceData.Z.Count
                            ? traceData.Z[sampleIndex]
                            : null
                    });
                }

                context.Traces.AddRange(samples);
                context.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    public void ClearErrorMessages(DateTime before)
    {
        using OmnidotsMonitorContext context = CreateContext();
        List<OmnidotsErrorMessageEntity> messages = [.. context.OmnidotsErrorMessages.Where(row => row.ErrorTime < before)];

        context.OmnidotsErrorMessages.RemoveRange(messages);
        context.SaveChanges();
    }

    public SiteTimes ReadSiteTimes(Guid monitorId)
    {
        using OmnidotsMonitorContext context = CreateContext();

        Guid? siteId = (from monitor in context.Monitors.AsNoTracking()
                        join deployment in context.Deployments.AsNoTracking() on monitor.Id equals deployment.MonitorId
                        join contract in context.Contracts.AsNoTracking() on deployment.ContractId equals contract.Id
                        where monitor.Id == monitorId &&
                              deployment.EndDate == null &&
                              contract.SiteId != null
                        select contract.SiteId).FirstOrDefault();

        if (siteId == null)
        {
            return new SiteTimes();
        }

        SiteEntity? site = context.Sites.AsNoTracking().FirstOrDefault(row => row.Id == siteId) ?? throw AdapterException.Of($"ReadSiteActivityTimeBySiteId No site times for siteId = {siteId}");
        return new SiteTimes
        {
            WeekdayStart = site.StartTime,
            WeekdayEnd = site.EndTime,
            SaturdayStart = site.SatStartTime,
            SaturdayEnd = site.SatEndTime,
            SundayStart = site.SunStartTime,
            SundayEnd = site.SunEndTime
        };
    }

    private OmnidotsMonitorContext CreateContext()
    {
        MonitorDbOptions monitorOptions = OmnidotsMonitorDbOptions.Current;
        DbContextOptions<OmnidotsMonitorContext> options = MonitorDbContextOptionsFactory.CreateOptions<OmnidotsMonitorContext>(_connectionString);
        return new OmnidotsMonitorContext(options, monitorOptions);
    }

    private static void UpsertMonitorStatus(OmnidotsMonitorContext context, VibrationMonitorStatusDto dto)
    {
        OmnidotsMonitorStatusEntity? entity = context.MonitorStatuses.FirstOrDefault(row => row.SerialId == dto.SerialId);
        if (entity == null)
        {
            entity = new OmnidotsMonitorStatusEntity
            {
                Id = Guid.NewGuid(),
                SerialId = dto.SerialId
            };
            context.MonitorStatuses.Add(entity);
        }

        OmnidotsDbMapper.UpdateMonitorStatusEntity(entity, dto);
    }

    private static void UpsertMonitorSensor(OmnidotsMonitorContext context, SensorDto dto)
    {
        OmnidotsSensorEntity? entity = context.Sensors.FirstOrDefault(row => row.SerialId == dto.SerialId);
        if (entity == null)
        {
            entity = new OmnidotsSensorEntity
            {
                Id = Guid.NewGuid(),
                SerialId = dto.SerialId
            };
            context.Sensors.Add(entity);
        }

        OmnidotsDbMapper.UpdateSensorEntity(entity, dto);
    }

    private static VibrationMonitorStatusDto ReadMonitorStatus(OmnidotsMonitorContext context, string serialId)
    {
        OmnidotsMonitorStatusEntity? entity = context.MonitorStatuses.AsNoTracking().FirstOrDefault(row => row.SerialId == serialId) ?? throw AdapterException.Of($"Missing VibrationMonitorStatus for serialId={serialId}");
        return OmnidotsDbMapper.ToStatusDto(entity);
    }

    private static SensorDto? ReadMonitorSensor(OmnidotsMonitorContext context, string serialId)
    {
        OmnidotsSensorEntity? entity = context.Sensors.AsNoTracking().FirstOrDefault(row => row.SerialId == serialId);
        return entity == null ? null : OmnidotsDbMapper.ToSensorDto(entity);
    }

    private void ExecuteImportWithRetry(
        OmnidotsMeasurementSeries series,
        Action<OmnidotsMonitorContext> stageAttempt)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using OmnidotsMonitorContext context = CreateContext();
            using IDbContextTransaction transaction = context.Database.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                stageAttempt(context);
                _beforeImportSave?.Invoke(series, attempt);
                context.SaveChanges();
                transaction.Commit();
                return;
            }
            catch (Exception exception)
            {
                transaction.Rollback();
                if (attempt == maxAttempts || !IsRetryableImportConflict(exception))
                {
                    throw;
                }
            }
        }
    }

    private static bool IsRetryableImportConflict(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is PostgresException postgresException &&
                IsRetryablePostgreSqlState(postgresException.SqlState))
            {
                return true;
            }

        }

        return false;
    }

    internal static bool IsRetryablePostgreSqlState(string sqlState) =>
        sqlState is "40001" or "40P01" or "23505";

    private static DateTime ValidatePeakImport(
        string serialId,
        DataTable records,
        DateTime newestSampleAt)
    {
        foreach (DataRow row in records.Rows)
        {
            if (!string.Equals(RequiredString(row, "SerialId"), serialId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Every Peak record serial must match the requested serial.",
                    nameof(records));
            }
        }

        DateTime batchMaximum = records.Rows
            .Cast<DataRow>()
            .Max(row => NormalizeUtc(RequiredDateTime(row, "SampleTime")));
        return ValidateNewestSampleAt(batchMaximum, newestSampleAt);
    }

    private static DateTime ValidateNewestSampleAt(DateTime batchMaximum, DateTime newestSampleAt)
    {
        DateTime normalizedNewestSampleAt = NormalizeUtc(newestSampleAt);
        if (normalizedNewestSampleAt != batchMaximum)
        {
            throw new ArgumentException(
                "The newest sample timestamp must match the batch maximum.",
                nameof(newestSampleAt));
        }

        return normalizedNewestSampleAt;
    }

    private static bool AdvanceCursor(
        OmnidotsMonitorContext context,
        string serialId,
        OmnidotsMeasurementSeries series,
        DateTime newestSampleAt)
    {
        string seriesName = SeriesName(series);
        DateTime normalizedNewestSampleAt = NormalizeUtc(newestSampleAt);
        OmnidotsImportCursorEntity? cursor = context.ImportCursors.FirstOrDefault(row =>
            row.SerialId == serialId && row.Series == seriesName);
        if (cursor == null)
        {
            context.ImportCursors.Add(new OmnidotsImportCursorEntity
            {
                SerialId = serialId,
                Series = seriesName,
                LastSampleAt = normalizedNewestSampleAt,
                UpdatedAt = DateTime.UtcNow
            });
            return true;
        }

        if (normalizedNewestSampleAt <= cursor.LastSampleAt)
        {
            return false;
        }

        cursor.LastSampleAt = normalizedNewestSampleAt;
        cursor.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    private static string SeriesName(OmnidotsMeasurementSeries series) => series switch
    {
        OmnidotsMeasurementSeries.Peak => "Peak",
        OmnidotsMeasurementSeries.Veff => "Veff",
        OmnidotsMeasurementSeries.Vdv => "Vdv",
        _ => throw new ArgumentOutOfRangeException(nameof(series), series, "Unsupported Omnidots measurement series.")
    };

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    internal static DateTime? NormalizeLatestMeasurementTime(DateTime? value) =>
        value == null ? null : NormalizeUtc(value.Value);

    private static DataTable CreatePeakRecordsTable(string serialId, IEnumerable<PeakRecordDto> dtos)
    {
        DataTable table = new("Results");
        table.Columns.Add("SerialId", typeof(string));
        table.Columns.Add("SampleTime", typeof(DateTime));
        foreach (string? columnName in new[]
                 {
                     "XFdom", "XVtop", "XVtopOverflow",
                     "YFdom", "YVtop", "YVtopOverflow",
                     "ZFdom", "ZVtop", "ZVtopOverflow"
                 })
        {
            table.Columns.Add(columnName, typeof(double)).AllowDBNull = true;
        }

        foreach (PeakRecordDto dto in dtos)
        {
            DataRow row = table.NewRow();
            row["SerialId"] = serialId;
            row["SampleTime"] = dto.SampleTime;
            SetNullableDouble(row, "XFdom", dto.X?.Fdom);
            SetNullableDouble(row, "XVtop", dto.X?.Vtop);
            SetNullableDouble(row, "XVtopOverflow", dto.X?.VtopOverflow);
            SetNullableDouble(row, "YFdom", dto.Y?.Fdom);
            SetNullableDouble(row, "YVtop", dto.Y?.Vtop);
            SetNullableDouble(row, "YVtopOverflow", dto.Y?.VtopOverflow);
            SetNullableDouble(row, "ZFdom", dto.Z?.Fdom);
            SetNullableDouble(row, "ZVtop", dto.Z?.Vtop);
            SetNullableDouble(row, "ZVtopOverflow", dto.Z?.VtopOverflow);
            table.Rows.Add(row);
        }

        return table;
    }

    private static void SetNullableDouble(DataRow row, string columnName, double? value)
    {
        row[columnName] = value.HasValue ? value.Value : DBNull.Value;
    }

    private static OmnidotsPeakLevelEntity ToPeakLevelEntity(DataRow row)
    {
        return new OmnidotsPeakLevelEntity
        {
            SerialId = RequiredString(row, "SerialId"),
            SampleTime = RequiredDateTime(row, "SampleTime"),
            XFdom = NullableDouble(row, "XFdom"),
            XVtop = NullableDouble(row, "XVtop"),
            XVtopOverflow = NullableDouble(row, "XVtopOverflow"),
            YFdom = NullableDouble(row, "YFdom"),
            YVtop = NullableDouble(row, "YVtop"),
            YVtopOverflow = NullableDouble(row, "YVtopOverflow"),
            ZFdom = NullableDouble(row, "ZFdom"),
            ZVtop = NullableDouble(row, "ZVtop"),
            ZVtopOverflow = NullableDouble(row, "ZVtopOverflow")
        };
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

    private static NotificationDto ToNotificationDto(NotificationEntity row)
    {
        return new NotificationDto(
            id: row.Id,
            notificationTime: row.NotificationTime,
            limitOn: row.LimitOn,
            averagingPeriod: row.AveragingPeriod,
            level: row.Level,
            closedTime: row.ClosedTime,
            closedByUser: row.ClosedByUser,
            alertType: (AlertType)row.AlertType,
            alertField: row.AlertField,
            monitorId: row.MonitorId);
    }

    private static string RequiredString(DataRow row, string columnName)
    {
        object value = row[columnName];
        return value == DBNull.Value ? string.Empty : Convert.ToString(value) ?? string.Empty;
    }

    private static DateTime RequiredDateTime(DataRow row, string columnName)
    {
        return Convert.ToDateTime(row[columnName]);
    }

    private static double? NullableDouble(DataRow row, string columnName)
    {
        object value = row[columnName];
        return value == DBNull.Value ? null : Convert.ToDouble(value);
    }
}
