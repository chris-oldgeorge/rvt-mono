using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Alerts.Persistence;

public sealed class EfAlertCommitStore<TContext> : IAlertCommitStore
    where TContext : MonitorDbContextBase
{
    private const string MqttKind = "MqttAlert";
    private const string EmailKind = "Email";
    private const string SmsKind = "Sms";
    private const string MqttDestination = "alert";
    private const string PendingStatus = "Pending";

    private readonly IMonitorDbContextFactory<TContext> contextFactory;
    private readonly IAlertAcceptancePolicy policy;

    public EfAlertCommitStore(
        IMonitorDbContextFactory<TContext> contextFactory,
        IAlertAcceptancePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(policy);

        this.contextFactory = contextFactory;
        this.policy = policy;
    }

    public async Task<AlertCommitResult> CommitAsync(
        AlertCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await TryCommitAsync(request, cancellationToken);
        }
        catch (Exception exception)
        {
            var classified = AlertPersistenceExceptionClassifier.Classify(exception);
            if (classified is AlertOccurrenceConflictException)
            {
                return await RecoverDuplicateAsync(request, cancellationToken);
            }

            throw classified;
        }
    }

    private async Task<AlertCommitResult> RecoverDuplicateAsync(
        AlertCommitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ReadDuplicateAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var classified = AlertPersistenceExceptionClassifier.Classify(exception);
            if (ReferenceEquals(classified, exception))
            {
                throw;
            }

            throw classified;
        }
    }

    private async Task<AlertCommitResult> TryCommitAsync(
        AlertCommitRequest request,
        CancellationToken cancellationToken)
    {
        await using var context = contextFactory.CreateDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var serialId = request.Signal.SerialId.Trim();
        var monitor = await context.Monitors.SingleAsync(
            row => row.SerialId == serialId,
            cancellationToken);
        var occurrence = NewOccurrence(request, monitor.Id, serialId);
        context.AlertOccurrences.Add(occurrence);

        // Establish the source/hash uniqueness authority before policy and delivery planning.
        await context.SaveChangesAsync(cancellationToken);

        var windowStart = request.Signal.EventTime - request.Signal.SuppressionWindow;
        var recentTypes = await context.Notifications
            .Where(row => row.MonitorId == monitor.Id &&
                          row.NotificationTime >= windowStart &&
                          row.NotificationTime <= request.Signal.EventTime &&
                          (row.AlertType == (int)AlertType.Caution ||
                           row.AlertType == (int)AlertType.Alert))
            .Select(row => (AlertType)row.AlertType)
            .ToListAsync(cancellationToken);
        var outcome = policy.Evaluate(request.Signal.AlertType, recentTypes);

        occurrence.Outcome = outcome.ToString();
        if (outcome == AlertOccurrenceOutcome.Accepted)
        {
            occurrence.NotificationId = request.NotificationId;
            context.Notifications.Add(NewNotification(request, monitor.Id));
            await PlanDeliveriesAsync(context, request, occurrence.Id, monitor, serialId, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AlertCommitResult(
            occurrence.Id,
            occurrence.NotificationId,
            outcome,
            IsDuplicate: false);
    }

    private async Task PlanDeliveriesAsync(
        TContext context,
        AlertCommitRequest request,
        Guid occurrenceId,
        MonitorEntity monitor,
        string serialId,
        CancellationToken cancellationToken)
    {
        var envelope = new AlertDeliveryEnvelope(
            Version: 1,
            request.NotificationId,
            request.Signal.EventTime,
            request.Signal.AlertType,
            serialId,
            monitor.CustomerId,
            monitor.FleetNr?.Trim() is { Length: > 0 } fleetNr ? fleetNr : serialId,
            request.Signal.Message);
        var payload = JsonSerializer.Serialize(envelope);
        var planned = new HashSet<string>(StringComparer.Ordinal);

        if (request.Signal.DeliveryChannels.HasFlag(AlertDeliveryChannels.Mqtt))
        {
            AddDelivery(
                context,
                request,
                occurrenceId,
                MqttKind,
                MqttDestination,
                MqttDestination,
                payload,
                planned);
        }

        if ((request.Signal.DeliveryChannels & (AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms)) == 0)
        {
            return;
        }

        var eventTime = request.Signal.EventTime;
        var contactRows = await (
            from deployment in context.Deployments.AsNoTracking()
            join contract in context.Contracts.AsNoTracking()
                on deployment.ContractId equals contract.Id
            join siteUser in context.SiteUsers.AsNoTracking()
                on contract.SiteId equals (Guid?)siteUser.SiteId
            join setting in context.NotificationSettings.AsNoTracking()
                on siteUser.Id equals setting.SiteUserId
            where deployment.MonitorId == monitor.Id &&
                  deployment.StartDate <= eventTime &&
                  (deployment.EndDate == null || deployment.EndDate >= eventTime) &&
                  siteUser.StartDate <= eventTime &&
                  (siteUser.EndDate == null || siteUser.EndDate >= eventTime) &&
                  (setting.Email || setting.SMS)
            select new ContactSetting(
                siteUser.UserId,
                setting.Email,
                setting.SMS,
                setting.StartTime,
                setting.EndTime))
            .ToListAsync(cancellationToken);

        var userIds = contactRows
            .Select(row => row.UserId.ToString("D").ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var users = await context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id.ToLower()))
            .ToListAsync(cancellationToken);
        var usersById = users.ToDictionary(user => user.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var contact in contactRows)
        {
            if (!ShouldSendAtEventTime(eventTime, contact.StartTime, contact.EndTime) ||
                !usersById.TryGetValue(contact.UserId.ToString("D"), out var user))
            {
                continue;
            }

            if (request.Signal.DeliveryChannels.HasFlag(AlertDeliveryChannels.Email) &&
                contact.Email &&
                !string.IsNullOrWhiteSpace(user.Email))
            {
                var destination = user.Email.Trim();
                AddDelivery(
                    context,
                    request,
                    occurrenceId,
                    EmailKind,
                    destination,
                    AlertDeliveryIdentity.CanonicalEmail(destination),
                    payload,
                    planned);
            }

            if (request.Signal.DeliveryChannels.HasFlag(AlertDeliveryChannels.Sms) &&
                contact.Sms &&
                !string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                var destination = user.PhoneNumber.Trim();
                AddDelivery(
                    context,
                    request,
                    occurrenceId,
                    SmsKind,
                    destination,
                    AlertDeliveryIdentity.CanonicalSms(destination),
                    payload,
                    planned);
            }
        }
    }

    private static void AddDelivery(
        TContext context,
        AlertCommitRequest request,
        Guid occurrenceId,
        string kind,
        string destination,
        string canonicalDestination,
        string payload,
        ISet<string> planned)
    {
        var deliveryKey = AlertDeliveryIdentity.Create(
            occurrenceId,
            kind,
            canonicalDestination);
        if (!planned.Add(deliveryKey))
        {
            return;
        }

        context.AlertDeliveryOutbox.Add(new AlertDeliveryOutboxEntity
        {
            Id = Guid.NewGuid(),
            OccurrenceId = occurrenceId,
            DeliveryKey = deliveryKey,
            Kind = kind,
            Destination = destination,
            Payload = payload,
            Status = PendingStatus,
            AttemptCount = 0,
            NextAttemptAt = request.CreatedAt,
            CreatedAt = request.CreatedAt
        });
    }

    private async Task<AlertCommitResult> ReadDuplicateAsync(
        AlertCommitRequest request,
        CancellationToken cancellationToken)
    {
        await using var context = contextFactory.CreateDbContext();
        var occurrence = await context.AlertOccurrences
            .AsNoTracking()
            .SingleAsync(
                row => row.Source == request.Signal.Source &&
                       row.SourceKeyHash.SequenceEqual(request.SourceKeyHash),
                cancellationToken);

        if (!Enum.TryParse<AlertOccurrenceOutcome>(
                occurrence.Outcome,
                ignoreCase: false,
                out var outcome))
        {
            throw new InvalidOperationException("The stored alert occurrence outcome is invalid.");
        }

        return new AlertCommitResult(
            occurrence.Id,
            occurrence.NotificationId,
            outcome,
            IsDuplicate: true);
    }

    private static AlertOccurrenceEntity NewOccurrence(
        AlertCommitRequest request,
        Guid monitorId,
        string serialId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Source = request.Signal.Source,
            SourceKeyHash = request.SourceKeyHash.ToArray(),
            MonitorId = monitorId,
            SerialId = serialId,
            EventTime = request.Signal.EventTime,
            AlertType = (int)request.Signal.AlertType,
            AlertField = request.Signal.Field,
            Level = request.Signal.Level,
            LimitOn = request.Signal.Limit,
            AveragingPeriod = request.Signal.AveragingPeriod,
            Outcome = nameof(AlertOccurrenceOutcome.Ignored),
            CreatedAt = request.CreatedAt
        };

    private static NotificationEntity NewNotification(
        AlertCommitRequest request,
        Guid monitorId) =>
        new()
        {
            Id = request.NotificationId,
            NotificationTime = request.Signal.EventTime,
            LimitOn = request.Signal.Limit,
            AveragingPeriod = request.Signal.AveragingPeriod,
            Level = request.Signal.Level,
            MonitorId = monitorId,
            AlertField = request.Signal.Field,
            AlertType = (int)request.Signal.AlertType
        };

    private static bool ShouldSendAtEventTime(
        DateTime eventTime,
        TimeSpan? startTime,
        TimeSpan? endTime)
    {
        if (startTime is null || endTime is null)
        {
            return true;
        }

        var time = eventTime.TimeOfDay;
        return startTime <= endTime
            ? time >= startTime && time <= endTime
            : time >= startTime || time <= endTime;
    }

    private sealed record ContactSetting(
        Guid UserId,
        bool Email,
        bool Sms,
        TimeSpan? StartTime,
        TimeSpan? EndTime);
}
