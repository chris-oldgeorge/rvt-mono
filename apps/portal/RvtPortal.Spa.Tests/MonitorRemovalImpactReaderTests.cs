// File summary: Pins the page-batched removal-impact query to the per-row BuildAsync results.
// Major updates:
// - 2026-07-30 pending Moved the parity test onto PostgreSQL after the InMemory view fallback was deleted.
// - 2026-07-30 pending Added with the batched unattached-monitors impact path (N+1 removal).

using Microsoft.EntityFrameworkCore;
using Npgsql;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Monitors;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public sealed class MonitorRemovalImpactReaderTests
{
    [RequiresPostgresFact]
    // Function summary: Verifies the batched page query returns exactly what per-row BuildAsync returns for every monitor.
    public async Task BuildForPageAsync_MatchesPerRowBuildAsync()
    {
        await using RemovalImpactFixture fixture = await RemovalImpactFixture.CreateAsync();

        Guid withEverything = Guid.NewGuid();
        Guid withNothing = Guid.NewGuid();
        Guid withMeasurementsOnly = Guid.NewGuid();
        string everythingSerial = $"SER-EVERYTHING-{Guid.NewGuid():N}";
        string nothingSerial = $"SER-NOTHING-{Guid.NewGuid():N}";
        string measurementsSerial = $"SER-MEASUREMENTS-{Guid.NewGuid():N}";

        await fixture.SeedAsync(
            deploymentMonitorIds: [withEverything, withEverything],
            notificationMonitorIds: [withEverything, withEverything, withEverything],
            alertRuleMonitorIds: [withEverything],
            measurementImpacts:
            [
                (everythingSerial, TableCount: 2, RowCount: 5),
                (measurementsSerial, TableCount: 1, RowCount: 3)
            ]);

        await using RVTDbContext domainContext = fixture.CreateDomainContext();
        await using RVTSearchContext searchContext = fixture.CreateSearchContext();
        MonitorRemovalImpactReader reader = new(domainContext, searchContext);
        List<MonitorRemovalImpactKey> page =
        [
            new(withEverything, everythingSerial),
            new(withNothing, nothingSerial),
            new(withMeasurementsOnly, measurementsSerial)
        ];

        IReadOnlyDictionary<Guid, MonitorRemovalImpactResponse> batched =
            await reader.BuildForPageAsync(page, CancellationToken.None);

        Assert.Equal(page.Count, batched.Count);
        foreach (MonitorRemovalImpactKey key in page)
        {
            MonitorRemovalImpactResponse expected = await reader.BuildAsync(key.MonitorId, key.SerialId, CancellationToken.None);
            MonitorRemovalImpactResponse actual = batched[key.MonitorId];

            Assert.Equal(expected.DeploymentCount, actual.DeploymentCount);
            Assert.Equal(expected.NotificationCount, actual.NotificationCount);
            Assert.Equal(expected.AlertRuleCount, actual.AlertRuleCount);
            Assert.Equal(expected.MeasurementTableCount, actual.MeasurementTableCount);
            Assert.Equal(expected.MeasurementRowCount, actual.MeasurementRowCount);
            Assert.Equal(expected.HasRelatedData, actual.HasRelatedData);
        }

        // The monitor with related rows must actually have non-zero counts, or this parity check proves nothing.
        Assert.Equal(2, batched[withEverything].DeploymentCount);
        Assert.Equal(3, batched[withEverything].NotificationCount);
        Assert.Equal(1, batched[withEverything].AlertRuleCount);
        Assert.Equal(2, batched[withEverything].MeasurementTableCount);
        Assert.Equal(5, batched[withEverything].MeasurementRowCount);
        Assert.True(batched[withMeasurementsOnly].MeasurementRowCount > 0);
        Assert.False(batched[withNothing].HasRelatedData);
    }

    [Fact]
    // Function summary: Verifies an empty page short-circuits without touching the database.
    public async Task BuildForPageAsync_WithEmptyPage_ReturnsEmpty()
    {
        using RVTDbContext domainContext = new(TestDbContexts.InMemory<RVTDbContext>());
        using RVTSearchContext searchContext = new(TestDbContexts.InMemory<RVTSearchContext>());
        MonitorRemovalImpactReader reader = new(domainContext, searchContext);

        IReadOnlyDictionary<Guid, MonitorRemovalImpactResponse> batched =
            await reader.BuildForPageAsync([], CancellationToken.None);

        Assert.Empty(batched);
    }

    /// <summary>
    /// A throwaway PostgreSQL schema holding only the relations the impact reader queries. The reader never
    /// selects beyond the monitor-id and impact-count columns, so the tables carry just those; the
    /// monitor_measurement_removal_impact view is stood in by a table of the same shape, letting the test seed
    /// per-serial counts directly instead of loading the fourteen measurement tables the real view aggregates.
    /// </summary>
    private sealed class RemovalImpactFixture : IAsyncDisposable
    {
        private readonly string baseConnectionString;
        private readonly string schema;
        private readonly string scopedConnectionString;

        private RemovalImpactFixture(string baseConnectionString, string schema)
        {
            this.baseConnectionString = baseConnectionString;
            this.schema = schema;
            scopedConnectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                SearchPath = schema
            }.ConnectionString;
        }

        public static async Task<RemovalImpactFixture> CreateAsync()
        {
            string baseConnectionString = Environment.GetEnvironmentVariable(
                RequiresPostgresFactAttribute.ConnectionVariable)!;
            string schema = $"removal_impact_{Guid.NewGuid():N}";
            RemovalImpactFixture fixture = new(baseConnectionString, schema);

            await using NpgsqlConnection connection = new(baseConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE SCHEMA "{schema}";

                CREATE TABLE "{schema}".deployment
                (
                    monitor_id uuid NOT NULL
                );

                CREATE TABLE "{schema}".notification
                (
                    monitor_id uuid NOT NULL
                );

                CREATE TABLE "{schema}".rvt_alert_rule
                (
                    monitor_id uuid NOT NULL
                );

                CREATE TABLE "{schema}".monitor_measurement_removal_impact
                (
                    serial_id character varying(255) NOT NULL,
                    measurement_table_count bigint NOT NULL,
                    measurement_row_count bigint NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
            return fixture;
        }

        public async Task SeedAsync(
            IReadOnlyList<Guid> deploymentMonitorIds,
            IReadOnlyList<Guid> notificationMonitorIds,
            IReadOnlyList<Guid> alertRuleMonitorIds,
            IReadOnlyList<(string SerialId, int TableCount, int RowCount)> measurementImpacts)
        {
            await using NpgsqlConnection connection = new(scopedConnectionString);
            await connection.OpenAsync();
            foreach ((string table, IReadOnlyList<Guid> monitorIds) in new (string, IReadOnlyList<Guid>)[]
            {
                ("deployment", deploymentMonitorIds),
                ("notification", notificationMonitorIds),
                ("rvt_alert_rule", alertRuleMonitorIds)
            })
            {
                foreach (Guid monitorId in monitorIds)
                {
                    await using NpgsqlCommand insert = connection.CreateCommand();
                    insert.CommandText = $"INSERT INTO {table} (monitor_id) VALUES (@monitor_id);";
                    insert.Parameters.AddWithValue("monitor_id", monitorId);
                    await insert.ExecuteNonQueryAsync();
                }
            }

            foreach ((string serialId, int tableCount, int rowCount) in measurementImpacts)
            {
                await using NpgsqlCommand insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO monitor_measurement_removal_impact
                        (serial_id, measurement_table_count, measurement_row_count)
                    VALUES
                        (@serial_id, @table_count, @row_count);
                    """;
                insert.Parameters.AddWithValue("serial_id", serialId);
                insert.Parameters.AddWithValue("table_count", (long)tableCount);
                insert.Parameters.AddWithValue("row_count", (long)rowCount);
                await insert.ExecuteNonQueryAsync();
            }
        }

        public RVTDbContext CreateDomainContext() =>
            new(TestDbContexts.Npgsql<RVTDbContext>(scopedConnectionString));

        public RVTSearchContext CreateSearchContext() =>
            new(TestDbContexts.Npgsql<RVTSearchContext>(scopedConnectionString));

        public async ValueTask DisposeAsync()
        {
            await using NpgsqlConnection connection = new(baseConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"""DROP SCHEMA IF EXISTS "{schema}" CASCADE;""";
            await command.ExecuteNonQueryAsync();
        }
    }
}
