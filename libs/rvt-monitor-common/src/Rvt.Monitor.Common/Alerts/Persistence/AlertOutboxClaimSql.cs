using System.Data;
using Rvt.Monitor.Common.Data;

namespace Rvt.Monitor.Common.Alerts.Persistence;

internal static class AlertOutboxClaimSql
{
    private const string PostgreSql = """
        WITH candidate AS (
            SELECT id
            FROM alert_delivery_outbox
            WHERE (status = 'Pending' AND next_attempt_at <= @now)
               OR (status = 'Leased' AND lease_until <= @now)
            ORDER BY next_attempt_at, created_at, id
            FOR UPDATE SKIP LOCKED
            LIMIT 1
        )
        UPDATE alert_delivery_outbox AS target
        SET status = 'Leased', lease_id = @leaseId, lease_until = @leaseUntil,
            attempt_count = attempt_count + 1
        FROM candidate
        WHERE target.id = candidate.id
        RETURNING target.*;
        """;

    private const string SqlServer = """
        WITH candidate AS (
            SELECT TOP (1) *
            FROM dbo.AlertDeliveryOutbox WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE (Status = N'Pending' AND NextAttemptAt <= @now)
               OR (Status = N'Leased' AND LeaseUntil <= @now)
            ORDER BY NextAttemptAt, CreatedAt, Id
        )
        UPDATE candidate
        SET Status = N'Leased', LeaseId = @leaseId, LeaseUntil = @leaseUntil,
            AttemptCount = AttemptCount + 1
        OUTPUT INSERTED.*;
        """;

    public static string For(MonitorDatabaseProvider provider) =>
        provider switch
        {
            MonitorDatabaseProvider.PostgreSql => PostgreSql,
            MonitorDatabaseProvider.SqlServer => SqlServer,
            _ => throw new NotSupportedException(
                "The database provider does not support durable alert claims.")
        };

    public static IsolationLevel IsolationLevelFor(MonitorDatabaseProvider provider) =>
        provider switch
        {
            MonitorDatabaseProvider.PostgreSql => IsolationLevel.ReadCommitted,
            MonitorDatabaseProvider.SqlServer => IsolationLevel.RepeatableRead,
            _ => throw new NotSupportedException(
                "The database provider does not support durable alert claims.")
        };
}
