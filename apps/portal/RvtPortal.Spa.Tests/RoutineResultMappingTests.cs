// File summary: Covers data-access mapping of canonical PostgreSQL routine result aliases.
// Major updates:
// - 2026-07-09 pending Added public repository coverage for canonical result aliases with legacy fallback.

using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;
using RVT.Entities;

namespace RvtPortal.Spa.Tests;

public sealed class RoutineResultMappingTests
{
    [Fact]
    // Function summary: Verifies monitor status routines prefer canonical PostgreSQL result aliases.
    public async Task MonitorStatusTimeCheckMapsCanonicalAliases()
    {
        var monitorDate = new DateTime(2026, 7, 9, 10, 0, 0);
        var utcDate = new DateTime(2026, 7, 9, 10, 5, 0);
        using var context = CreateContext();
        var repository = new MonitorRepository(
            context,
            FakeRoutineExecutor.FromRow(("monitor_date", monitorDate), ("utc_date", utcDate)));

        var result = await repository.MonitorStatusTimeCheck(Guid.NewGuid());

        Assert.Equal(monitorDate, result.MonitorDate);
        Assert.Equal(utcDate, result.UtcDate);
    }

    [Fact]
    // Function summary: Verifies monitor status routines still tolerate legacy result aliases during cutover.
    public async Task MonitorStatusTimeCheckFallsBackToLegacyAliases()
    {
        var monitorDate = new DateTime(2025, 1, 1, 12, 0, 0);
        var utcDate = new DateTime(2025, 1, 1, 12, 5, 0);
        using var context = CreateContext();
        var repository = new MonitorRepository(
            context,
            FakeRoutineExecutor.FromRow(("MonitorDate", monitorDate), ("UtcDate", utcDate)));

        var result = await repository.MonitorStatusTimeCheck(Guid.NewGuid());

        Assert.Equal(monitorDate, result.MonitorDate);
        Assert.Equal(utcDate, result.UtcDate);
    }

    [Fact]
    // Function summary: Verifies breach routines map canonical PostgreSQL result aliases to legacy DTO properties.
    public async Task BreachesAndAlertsMapCanonicalAliases()
    {
        // The values are arbitrary; the test's objective is that each canonical routine column is
        // mapped to the matching legacy DTO property, so every seeded value is named and reused.
        const string serialId = "VIB-001";
        const string fleetNumber = "F-100";
        const double xVtop = 1.1d;
        const double yVtop = 2.2d;
        const double zVtop = 3.3d;
        var monitorId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var sampleTime = new DateTime(2026, 7, 9, 9, 0, 0);
        var repository = new OmnidotsBreachesAndAlertsRepository(
            FakeRoutineExecutor.FromRow(
                ("serial_id", serialId),
                ("fleet_nr", fleetNumber),
                ("monitor_id", monitorId),
                ("sample_time", sampleTime),
                ("notification_id", notificationId),
                ("notification_time", sampleTime.AddMinutes(1)),
                ("x_vtop", xVtop),
                ("y_vtop", yVtop),
                ("z_vtop", zVtop)));

        var result = await repository.BreachesAndAlertsForDate(sampleTime);

        var row = Assert.Single(result);
        Assert.Equal(serialId, row.SerialID);
        Assert.Equal(fleetNumber, row.FleetNr);
        Assert.Equal(monitorId, row.MonitorId);
        Assert.Equal(notificationId, row.NotificationId);
        Assert.Equal(xVtop, row.XVtop);
        Assert.Equal(yVtop, row.YVtop);
        Assert.Equal(zVtop, row.ZVtop);
    }

    // Function summary: Creates an isolated EF context for repository construction without touching a real database.
    private static RVTDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new RVTDbContext(options);
    }

    private sealed class FakeRoutineExecutor : IRvtStoredRoutineExecutor
    {
        private readonly (string ColumnName, object? Value)[] columns;

        // Function summary: Initializes the fake executor with one routine result row.
        private FakeRoutineExecutor((string ColumnName, object? Value)[] columns)
        {
            this.columns = columns;
        }

        // Function summary: Builds a fake routine executor for a single result row.
        public static FakeRoutineExecutor FromRow(params (string ColumnName, object? Value)[] columns)
        {
            return new FakeRoutineExecutor(columns);
        }

        // Function summary: Executes the caller's mapper against the configured in-memory result row.
        public Task<IReadOnlyList<T>> QueryAsync<T>(
            string routineName,
            IEnumerable<RvtRoutineParameter> parameters,
            Func<DbDataReader, T> map,
            CancellationToken cancellationToken = default)
        {
            using var reader = CreateReader(columns);
            var rows = new List<T>();
            while (reader.Read())
            {
                rows.Add(map(reader));
            }

            return Task.FromResult<IReadOnlyList<T>>(rows);
        }

        // Function summary: Builds a data reader with predictable routine result-column names and values.
        private static DbDataReader CreateReader(params (string ColumnName, object? Value)[] columns)
        {
            var table = new DataTable();
            foreach (var column in columns)
            {
                table.Columns.Add(column.ColumnName, column.Value?.GetType() ?? typeof(object));
            }

            var row = table.NewRow();
            foreach (var column in columns)
            {
                row[column.ColumnName] = column.Value ?? DBNull.Value;
            }

            table.Rows.Add(row);
            return table.CreateDataReader();
        }
    }
}
