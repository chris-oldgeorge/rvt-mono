// File summary: Verifies the ownership predicate compiles to canonical PostgreSQL SQL.
// Major updates:
// - 2026-07-30 pending Pinned the archive raw SQL to the EF and in-memory ownership-end rules.
// - 2026-07-25 pending Removed the retired-provider translation case.
// - 2026-07-14 pending Added provider translation guards for MonitorOwnershipWindowResolver.OwnsAt.

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Adapters.Archive;
using RvtPortal.Spa.Application.Monitors;

using RvtPortal.Spa.Tests.Support;
namespace RvtPortal.Spa.Tests;

/// <summary>
/// The rest of the suite runs on the EF InMemory provider, which evaluates every predicate client-side and so
/// cannot tell a translatable expression from an untranslatable one. ToQueryString() compiles the query against
/// a real relational provider without opening a connection, so an ownership predicate that PostgreSQL could
/// not translate fails here instead of in production.
/// </summary>
public sealed partial class MonitorOwnershipWindowSqlTests
{
    /// <summary>
    /// The whole-day off-hire rule, spelled the way Npgsql compiles
    /// <see cref="MonitorOwnershipWindowResolver.OwnsAt"/>. The archive's hand-written SQL carries the identical
    /// fragment. The rule exists in three copies; this literal pins two of them by text and
    /// <see cref="EffectiveEnd_MatchesTheInMemoryOwnershipWindow"/> pins the third by execution.
    /// </summary>
    private const string WholeDayOffHireNormalization =
        "CASE WHEN CAST(c.off_hire_date AT TIME ZONE 'UTC' AS time) = TIME '00:00:00' "
        + "THEN date_trunc('day', c.off_hire_date, 'UTC') + INTERVAL '1 days' "
        + "ELSE c.off_hire_date END";

    /// <summary>
    /// Every archive export except Monitors.csv bounds a measurement time range by the ownership window: the six
    /// measurement exports plus breaches. Pinned so an export added without the bound is noticed here.
    /// </summary>
    private const int TimeBoundedExportCount = 7;

    private static readonly DateTime _timestamp = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    // Function summary: Verifies the ownership predicate translates to PostgreSQL SQL.
    public void OwnsAt_TranslatesOnPostgres()
    {
        using RVTDbContext context = new(
            TestDbContexts.ModelOnlyNpgsql<RVTDbContext>());

        string sql = OwnershipQuerySql(context);

        // The whole-day off-hire rule must survive translation rather than being silently dropped.
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("date_trunc", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The archive exports cannot reuse the EF expression - they are hand-written SQL - so nothing but a test
    /// stops the two from drifting. They drifted once already: the archive compared the raw off-hire value and
    /// so dropped the final day of every contract from all eight CSV exports.
    /// </summary>
    [Fact]
    // Function summary: Verifies the archive SQL spells the whole-day off-hire rule exactly as the EF translation does.
    public void ArchiveEndExpression_UsesTheSameWholeDayOffHireRuleAsTheEfTranslation()
    {
        using RVTDbContext context = new(
            TestDbContexts.ModelOnlyNpgsql<RVTDbContext>());
        SiteArchiveQueryCatalog catalog = new();
        string archiveEnd = SiteArchiveQueryCatalog.EffectiveEndExpression();

        string translated = CollapseWhitespace(OwnershipQuerySql(context));
        int timeBounded = catalog.CsvExports
            .Count(export => export.Sql.Contains(archiveEnd, StringComparison.Ordinal));

        Assert.Contains(WholeDayOffHireNormalization, translated, StringComparison.Ordinal);
        Assert.Contains(WholeDayOffHireNormalization, archiveEnd, StringComparison.Ordinal);
        Assert.Equal(TimeBoundedExportCount, timeBounded);
    }

    /// <summary>
    /// Executes the archive's end expression on PostgreSQL and compares it with the in-memory window the rest of
    /// the portal computes, over a date-only off-hire, an off-hire carrying a time component, and both orderings
    /// of the deployment end against it. Runs the whole matrix under a deliberately non-UTC session time zone as
    /// well: the off-hire column is <c>timestamptz</c>, so a normalization written without an explicit UTC anchor
    /// would agree in CI and disagree on a server whose <c>TimeZone</c> setting is not UTC.
    /// </summary>
    [RequiresPostgresFact]
    // Function summary: Verifies the archive end expression returns exactly what the in-memory ownership window computes.
    public async Task EffectiveEnd_MatchesTheInMemoryOwnershipWindow()
    {
        string connectionString = Environment.GetEnvironmentVariable(
            RequiresPostgresFactAttribute.ConnectionVariable)!;
        DateTime start = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        (DateTime? OffHire, DateTime? DeploymentEnd)[] cases =
        [
            // A date-only off-hire covers the whole day, so the exclusive end is the next midnight.
            (new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), null),
            // An off-hire carrying a time component is already exclusive and must be left alone.
            (new DateTime(2026, 5, 10, 17, 30, 0, DateTimeKind.Utc), null),
            // The minimum has to be taken after normalizing, not before: this deployment end falls inside the
            // final day, so it wins over an off-hire that the raw comparison called the earlier of the two.
            (new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc)),
            // The normalized off-hire wins when the deployment outlives the contract.
            (new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            (null, new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc))
        ];

        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        foreach (string sessionTimeZone in new[] { "UTC", "America/New_York" })
        {
            await using (NpgsqlCommand setTimeZone = connection.CreateCommand())
            {
                setTimeZone.CommandText = $"SET TIME ZONE '{sessionTimeZone}';";
                await setTimeZone.ExecuteNonQueryAsync();
            }

            foreach ((DateTime? offHire, DateTime? deploymentEnd) in cases)
            {
                DateTime? expected = MonitorOwnershipWindowResolver.ForDeployment(new Deployment
                {
                    StartDate = start,
                    EndDate = deploymentEnd,
                    Contract = new Contract
                    {
                        OnHireDate = start,
                        OffHireDate = offHire
                    }
                }).End;
                DateTime actual = await EvaluateEndAsync(connection, offHire, deploymentEnd);

                Assert.Equal(expected, actual);
            }
        }
    }

    // Function summary: Compiles the ownership-filtered deployment query to SQL without executing it.
    private static string OwnershipQuerySql(RVTDbContext context)
    {
        // ToQueryString() throws InvalidOperationException if any part of the predicate cannot be translated.
        return context.Deployments
            .Where(MonitorOwnershipWindowResolver.OwnsAt(_timestamp))
            .ToQueryString();
    }

    // Function summary: Evaluates the archive end expression over one off-hire and deployment-end pair.
    private static async Task<DateTime> EvaluateEndAsync(
        NpgsqlConnection connection,
        DateTime? offHire,
        DateTime? deploymentEnd)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = string.Format(
            CultureInfo.InvariantCulture,
            "SELECT {0} FROM (SELECT @offHire::timestamptz AS off_hire_date) AS c, "
                + "(SELECT @endDate::timestamptz AS end_date) AS d",
            SiteArchiveQueryCatalog.EffectiveEndExpression());
        command.Parameters.AddWithValue("offHire", (object?)offHire ?? DBNull.Value);
        command.Parameters.AddWithValue("endDate", (object?)deploymentEnd ?? DBNull.Value);
        return (DateTime)(await command.ExecuteScalarAsync())!;
    }

    // Function summary: Collapses runs of whitespace so compiled SQL can be matched against a single-line fragment.
    private static string CollapseWhitespace(string sql)
    {
        return WhitespaceRuns().Replace(sql, " ");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRuns();
}
