using System.Data;

namespace Rvt.Monitor.Common.Alerts.Persistence;

internal static class AlertOutboxClaimSql
{
    public const string Statement = """
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

    public static IsolationLevel IsolationLevel => System.Data.IsolationLevel.ReadCommitted;
}
