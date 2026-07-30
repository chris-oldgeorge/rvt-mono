using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MyAtm.Api.Db.EntityFramework;
using MyAtm.Api.Db.Mapping;
using MyAtm.Api.Rules;
using MyAtm.Delivery;
using MyAtm.Model;
using MyAtm.Model.Config;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Data.Queries;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace MyAtm.Api.Db
{

    // Summary: EF Core-backed MyAtm database client that preserves the IDBClient contract.
    // Major updates:
    // - 2026-06-20 EF migration: replaced DBUtil SQL calls with provider-aware EF Core operations.
    public class DBClient : IDBClient
    {

        private static readonly TimeSpan _alertSuppressionWindow = TimeSpan.FromMinutes(30);
        private static readonly RuleAlertDeliveryPlanner _deliveryPlanner = new();

        private readonly string _connectionString;

        public DBClient(string connectionString)
        {
            MonitorDb.ValidateLegacyProvider(
                Environment.GetEnvironmentVariable("RVT__DATABASE_PROVIDER"),
                Environment.GetEnvironmentVariable("DatabaseProvider"));
            _connectionString = connectionString;
        }

        public List<DustMonitorDto> ReadMonitorList(DateTime? lastDataTime)
        {
            using MyAtmMonitorContext context = CreateContext();

            return ToDustMonitorList(context.Monitors
                .AsNoTracking()
                .Where(row => row.TypeOfMonitor == DustMonitorDto.MONITOR_TYPE_DUST)
                .Where(row => row.FleetNr != null));
        }

        public List<DustMonitorDto> ReadMonitorList(int customerId, DateTime? lastDataTime)
        {
            using MyAtmMonitorContext context = CreateContext();

            return ToDustMonitorList(context.Monitors
                .AsNoTracking()
                .Where(row => row.TypeOfMonitor == DustMonitorDto.MONITOR_TYPE_DUST)
                .Where(row => row.FleetNr != null)
                .Where(row => row.CustomerId == customerId));
        }

        public DustMonitorDto? ReadMonitor(string serialId)
        {
            using MyAtmMonitorContext context = CreateContext();

            MonitorEntity? monitor = context.Monitors
                .AsNoTracking()
                .Where(row => row.TypeOfMonitor == DustMonitorDto.MONITOR_TYPE_DUST)
                .FirstOrDefault(row => row.SerialId == serialId);

            return monitor == null ? null : MyAtmDbMapper.ToDustMonitorDto(monitor);
        }

        public DustMonitorDto? ReadMonitor(int customerId, string serialId)
        {
            using MyAtmMonitorContext context = CreateContext();

            MonitorEntity? monitor = context.Monitors
                .AsNoTracking()
                .Where(row => row.TypeOfMonitor == DustMonitorDto.MONITOR_TYPE_DUST)
                .Where(row => row.CustomerId == customerId)
                .FirstOrDefault(row => row.SerialId == serialId);

            return monitor == null ? null : MyAtmDbMapper.ToDustMonitorDto(monitor);
        }

        private static List<DustMonitorDto> ToDustMonitorList(IQueryable<MonitorEntity> query) =>
            [.. query.Select(row => MyAtmDbMapper.ToDustMonitorDto(row))];

        public List<RvtAlertRuleDto> ReadRules(string? serialId)
        {
            using MyAtmMonitorContext context = CreateContext();

            IQueryable<RvtAlertRuleEntity> query;
            if (serialId == null)
            {
                query = context.AlertRules.AsNoTracking().Where(row => row.SerialId == null && !row.IsDeleted);
            }
            else
            {
                // Deleted rules are excluded at the query, as the site-rule
                // branch above and Svantek's twin already do. The fleet-wide
                // ReadRules(Period) below deliberately still returns them:
                // ProcessDustLevels uses it to deactivate a deleted rule that
                // is still latched.
                query = from rule in context.AlertRules.AsNoTracking()
                        join monitor in context.Monitors.AsNoTracking() on rule.MonitorId equals monitor.Id
                        where monitor.TypeOfMonitor == DustMonitorDto.MONITOR_TYPE_DUST &&
                              rule.SerialId == serialId &&
                              !rule.IsDeleted
                        select rule;
            }

            return [.. query
                .AsEnumerable()
                .Select(rule => ToRuleDto(rule, serialId))];
        }

        public List<RvtAlertRuleDto> ReadRules(string? serialId, Period period)
        {
            if (serialId == null)
            {
                return ReadRules(period);
            }

            int periodSeconds = DustMonitorDto.PeriodToSeconds(period);
            using MyAtmMonitorContext context = CreateContext();

            IQueryable<RvtAlertRuleEntity> query = from rule in context.AlertRules.AsNoTracking()
                                                   join monitor in context.Monitors.AsNoTracking() on rule.MonitorId equals monitor.Id
                                                   where monitor.TypeOfMonitor == DustMonitorDto.MONITOR_TYPE_DUST &&
                                                         rule.SerialId == serialId &&
                                                         rule.AveragingPeriod == periodSeconds &&
                                                         !rule.IsDeleted
                                                   select rule;

            return [.. query
                .AsEnumerable()
                .Select(rule => ToRuleDto(rule, serialId))];
        }

        public List<RvtAlertRuleDto> ReadRules(Period period)
        {
            int periodSeconds = DustMonitorDto.PeriodToSeconds(period);
            using MyAtmMonitorContext context = CreateContext();

            IQueryable<RvtAlertRuleEntity> query = from rule in context.AlertRules.AsNoTracking()
                                                   join monitor in context.Monitors.AsNoTracking() on rule.MonitorId equals monitor.Id
                                                   join deployment in context.Deployments.AsNoTracking() on monitor.Id equals deployment.MonitorId
                                                   where monitor.TypeOfMonitor == DustMonitorDto.MONITOR_TYPE_DUST &&
                                                         rule.AveragingPeriod == periodSeconds &&
                                                         deployment.EndDate == null
                                                   select rule;

            return [.. query
                .AsEnumerable()
                .Select(rule => ToRuleDto(rule, rule.SerialId))];
        }

        public List<RvtContactDto> ReadAlertContacts(Guid monitorId)
        {
            using MyAtmMonitorContext context = CreateContext();

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

        public void WriteMonitorList(List<DustMonitorDto> devices)
        {
            using MyAtmMonitorContext context = CreateContext();

            foreach (DustMonitorDto dto in devices)
            {
                MonitorEntity? entity = context.Monitors.FirstOrDefault(row =>
                    row.SerialId == dto.SerialId &&
                    row.TypeOfMonitor == dto.TypeOfMonitor);

                if (entity == null)
                {
                    context.Monitors.Add(MyAtmDbMapper.ToMonitorEntity(dto));
                }
                else
                {
                    MyAtmDbMapper.UpdateMonitorEntity(entity, dto);
                }
            }

            context.SaveChanges();
        }

        public void SetMonitorOffline(Guid monitorId, bool offline)
        {
            using MyAtmMonitorContext context = CreateContext();
            MonitorEntity? monitor = context.Monitors.FirstOrDefault(row => row.Id == monitorId);
            if (monitor == null)
            {
                return;
            }

            monitor.Offline = offline;
            context.SaveChanges();
        }


        public void HandleException(string message, Exception exception)
        {
            // The logger sink gets the full exception (type + stack); the DB
            // sink keeps its message-column semantics below.
            RvtLogger.Logger.LogError(exception, "DBClient HandleException message={Value1}", message);

            using MyAtmMonitorContext context = CreateContext();
            string error = exception.ToString();
            if (error.Length > 1023)
            {
                error = error[..1023];
            }

            context.MyAtmErrorMessages.Add(new MyAtmErrorMessageEntity
            {
                Tag = message.Length > 64 ? message[..64] : message,
                Error = error,
                ErrorTime = DateTime.UtcNow
            });
            context.SaveChanges();
        }

        public async Task<DustImportCommitResult> CommitDustImportAsync(
            MyAtmDustImportCommit commit,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(commit);

            await using MyAtmMonitorContext context = CreateContext();
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            MonitorEntity monitor = await context.Monitors.SingleOrDefaultAsync(row => row.Id == commit.Monitor.Id, cancellationToken)
                ?? throw AdapterException.Of($"CommitDustImport monitorId={commit.Monitor.Id} was not found.");

            await InsertDustDtosAsync(context, commit.Measurements, cancellationToken);
            SetLatestTimestamp(monitor, commit.Watermark, commit.Period);

            foreach (RuleStateMutation mutation in commit.RuleStateMutations)
            {
                RvtAlertRuleEntity? rule = await context.AlertRules.SingleOrDefaultAsync(row => row.Id == mutation.RuleId, cancellationToken);
                if (rule == null || rule.IsActive != mutation.ExpectedIsActive || rule.Accessed != mutation.ExpectedAccessed)
                {
                    throw new DbUpdateConcurrencyException(
                        $"Alert rule {mutation.RuleId} changed before its dust import commit could be applied.");
                }

                rule.IsActive = mutation.IsActive;
                rule.Accessed = DateTimeUtil.AsUtc(mutation.Accessed);
            }

            List<MonitorDeliveryRequest> createdRequests = [];
            if (commit.Measurements.Count > 0)
            {
                string dataDeliveryKey = DataDeliveryKey(commit);
                bool dataMessageExists = await context.DeliveryOutbox
                    .AsNoTracking()
                    .AnyAsync(
                        row => row.Producer == MonitorDeliveryProducers.MyAtm &&
                               row.DeliveryKey == dataDeliveryKey,
                        cancellationToken);
                if (!dataMessageExists)
                {
                    createdRequests.Add(CreateDataInsertedRequest(commit, dataDeliveryKey));
                }
            }
            List<AcceptedAlertCandidate> acceptedCandidates = [];
            foreach (AlertOccurrenceProposal proposal in commit.AlertOccurrences)
            {
                MyAtmAlertOccurrenceEntity? occurrence = await context.AlertOccurrences
                    .SingleOrDefaultAsync(row => row.OccurrenceKey == proposal.Key, cancellationToken);
                if (occurrence != null)
                {
                    continue;
                }

                string normalizedField = MyAtmAlertTransitionEvaluator.NormalizeField(proposal.Field);
                DateTime triggeredAt = DateTimeUtil.AsUtc(proposal.TriggeredAt);
                int period = DustMonitorDto.PeriodToSeconds(proposal.Period);
                bool hasRecentAcceptedCandidate = await IsSuppressedAsync(
                    context,
                    proposal.MonitorId,
                    period,
                    proposal.AlertType,
                    normalizedField,
                    triggeredAt,
                    acceptedCandidates,
                    cancellationToken);
                Guid notificationId = MonitorDeliveryIdentity.CreateGuid($"notification:{proposal.Key}");

                occurrence = new MyAtmAlertOccurrenceEntity
                {
                    OccurrenceKey = proposal.Key,
                    NotificationId = notificationId,
                    MonitorId = proposal.MonitorId,
                    RuleId = proposal.RuleId,
                    Period = period,
                    AlertType = (int)proposal.AlertType,
                    Field = normalizedField,
                    Level = proposal.Level,
                    TriggeredAt = triggeredAt,
                    IsSuppressed = hasRecentAcceptedCandidate,
                    CreatedAt = DateTimeUtil.AsUtc(commit.UtcNow)
                };
                context.AlertOccurrences.Add(occurrence);

                if (hasRecentAcceptedCandidate)
                {
                    continue;
                }

                if (proposal.AlertType is AlertType.Alert or AlertType.Caution)
                {
                    acceptedCandidates.Add(new AcceptedAlertCandidate(
                        proposal.MonitorId,
                        period,
                        proposal.AlertType,
                        normalizedField,
                        triggeredAt));
                }

                RuleAlertDeliveryPlan deliveryPlan = CreateDeliveryPlan(commit.Monitor, proposal, normalizedField, commit.UtcNow);
                context.Notifications.Add(ToNotificationEntity(deliveryPlan.Notification));
                createdRequests.AddRange(deliveryPlan.Deliveries);
            }

            // Persist notifications before the shared outbox rows that reference them. The
            // explicit transaction keeps this two-phase write atomic while avoiding
            // provider-specific batching order.
            await context.SaveChangesAsync(cancellationToken);
            context.DeliveryOutbox.AddRange(createdRequests.Select(request => ToOutboxEntity(request, commit.UtcNow)));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new DustImportCommitResult(createdRequests);
        }

        public async Task<MonitorDeliveryMessage?> ClaimNextDueAsync(
            string producer,
            DateTime utcNow,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            if (!MonitorDeliveryProducers.IsKnown(producer))
            {
                throw new ArgumentException("Unknown monitor delivery producer.", nameof(producer));
            }

            if (leaseDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(leaseDuration));
            }

            DateTime normalizedUtcNow = DateTimeUtil.AsUtc(utcNow);

            for (int attempt = 0; attempt < 3; attempt++)
            {
                await using MyAtmMonitorContext context = CreateContext();
                MonitorDeliveryOutboxEntity? candidate = await context.DeliveryOutbox
                    .AsNoTracking()
                    .Where(row => row.Producer == producer)
                    .Where(row =>
                        (row.Status == "Pending" && row.NextAttemptAt <= normalizedUtcNow) ||
                        (row.Status == "InProgress" && row.LeaseUntil <= normalizedUtcNow))
                    .OrderBy(row => row.NextAttemptAt)
                    .ThenBy(row => row.CreatedAt)
                    .ThenBy(row => row.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (candidate == null)
                {
                    return null;
                }

                await BeforeConditionalOutboxClaimAsync(candidate.Id, normalizedUtcNow, leaseDuration, cancellationToken);
                Guid leaseId = Guid.NewGuid();
                DateTime leaseUntil = normalizedUtcNow.Add(leaseDuration);
                int claimed = await context.DeliveryOutbox
                    .Where(row => row.Id == candidate.Id && row.Producer == producer)
                    .Where(row =>
                        (row.Status == "Pending" && row.NextAttemptAt <= normalizedUtcNow) ||
                        (row.Status == "InProgress" && row.LeaseUntil <= normalizedUtcNow))
                    .ExecuteUpdateAsync(
                        updates => updates
                            .SetProperty(row => row.Status, "InProgress")
                            .SetProperty(row => row.LeaseId, leaseId)
                            .SetProperty(row => row.LeaseUntil, leaseUntil)
                            .SetProperty(row => row.AttemptCount, row => row.AttemptCount + 1),
                        cancellationToken);

                if (claimed != 1)
                {
                    continue;
                }

                candidate.Status = "InProgress";
                candidate.LeaseId = leaseId;
                candidate.LeaseUntil = leaseUntil;
                candidate.AttemptCount++;
                return ToDeliveryMessage(candidate);
            }

            return null;
        }

        // Test seam for deterministically exercising a contender between selection and the real conditional update.
        protected virtual Task BeforeConditionalOutboxClaimAsync(
            Guid candidateId,
            DateTime utcNow,
            TimeSpan lease,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<bool> CompleteAsync(
            Guid id,
            Guid leaseId,
            DateTime completedAt,
            MonitorDeliveryAudit? audit,
            CancellationToken cancellationToken = default)
        {
            DateTime normalizedCompletedAt = DateTimeUtil.AsUtc(completedAt);
            await using MyAtmMonitorContext context = CreateContext();
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            int completed = await context.DeliveryOutbox
                .Where(row => row.Id == id && row.Status == "InProgress" && row.LeaseId == leaseId)
                .ExecuteUpdateAsync(
                    updates => updates
                        .SetProperty(row => row.Status, "Completed")
                        .SetProperty(row => row.LeaseId, (Guid?)null)
                        .SetProperty(row => row.LeaseUntil, (DateTime?)null)
                        .SetProperty(row => row.CompletedAt, normalizedCompletedAt)
                        .SetProperty(row => row.DeadLetteredAt, (DateTime?)null)
                        .SetProperty(row => row.LastError, (string?)null),
                    cancellationToken);

            if (completed != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            if (audit != null)
            {
                AddDeliveryAudit(context, audit);
                await context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        public async Task<bool> RetryAsync(
            Guid id,
            Guid leaseId,
            DateTime nextAttemptAt,
            string error,
            CancellationToken cancellationToken = default)
        {
            string persistedError = PersistedDeliveryError(error);
            DateTime normalizedNextAttemptAt = DateTimeUtil.AsUtc(nextAttemptAt);
            await using MyAtmMonitorContext context = CreateContext();
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            int retried = await context.DeliveryOutbox
                .Where(row => row.Id == id && row.Status == "InProgress" && row.LeaseId == leaseId)
                .ExecuteUpdateAsync(
                    updates => updates
                        .SetProperty(row => row.Status, "Pending")
                        .SetProperty(row => row.NextAttemptAt, normalizedNextAttemptAt)
                        .SetProperty(row => row.LeaseId, (Guid?)null)
                        .SetProperty(row => row.LeaseUntil, (DateTime?)null)
                        .SetProperty(row => row.CompletedAt, (DateTime?)null)
                        .SetProperty(row => row.DeadLetteredAt, (DateTime?)null)
                        .SetProperty(row => row.LastError, persistedError),
                    cancellationToken);

            if (retried != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeadLetterAsync(
            Guid id,
            Guid leaseId,
            DateTime failedAt,
            string error,
            MonitorDeliveryAudit? audit,
            CancellationToken cancellationToken = default)
        {
            string persistedError = PersistedDeliveryError(error);
            DateTime normalizedFailedAt = DateTimeUtil.AsUtc(failedAt);
            await using MyAtmMonitorContext context = CreateContext();
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            int deadLettered = await context.DeliveryOutbox
                .Where(row => row.Id == id && row.Status == "InProgress" && row.LeaseId == leaseId)
                .ExecuteUpdateAsync(
                    updates => updates
                        .SetProperty(row => row.Status, "DeadLetter")
                        .SetProperty(row => row.LeaseId, (Guid?)null)
                        .SetProperty(row => row.LeaseUntil, (DateTime?)null)
                        .SetProperty(row => row.CompletedAt, (DateTime?)null)
                        .SetProperty(row => row.DeadLetteredAt, normalizedFailedAt)
                        .SetProperty(row => row.LastError, persistedError),
                    cancellationToken);

            if (deadLettered != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            if (audit != null)
            {
                AddDeliveryAudit(context, audit);
                await context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        public async Task<int> DeleteCompletedBeforeAsync(
            string producer,
            DateTime cutoff,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!MonitorDeliveryProducers.IsKnown(producer))
            {
                throw new ArgumentException("Unknown monitor delivery producer.", nameof(producer));
            }

            DateTime normalizedCutoff = DateTimeUtil.AsUtc(cutoff);
            await using MyAtmMonitorContext context = CreateContext();
            return await context.DeliveryOutbox
                .Where(row =>
                    row.Producer == producer &&
                    row.Status == "Completed" &&
                    row.CompletedAt != null &&
                    row.CompletedAt < normalizedCutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        public async Task InsertAccessoryPageAsync(
            IReadOnlyList<AccessoryInfoDto> page,
            CancellationToken cancellationToken = default)
        {
            List<AccessoryInfoDto> normalized = [.. page
                .GroupBy(dto => new { dto.SerialId, SampleTime = DateTimeUtil.AsUtc(dto.SampleTime) })
                .Select(group => group.First())];
            if (normalized.Count == 0)
            {
                return;
            }

            await using MyAtmMonitorContext context = CreateContext();
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            List<string> serialIds = [.. normalized.Select(dto => dto.SerialId).Distinct()];
            List<DateTime> sampleTimes = [.. normalized.Select(dto => DateTimeUtil.AsUtc(dto.SampleTime)).Distinct()];
            var existing = await context.AccessoryInfo
                .Where(row => serialIds.Contains(row.SerialId) && sampleTimes.Contains(row.SampleTime))
                .Select(row => new { row.SerialId, row.SampleTime })
                .ToListAsync(cancellationToken);
            HashSet<(string SerialId, DateTime)> existingKeys = [.. existing.Select(row => (row.SerialId, DateTimeUtil.AsUtc(row.SampleTime)))];
            List<MyAtmAccessoryInfoEntity> missing = [.. normalized
                .Where(dto => !existingKeys.Contains((dto.SerialId, DateTimeUtil.AsUtc(dto.SampleTime))))
                .Select(ToAccessoryInfoEntityUtc)];

            context.AccessoryInfo.AddRange(missing);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<MyAtmAlertCommitResult> CommitAlertAsync(
            MyAtmAlertCommit commit,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(commit);

            await using MyAtmMonitorContext context = CreateContext();
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            if (commit.MonitorStateMutation is { } monitorMutation)
            {
                IQueryable<MonitorEntity> monitorQuery = context.Monitors.Where(row => row.Id == monitorMutation.MonitorId);
                if (monitorMutation.ExpectedOffline.HasValue)
                {
                    monitorQuery = monitorMutation.ExpectedOffline.Value
                        ? monitorQuery.Where(row => row.Offline == true)
                        : monitorQuery.Where(row => row.Offline == false || row.Offline == null);
                }

                if (monitorMutation.Offline.HasValue)
                {
                    int updated = await monitorQuery.ExecuteUpdateAsync(
                        setters => setters.SetProperty(row => row.Offline, monitorMutation.Offline.Value),
                        cancellationToken);
                    if (updated != 1)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return new MyAtmAlertCommitResult(false, Array.Empty<MonitorDeliveryRequest>());
                    }
                }
                else if (!await monitorQuery.AnyAsync(cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new MyAtmAlertCommitResult(false, Array.Empty<MonitorDeliveryRequest>());
                }
            }

            foreach (RuleStateMutation mutation in commit.RuleStateMutations)
            {
                IQueryable<RvtAlertRuleEntity> ruleQuery = context.AlertRules
                    .Where(row => row.Id == mutation.RuleId)
                    .Where(row => row.IsActive == mutation.ExpectedIsActive);
                ruleQuery = mutation.ExpectedAccessed.HasValue
                    ? ruleQuery.Where(row => row.Accessed == mutation.ExpectedAccessed.Value)
                    : ruleQuery.Where(row => row.Accessed == null);

                int updated = await ruleQuery.ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(row => row.IsActive, mutation.IsActive)
                        .SetProperty(row => row.Accessed, DateTimeUtil.AsUtc(mutation.Accessed)),
                    cancellationToken);
                if (updated != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new MyAtmAlertCommitResult(false, Array.Empty<MonitorDeliveryRequest>());
                }
            }

            List<MonitorDeliveryRequest> createdRequests = [];
            List<AcceptedAlertCandidate> acceptedCandidates = [];
            foreach (MyAtmAlertOccurrenceInput proposal in commit.Occurrences)
            {
                if (await context.AlertOccurrences.AnyAsync(row => row.OccurrenceKey == proposal.Key, cancellationToken))
                {
                    continue;
                }

                string normalizedField = MyAtmAlertTransitionEvaluator.NormalizeField(proposal.Field);
                DateTime triggeredAt = DateTimeUtil.AsUtc(proposal.TriggeredAt);
                int period = DustMonitorDto.PeriodToSeconds(proposal.Period);
                bool hasRecentAcceptedCandidate = await IsSuppressedAsync(
                    context,
                    proposal.MonitorId,
                    period,
                    proposal.AlertType,
                    normalizedField,
                    triggeredAt,
                    acceptedCandidates,
                    cancellationToken);
                Guid notificationId = MonitorDeliveryIdentity.CreateGuid($"notification:{proposal.Key}");
                RuleAlertDeliveryPlan? deliveryPlan = hasRecentAcceptedCandidate
                    ? null
                    : proposal.DeliveryPlan ?? throw new InvalidOperationException(
                        $"Accepted MyATM occurrence '{proposal.Key}' requires a delivery plan.");
                context.AlertOccurrences.Add(new MyAtmAlertOccurrenceEntity
                {
                    OccurrenceKey = proposal.Key,
                    NotificationId = notificationId,
                    MonitorId = proposal.MonitorId,
                    RuleId = proposal.RuleId,
                    Period = period,
                    AlertType = (int)proposal.AlertType,
                    Field = normalizedField,
                    Level = proposal.Level,
                    TriggeredAt = triggeredAt,
                    IsSuppressed = hasRecentAcceptedCandidate,
                    CreatedAt = DateTimeUtil.AsUtc(commit.UtcNow)
                });
                if (deliveryPlan == null)
                {
                    continue;
                }

                if (proposal.AlertType is AlertType.Alert or AlertType.Caution)
                {
                    acceptedCandidates.Add(new AcceptedAlertCandidate(
                        proposal.MonitorId,
                        period,
                        proposal.AlertType,
                        normalizedField,
                        triggeredAt));
                }

                if (deliveryPlan.Notification.Id != notificationId)
                {
                    throw new InvalidOperationException(
                        $"MyATM occurrence '{proposal.Key}' delivery plan has an unexpected notification identity.");
                }

                context.Notifications.Add(ToNotificationEntity(deliveryPlan.Notification));

                foreach (MonitorDeliveryRequest delivery in deliveryPlan.Deliveries)
                {
                    bool exists = await context.DeliveryOutbox
                        .AsNoTracking()
                        .AnyAsync(
                            row => row.Producer == MonitorDeliveryProducers.MyAtm &&
                                   row.DeliveryKey == delivery.DeliveryKey,
                            cancellationToken);
                    if (exists || createdRequests.Any(message => message.DeliveryKey == delivery.DeliveryKey))
                    {
                        continue;
                    }

                    createdRequests.Add(delivery);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            context.DeliveryOutbox.AddRange(createdRequests.Select(request => ToOutboxEntity(request, commit.UtcNow)));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new MyAtmAlertCommitResult(true, createdRequests);
        }

        public DateTime? ReadLatestAccessoryTimestamp(string serialId)
        {
            using MyAtmMonitorContext context = CreateContext();

            DateTime? latest = context.AccessoryInfo
                .AsNoTracking()
                .Where(row => row.SerialId == serialId)
                .Select(row => (DateTime?)row.SampleTime)
                .Max();
            return DateTimeUtil.AsUtc(latest);
        }

        public MyAtmSiteSchedule ReadSiteSchedule(Guid monitorId)
        {
            using MyAtmMonitorContext context = CreateContext();
            MyAtmSiteSchedule? schedule = (from monitor in context.Monitors.AsNoTracking()
                                           join deployment in context.Deployments.AsNoTracking() on monitor.Id equals deployment.MonitorId
                                           join contract in context.Contracts.AsNoTracking() on deployment.ContractId equals contract.Id
                                           join site in context.Sites.AsNoTracking() on contract.SiteId equals site.Id
                                           where monitor.Id == monitorId && deployment.EndDate == null
                                           select new MyAtmSiteSchedule
                                           {
                                               WeekdayStart = site.StartTime,
                                               WeekdayEnd = site.EndTime,
                                               SaturdayStart = site.SatStartTime,
                                               SaturdayEnd = site.SatEndTime,
                                               SundayStart = site.SunStartTime,
                                               SundayEnd = site.SunEndTime
                                           })
                .FirstOrDefault();

            return schedule ?? throw AdapterException.Of(
                $"ReadSiteSchedule monitorId={monitorId} has no active deployment site.");
        }

        public bool CanConnect()
        {
            try
            {
                using MyAtmMonitorContext context = CreateContext();
                return context.Database.CanConnect();
            }
            catch
            {
                return false;
            }
        }

        public double? GetAverageDustLevel(string serialNumber, string columnName, DateTime start, DateTime end)
        {
            using MyAtmMonitorContext context = CreateContext();
            MonitorAggregateField<MyAtmDustLevelEntity> field = MyAtmAggregateFields.Resolve(columnName);
            DateTime startUtc = DateTimeUtil.AsUtc(start);
            DateTime endUtc = DateTimeUtil.AsUtc(end);
            IQueryable<MyAtmDustLevelEntity> query = context.DustLevels
                .Where(row => row.Avrg == 60)
                .Where(row => row.SerialId == serialNumber)
                .Where(row => row.SampleTime > startUtc && row.SampleTime <= endUtc);

            return field.UseMaximum ? query.Max(field.Selector) : query.Average(field.Selector);
        }

        public void ClearErrorMessages(DateTime before)
        {
            using MyAtmMonitorContext context = CreateContext();
            DateTime beforeUtc = DateTimeUtil.AsUtc(before);
            List<MyAtmErrorMessageEntity> messages = [.. context.MyAtmErrorMessages.Where(row => row.ErrorTime < beforeUtc)];

            context.MyAtmErrorMessages.RemoveRange(messages);
            context.SaveChanges();
        }

        private MyAtmMonitorContext CreateContext()
        {
            MonitorDbOptions monitorOptions = MyAtmMonitorDbOptions.Current;
            DbContextOptions<MyAtmMonitorContext> options = MonitorDbContextOptionsFactory.CreateOptions<MyAtmMonitorContext>(_connectionString);
            return new MyAtmMonitorContext(options, monitorOptions);
        }

        private static async Task InsertDustDtosAsync(
            MyAtmMonitorContext context,
            IReadOnlyList<DustDto> dtos,
            CancellationToken cancellationToken)
        {
            if (dtos.Count == 0)
            {
                return;
            }

            Dictionary<(string SerialId, DateTime SampleTime, int Avrg), DustDto> pendingDtos = [];
            foreach (DustDto dto in dtos)
            {
                pendingDtos.TryAdd((dto.SerialId, DateTimeUtil.AsUtc(dto.SampleTime), dto.Avrg), dto);
            }

            HashSet<string> serialIds = pendingDtos.Keys.Select(key => key.SerialId).ToHashSet(StringComparer.Ordinal);
            DateTime earliest = pendingDtos.Keys.Min(key => key.SampleTime);
            DateTime latest = pendingDtos.Keys.Max(key => key.SampleTime);
            HashSet<(string SerialId, DateTime, int Avrg)> existingKeys = [.. (await context.DustLevels
                    .AsNoTracking()
                    .Where(row => serialIds.Contains(row.SerialId))
                    .Where(row => row.SampleTime >= earliest && row.SampleTime <= latest)
                    .Select(row => new { row.SerialId, row.SampleTime, row.Avrg })
                    .ToListAsync(cancellationToken))
                .Select(row => (row.SerialId, DateTimeUtil.AsUtc(row.SampleTime), row.Avrg))];

            foreach (((string SerialId, DateTime SampleTime, int Avrg) key, DustDto? dto) in pendingDtos)
            {
                if (!existingKeys.Contains(key))
                {
                    context.DustLevels.Add(ToDustLevelEntityUtc(dto));
                }
            }
        }

        private static void SetLatestTimestamp(MonitorEntity monitor, DateTime lastDataTime, Period period)
        {
            DateTime utcLastDataTime = DateTimeUtil.AsUtc(lastDataTime);
            switch (period)
            {
                case Period.Minutes1:
                    monitor.LastDataTime1Min = Latest(monitor.LastDataTime1Min, utcLastDataTime);
                    break;
                case Period.Minutes15:
                    monitor.LastDataTime15Min = Latest(monitor.LastDataTime15Min, utcLastDataTime);
                    break;
                case Period.Hours1:
                    monitor.LastDataTime1Hour = Latest(monitor.LastDataTime1Hour, utcLastDataTime);
                    break;
                case Period.Hours24:
                    monitor.LastDataTime24Hour = Latest(monitor.LastDataTime24Hour, utcLastDataTime);
                    break;
                default:
                    throw AdapterException.Of("WriteLatestTimestamp Unknown Period " + period);
            }
        }

        private static DateTime Latest(DateTime? current, DateTime incoming)
        {
            DateTime? currentUtc = DateTimeUtil.AsUtc(current);
            return !currentUtc.HasValue || incoming > currentUtc.Value ? incoming : currentUtc.Value;
        }

        private static bool Suppresses(AlertType acceptedAlertType, AlertType candidateAlertType) =>
            candidateAlertType switch
            {
                AlertType.Alert => acceptedAlertType == AlertType.Alert,
                AlertType.Caution => acceptedAlertType is AlertType.Alert or AlertType.Caution,
                _ => false
            };

        private static async Task<bool> IsSuppressedAsync(
            MyAtmMonitorContext context,
            Guid monitorId,
            int period,
            AlertType alertType,
            string normalizedField,
            DateTime triggeredAt,
            IReadOnlyCollection<AcceptedAlertCandidate> acceptedCandidates,
            CancellationToken cancellationToken)
        {
            if (alertType is not (AlertType.Alert or AlertType.Caution))
            {
                return false;
            }

            DateTime windowStart = triggeredAt.Subtract(_alertSuppressionWindow);
            List<MyAtmAlertOccurrenceEntity> persistedCandidates = await context.AlertOccurrences
                .AsNoTracking()
                .Where(row =>
                    row.MonitorId == monitorId &&
                    row.Period == period &&
                    !row.IsSuppressed &&
                    (row.AlertType == (int)AlertType.Alert || row.AlertType == (int)AlertType.Caution) &&
                    row.TriggeredAt >= windowStart &&
                    row.TriggeredAt <= triggeredAt)
                .ToListAsync(cancellationToken);
            return persistedCandidates.Any(candidate =>
                    MyAtmAlertTransitionEvaluator.NormalizeField(candidate.Field) == normalizedField &&
                    Suppresses((AlertType)candidate.AlertType, alertType)) ||
                acceptedCandidates.Any(candidate =>
                    candidate.MonitorId == monitorId &&
                    candidate.Period == period &&
                    candidate.NormalizedField == normalizedField &&
                    candidate.TriggeredAt >= windowStart &&
                    candidate.TriggeredAt <= triggeredAt &&
                    Suppresses(candidate.AlertType, alertType));
        }

        private sealed record AcceptedAlertCandidate(
            Guid MonitorId,
            int Period,
            AlertType AlertType,
            string NormalizedField,
            DateTime TriggeredAt);

        private static RuleAlertDeliveryPlan CreateDeliveryPlan(
            DustMonitorDto monitor,
            AlertOccurrenceProposal proposal,
            string normalizedField,
            DateTime createdAt) =>
            _deliveryPlanner.Plan(
                new RuleNotificationRequest(
                    monitor.FleetNr ?? string.Empty,
                    monitor.SerialId,
                    DateTimeUtil.AsUtc(proposal.TriggeredAt),
                    proposal.LimitOn,
                    DustMonitorDto.PeriodToSeconds(proposal.Period),
                    proposal.Level,
                    proposal.AlertType,
                    normalizedField,
                    proposal.MonitorId),
                proposal.Contacts,
                MonitorDeliveryProducers.MyAtm,
                monitor.CustomerId,
                proposal.Key,
                DateTimeUtil.AsUtc(createdAt));

        private static MonitorDeliveryRequest CreateDataInsertedRequest(
            MyAtmDustImportCommit commit,
            string deliveryKey)
        {
            MonitorDeliveryPayloadV1 payload = new(
                Guid.Empty,
                DateTimeUtil.AsUtc(commit.Watermark),
                commit.Monitor.SerialId,
                commit.Monitor.CustomerId,
                commit.Monitor.FleetNr ?? string.Empty,
                AlertType.Ignore,
                string.Empty,
                0);
            return new MonitorDeliveryRequest(
                MonitorDeliveryIdentity.CreateGuid($"outbox:{deliveryKey}"),
                MonitorDeliveryProducers.MyAtm,
                NotificationId: null,
                CorrelationKey: null,
                deliveryKey,
                MonitorDeliveryKind.MqttDataInserted,
                "insert",
                PayloadVersion: 1,
                JsonSerializer.Serialize(payload),
                commit.UtcNow);
        }

        private static string DataDeliveryKey(MyAtmDustImportCommit commit) =>
            $"data:{commit.Monitor.Id:N}:{DustMonitorDto.PeriodToSeconds(commit.Period)}:{DateTimeUtil.AsUtc(commit.Watermark):O}";

        private static NotificationEntity ToNotificationEntity(NotificationDto notification) => new()
        {
            Id = notification.Id,
            NotificationTime = DateTimeUtil.AsUtc(notification.NotificationTime),
            LimitOn = notification.LimitOn,
            AveragingPeriod = notification.AveragingPeriod,
            Level = notification.Level,
            MonitorId = notification.MonitorId,
            AlertType = (int)notification.AlertType,
            AlertField = notification.AlertField
        };

        private static MonitorDeliveryOutboxEntity ToOutboxEntity(
            MonitorDeliveryRequest request,
            DateTime nextAttemptAt) => new()
            {
                Id = request.Id,
                Producer = request.Producer,
                NotificationId = request.NotificationId,
                CorrelationKey = request.CorrelationKey,
                DeliveryKey = request.DeliveryKey,
                Kind = request.Kind,
                Destination = request.Destination,
                PayloadVersion = request.PayloadVersion,
                Payload = request.Payload,
                Status = "Pending",
                AttemptCount = 0,
                NextAttemptAt = DateTimeUtil.AsUtc(nextAttemptAt),
                CreatedAt = DateTimeUtil.AsUtc(request.CreatedAt)
            };

        private static MonitorDeliveryMessage ToDeliveryMessage(MonitorDeliveryOutboxEntity message) => new(
            message.Id,
            message.Producer,
            message.NotificationId,
            message.CorrelationKey,
            message.DeliveryKey,
            message.Kind,
            message.Destination,
            message.PayloadVersion,
            message.Payload,
            message.AttemptCount,
            message.LeaseId ?? throw new InvalidOperationException($"Claimed delivery {message.Id} has no lease ID."));

        private static string PersistedDeliveryError(string error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return error.Length <= 1024 ? error : error[..1024];
        }

        private static void AddDeliveryAudit(MyAtmMonitorContext context, MonitorDeliveryAudit audit)
        {
            context.NotificationAudits.Add(new NotificationSentEntity
            {
                Id = Guid.NewGuid(),
                SendTime = DateTimeUtil.AsUtc(audit.SentAt),
                Address = audit.Address,
                ErrorMessage = audit.Result,
                NotificationId = audit.NotificationId
            });
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
                created: DateTimeUtil.AsUtc(rule.Created),
                accessed: DateTimeUtil.AsUtc(rule.Accessed));
        }

        private static MyAtmDustLevelEntity ToDustLevelEntityUtc(DustDto dto)
        {
            MyAtmDustLevelEntity entity = MyAtmDbMapper.ToDustLevelEntity(dto);
            entity.SampleTime = DateTimeUtil.AsUtc(entity.SampleTime);
            return entity;
        }

        private static MyAtmAccessoryInfoEntity ToAccessoryInfoEntityUtc(AccessoryInfoDto dto)
        {
            MyAtmAccessoryInfoEntity entity = MyAtmDbMapper.ToAccessoryInfoEntity(dto);
            entity.SampleTime = DateTimeUtil.AsUtc(entity.SampleTime);
            return entity;
        }
    }
}
