using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Rvt.Monitor.Common.Alerts.Persistence;

public sealed class EfAlertOutboxStore<TContext>(IMonitorDbContextFactory<TContext> contextFactory)
    : IAlertOutboxStore
    where TContext : MonitorDbContextBase
{
    private const string LeasedStatus = "Leased";
    private const string CompletedStatus = "Completed";
    private const string PendingStatus = "Pending";
    private const string DeadLetterStatus = "DeadLetter";

    // Keep the outbox row's error the same length as the audit trail's
    // (DeliveryDispatchPolicy.SafeError) instead of re-truncating to 256.
    private const int MaximumErrorLength = Delivery.DeliveryDispatchPolicy.MaximumErrorLength;

    public async Task<ClaimedAlertDelivery?> ClaimNextDueAsync(
        DateTime utcNow,
        TimeSpan lease,
        CancellationToken cancellationToken = default)
    {
        if (lease <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lease));
        }

        cancellationToken.ThrowIfCancellationRequested();
        Guid leaseId = Guid.NewGuid();
        DateTime leaseUntil = utcNow.Add(lease);
        await using TContext context = contextFactory.CreateDbContext();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(
            AlertOutboxClaimSql.IsolationLevel,
            cancellationToken);
        ClaimedAlertDelivery? claimed = await ExecuteClaimAsync(
            context,
            transaction,
            utcNow,
            leaseId,
            leaseUntil,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    public async Task<bool> CompleteAsync(
        Guid id,
        Guid leaseId,
        DateTime completedAt,
        AlertDeliveryAudit? audit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using TContext context = contextFactory.CreateDbContext();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        int affected = await context.AlertDeliveryOutbox
            .Where(row => row.Id == id && row.Status == LeasedStatus && row.LeaseId == leaseId)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(row => row.Status, CompletedStatus)
                    .SetProperty(row => row.LeaseId, (Guid?)null)
                    .SetProperty(row => row.LeaseUntil, (DateTime?)null)
                    .SetProperty(row => row.CompletedAt, completedAt)
                    .SetProperty(row => row.LastError, (string?)null),
                cancellationToken);

        if (affected == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (audit is not null)
        {
            AddAudit(context, audit);
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
        bool deadLetter,
        AlertDeliveryAudit? audit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);
        cancellationToken.ThrowIfCancellationRequested();
        string persistedError = error.Length <= MaximumErrorLength
            ? error
            : error[..MaximumErrorLength];
        string status = deadLetter ? DeadLetterStatus : PendingStatus;
        DateTime? completedAt = deadLetter ? nextAttemptAt : null;

        await using TContext context = contextFactory.CreateDbContext();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        int affected = await context.AlertDeliveryOutbox
            .Where(row => row.Id == id && row.Status == LeasedStatus && row.LeaseId == leaseId)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(row => row.Status, status)
                    .SetProperty(row => row.NextAttemptAt, nextAttemptAt)
                    .SetProperty(row => row.LeaseId, (Guid?)null)
                    .SetProperty(row => row.LeaseUntil, (DateTime?)null)
                    .SetProperty(row => row.CompletedAt, completedAt)
                    .SetProperty(row => row.LastError, persistedError),
                cancellationToken);

        if (affected == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (deadLetter && audit is not null)
        {
            AddAudit(context, audit);
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteCompletedBeforeAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using TContext context = contextFactory.CreateDbContext();
        return await context.AlertDeliveryOutbox
            .Where(row => row.Status == CompletedStatus && row.CompletedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task<ClaimedAlertDelivery?> ExecuteClaimAsync(
        TContext context,
        IDbContextTransaction transaction,
        DateTime utcNow,
        Guid leaseId,
        DateTime leaseUntil,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = AlertOutboxClaimSql.Statement;
        AddInstantParameter(command, "@now", utcNow);
        AddParameter(command, "@leaseId", DbType.Guid, leaseId);
        AddInstantParameter(command, "@leaseUntil", leaseUntil);

        ClaimedAlertDelivery claimed;
        await using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            claimed = Materialize(reader);
        }

        Guid? notificationId = await context.AlertOccurrences
            .AsNoTracking()
            .Where(row => row.Id == claimed.OccurrenceId)
            .Select(row => row.NotificationId)
            .SingleAsync(cancellationToken);
        return claimed with { NotificationId = notificationId };
    }

    private static ClaimedAlertDelivery Materialize(DbDataReader reader)
    {
        ClaimColumns names = PostgreSqlColumns.Instance;
        return new ClaimedAlertDelivery(
            reader.GetGuid(reader.GetOrdinal(names.Id)),
            reader.GetGuid(reader.GetOrdinal(names.OccurrenceId)),
            null,
            reader.GetString(reader.GetOrdinal(names.DeliveryKey)),
            reader.GetString(reader.GetOrdinal(names.Kind)),
            reader.GetString(reader.GetOrdinal(names.Destination)),
            reader.GetString(reader.GetOrdinal(names.Payload)),
            reader.GetString(reader.GetOrdinal(names.Status)),
            reader.GetInt32(reader.GetOrdinal(names.AttemptCount)),
            reader.GetDateTime(reader.GetOrdinal(names.NextAttemptAt)),
            reader.GetGuid(reader.GetOrdinal(names.LeaseId)),
            reader.GetDateTime(reader.GetOrdinal(names.LeaseUntil)),
            ReadNullableDateTime(reader, names.CompletedAt),
            ReadNullableString(reader, names.LastError),
            reader.GetDateTime(reader.GetOrdinal(names.CreatedAt)));
    }

    private static void AddAudit(TContext context, AlertDeliveryAudit audit)
    {
        context.NotificationAudits.Add(new NotificationSentEntity
        {
            Id = Guid.NewGuid(),
            SendTime = audit.SentAt,
            Address = audit.Address,
            ErrorMessage = audit.Message,
            NotificationId = audit.NotificationId
        });
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddInstantParameter(
        DbCommand command,
        string name,
        DateTime value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        if (parameter is not NpgsqlParameter postgreSqlParameter)
        {
            throw new NotSupportedException(
                "The database provider does not support durable alert claims.");
        }

        postgreSqlParameter.NpgsqlDbType = NpgsqlDbType.TimestampTz;
        command.Parameters.Add(parameter);
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string name)
    {
        int ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static string? ReadNullableString(DbDataReader reader, string name)
    {
        int ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private sealed record ClaimColumns(
        string Id,
        string OccurrenceId,
        string DeliveryKey,
        string Kind,
        string Destination,
        string Payload,
        string Status,
        string AttemptCount,
        string NextAttemptAt,
        string LeaseId,
        string LeaseUntil,
        string CompletedAt,
        string LastError,
        string CreatedAt);

    private static class PostgreSqlColumns
    {
        public static ClaimColumns Instance { get; } = new(
            "id",
            "occurrence_id",
            "delivery_key",
            "kind",
            "destination",
            "payload",
            "status",
            "attempt_count",
            "next_attempt_at",
            "lease_id",
            "lease_until",
            "completed_at",
            "last_error",
            "created_at");
    }

}
