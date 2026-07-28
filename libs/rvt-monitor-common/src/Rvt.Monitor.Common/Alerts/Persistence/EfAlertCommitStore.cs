using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
            Exception classified = AlertPersistenceExceptionClassifier.Classify(exception);
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
            Exception classified = AlertPersistenceExceptionClassifier.Classify(exception);
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
        await using TContext context = contextFactory.CreateDbContext();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        string serialId = request.Signal.SerialId.Trim();
        MonitorEntity monitor = await context.Monitors.SingleAsync(
            row => row.SerialId == serialId,
            cancellationToken);
        AlertOccurrenceEntity occurrence = NewOccurrence(request, monitor.Id, serialId);
        context.AlertOccurrences.Add(occurrence);

        // Establish the source/hash uniqueness authority before policy and delivery planning.
        await context.SaveChangesAsync(cancellationToken);

        DateTime windowStart = request.Signal.EventTime - request.Signal.SuppressionWindow;
        List<AlertType> recentTypes = await context.Notifications
            .Where(row => row.MonitorId == monitor.Id &&
                          row.NotificationTime >= windowStart &&
                          row.NotificationTime <= request.Signal.EventTime &&
                          (row.AlertType == (int)AlertType.Caution ||
                           row.AlertType == (int)AlertType.Alert))
            .Select(row => (AlertType)row.AlertType)
            .ToListAsync(cancellationToken);
        AlertOccurrenceOutcome outcome = policy.Evaluate(request.Signal.AlertType, recentTypes);

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
        AlertDeliveryEnvelope envelope = new(
            Version: 1,
            request.NotificationId,
            request.Signal.EventTime,
            request.Signal.AlertType,
            serialId,
            monitor.CustomerId,
            monitor.FleetNr?.Trim() is { Length: > 0 } fleetNr ? fleetNr : serialId,
            request.Signal.Message);
        string payload = JsonSerializer.Serialize(envelope);
        HashSet<string> planned = new(StringComparer.Ordinal);

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

        DateTime eventTime = request.Signal.EventTime;
        List<ContactSetting> contactRows = await (
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

        string[] userIds = [.. contactRows
            .Select(row => row.UserId.ToString("D").ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)];
        List<AspNetUserEntity> users = await context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id.ToLower()))
            .ToListAsync(cancellationToken);
        Dictionary<string, AspNetUserEntity> usersById = users.ToDictionary(user => user.Id, StringComparer.OrdinalIgnoreCase);

        foreach (ContactSetting? contact in contactRows)
        {
            if (!ShouldSendAtEventTime(eventTime, contact.StartTime, contact.EndTime) ||
                !usersById.TryGetValue(contact.UserId.ToString("D"), out AspNetUserEntity? user))
            {
                continue;
            }

            if (request.Signal.DeliveryChannels.HasFlag(AlertDeliveryChannels.Email) &&
                contact.Email &&
                !string.IsNullOrWhiteSpace(user.Email))
            {
                string destination = user.Email.Trim();
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
                string destination = user.PhoneNumber.Trim();
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
        string deliveryKey = AlertDeliveryIdentity.Create(
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
        await using TContext context = contextFactory.CreateDbContext();
        AlertOccurrenceEntity occurrence = await context.AlertOccurrences
            .AsNoTracking()
            .SingleAsync(
                row => row.Source == request.Signal.Source &&
                       row.SourceKeyHash.SequenceEqual(request.SourceKeyHash),
                cancellationToken);

        if (!Enum.TryParse<AlertOccurrenceOutcome>(
                occurrence.Outcome,
                ignoreCase: false,
                out AlertOccurrenceOutcome outcome))
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
            SourceKeyHash = [.. request.SourceKeyHash],
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

        TimeSpan time = eventTime.TimeOfDay;
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
