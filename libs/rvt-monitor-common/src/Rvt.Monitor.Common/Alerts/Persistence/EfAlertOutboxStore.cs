using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
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
    private const int MaximumErrorLength = 256;

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
        var leaseId = Guid.NewGuid();
        var leaseUntil = utcNow.Add(lease);
        await using var context = contextFactory.CreateDbContext();
        var provider = ResolveProvider(context);
        await using var transaction = await context.Database.BeginTransactionAsync(
            AlertOutboxClaimSql.IsolationLevelFor(provider),
            cancellationToken);
        var claimed = await ExecuteClaimAsync(
            context,
            transaction,
            provider,
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
        await using var context = contextFactory.CreateDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var affected = await context.AlertDeliveryOutbox
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
        var persistedError = error.Length <= MaximumErrorLength
            ? error
            : error[..MaximumErrorLength];
        var status = deadLetter ? DeadLetterStatus : PendingStatus;
        DateTime? completedAt = deadLetter ? nextAttemptAt : null;

        await using var context = contextFactory.CreateDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var affected = await context.AlertDeliveryOutbox
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
        await using var context = contextFactory.CreateDbContext();
        return await context.AlertDeliveryOutbox
            .Where(row => row.Status == CompletedStatus && row.CompletedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task<ClaimedAlertDelivery?> ExecuteClaimAsync(
        TContext context,
        IDbContextTransaction transaction,
        MonitorDatabaseProvider provider,
        DateTime utcNow,
        Guid leaseId,
        DateTime leaseUntil,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = AlertOutboxClaimSql.For(provider);
        AddInstantParameter(command, "@now", provider, utcNow);
        AddParameter(command, "@leaseId", DbType.Guid, leaseId);
        AddInstantParameter(command, "@leaseUntil", provider, leaseUntil);

        ClaimedAlertDelivery claimed;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            claimed = Materialize(reader, provider);
        }

        var notificationId = await context.AlertOccurrences
            .AsNoTracking()
            .Where(row => row.Id == claimed.OccurrenceId)
            .Select(row => row.NotificationId)
            .SingleAsync(cancellationToken);
        return claimed with { NotificationId = notificationId };
    }

    private static ClaimedAlertDelivery Materialize(DbDataReader reader, MonitorDatabaseProvider provider)
    {
        var names = provider == MonitorDatabaseProvider.PostgreSql
            ? PostgreSqlColumns.Instance
            : SqlServerColumns.Instance;
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
            AlertOutboxClaimDateTime.Normalize(
                reader.GetDateTime(reader.GetOrdinal(names.NextAttemptAt)),
                provider),
            reader.GetGuid(reader.GetOrdinal(names.LeaseId)),
            AlertOutboxClaimDateTime.Normalize(
                reader.GetDateTime(reader.GetOrdinal(names.LeaseUntil)),
                provider),
            AlertOutboxClaimDateTime.Normalize(
                ReadNullableDateTime(reader, names.CompletedAt),
                provider),
            ReadNullableString(reader, names.LastError),
            AlertOutboxClaimDateTime.Normalize(
                reader.GetDateTime(reader.GetOrdinal(names.CreatedAt)),
                provider));
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
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddInstantParameter(
        DbCommand command,
        string name,
        MonitorDatabaseProvider provider,
        DateTime value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        switch (provider)
        {
            case MonitorDatabaseProvider.PostgreSql when parameter is NpgsqlParameter postgreSqlParameter:
                postgreSqlParameter.NpgsqlDbType = NpgsqlDbType.TimestampTz;
                break;
            case MonitorDatabaseProvider.SqlServer when parameter is SqlParameter sqlServerParameter:
                sqlServerParameter.SqlDbType = SqlDbType.DateTime2;
                break;
            default:
                throw new NotSupportedException(
                    "The database provider does not support durable alert claims.");
        }

        command.Parameters.Add(parameter);
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static string? ReadNullableString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static MonitorDatabaseProvider ResolveProvider(TContext context)
    {
        if (context.Database.IsNpgsql())
        {
            return MonitorDatabaseProvider.PostgreSql;
        }

        if (context.Database.IsSqlServer())
        {
            return MonitorDatabaseProvider.SqlServer;
        }

        throw new NotSupportedException(
            "The database provider does not support durable alert claims.");
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

    private static class SqlServerColumns
    {
        public static ClaimColumns Instance { get; } = new(
            "Id",
            "OccurrenceId",
            "DeliveryKey",
            "Kind",
            "Destination",
            "Payload",
            "Status",
            "AttemptCount",
            "NextAttemptAt",
            "LeaseId",
            "LeaseUntil",
            "CompletedAt",
            "LastError",
            "CreatedAt");
    }
}

internal static class AlertOutboxClaimDateTime
{
    internal static DateTime Normalize(DateTime value, MonitorDatabaseProvider provider) =>
        provider == MonitorDatabaseProvider.SqlServer
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value;

    internal static DateTime? Normalize(DateTime? value, MonitorDatabaseProvider provider) =>
        value is { } timestamp ? Normalize(timestamp, provider) : null;
}
