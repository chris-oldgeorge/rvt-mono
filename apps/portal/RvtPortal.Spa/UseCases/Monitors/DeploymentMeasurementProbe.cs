// File summary: Answers whether any measurement has arrived inside a deployment's ownership window.
// Major updates:
// - 2026-07-31 pending Added for the ruling that a deployment may only be hard-deleted while it owns no data.

using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using RVT.DataAccess.Context;

namespace RvtPortal.Spa.UseCases.Monitors;

public interface IDeploymentMeasurementProbe
{
    // Function summary: Reports whether any measurement exists for the serial inside the ownership window.
    Task<bool> HasMeasurementsAsync(
        string serialId,
        MonitorOwnershipWindow window,
        CancellationToken cancellationToken);
}

/// <summary>
/// The "has any data arrived?" half of the deployment-removal ruling (§4.2, 2026-07-30): removing a monitor from
/// a contract may only delete the deployment row outright while nothing is attributed to it.
/// <para>
/// The relations are the five raw arrival tables the site archive exports - the ones a monitor writes into
/// directly. The portal's own reads go through Timescale continuous aggregates, which lag their base tables by
/// a refresh policy; asking the aggregates would report "no data" for a sample that landed minutes ago and
/// hard-delete the deployment that owns it, which is exactly the outcome the ruling forbids.
/// </para>
/// <para>
/// One statement, one <c>EXISTS</c> per relation, OR-ed: PostgreSQL stops each subquery at its first matching
/// row and skips the remaining branches once one is true, so the common "yes, data exists" answer costs a
/// single index probe. A count would read every row to produce a number nothing uses.
/// </para>
/// <para>
/// Relations are unqualified so they resolve through the connection's <c>SearchPath</c>, matching the archive
/// catalog's convention; the two noise tables no portal EF model maps are created in test schemas by
/// <c>SpaTestDatabase</c>.
/// </para>
/// </summary>
public sealed class DeploymentMeasurementProbe : IDeploymentMeasurementProbe
{
    private readonly RVTDbContext _domainContext;

    // Function summary: Initializes the probe on the portal's shared scoped connection.
    public DeploymentMeasurementProbe(RVTDbContext domainContext)
    {
        _domainContext = domainContext;
    }

    // Function summary: Reports whether any measurement exists for the serial inside the ownership window.
    public async Task<bool> HasMeasurementsAsync(
        string serialId,
        MonitorOwnershipWindow window,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serialId))
        {
            return false;
        }

        // The measurement relations are timestamp-without-zone holding UTC, so the bounds cross the same
        // boundary every other telemetry read crosses. The parameters are typed explicitly because Npgsql maps
        // a bare DateTime to timestamptz: an interpolated bound would either be rejected outright or, worse,
        // compared across types and resolved in the session time zone.
        DateTime from = SearchTimestampPolicy.ToDatabase(window.Start);
        DateTime? to = window.End.HasValue ? SearchTimestampPolicy.ToDatabase(window.End.Value) : null;

        return await _domainContext.Database
            .SqlQueryRaw<bool>(
                """
                SELECT
                    (
                        EXISTS (
                            SELECT 1 FROM my_atm_dust_level
                            WHERE serial_id = @serialId
                              AND sample_time >= @fromUtc
                              AND (@toUtc IS NULL OR sample_time < @toUtc)
                        )
                        OR EXISTS (
                            SELECT 1 FROM air_q_noise_level
                            WHERE serial_id = @serialId
                              AND sample_time >= @fromUtc
                              AND (@toUtc IS NULL OR sample_time < @toUtc)
                        )
                        OR EXISTS (
                            SELECT 1 FROM svantek_noise_level
                            WHERE serial_id = @serialId
                              AND sample_time >= @fromUtc
                              AND (@toUtc IS NULL OR sample_time < @toUtc)
                        )
                        OR EXISTS (
                            SELECT 1 FROM omnidots_peak_level
                            WHERE serial_id = @serialId
                              AND sample_time >= @fromUtc
                              AND (@toUtc IS NULL OR sample_time < @toUtc)
                        )
                        OR EXISTS (
                            SELECT 1 FROM omnidots_trace_index
                            WHERE serial_id = @serialId
                              AND start_time >= @fromUtc
                              AND (@toUtc IS NULL OR start_time < @toUtc)
                        )
                    ) AS "Value"
                """,
                new NpgsqlParameter("serialId", NpgsqlDbType.Varchar) { Value = serialId },
                new NpgsqlParameter("fromUtc", NpgsqlDbType.Timestamp) { Value = from },
                new NpgsqlParameter("toUtc", NpgsqlDbType.Timestamp) { Value = (object?)to ?? DBNull.Value })
            .SingleAsync(cancellationToken);
    }
}
