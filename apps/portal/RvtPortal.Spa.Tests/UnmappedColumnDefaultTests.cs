// File summary: Guards the columns that exist physically but are mapped by no EF model.
// Major updates:
// - 2026-07-14 pending Added after rvt_alert_rule.created (NOT NULL, no default) broke every EF alert-rule insert.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// create_unmapped_schema.sql adds columns to tables the EF migrations create. EF does not know those columns
/// exist, so it never names them in an INSERT. A column like that can only be NOT NULL if the database supplies
/// the value itself - otherwise PostgreSQL rejects every EF insert into the table with 23502.
///
/// That is not hypothetical: rvt_alert_rule.created was NOT NULL with no default, and it broke alert level
/// creation and monitor creation (which seeds default alert levels) on PostgreSQL. The rest of the suite runs on
/// the EF InMemory and SQLite providers, which build their schema from the model - so the model's missing column
/// is simply absent from the test database, the insert succeeds, and the suite stays green while production
/// fails. Only the shipped SQL can reveal this, so these tests read it.
/// </summary>
public sealed class UnmappedColumnDefaultTests
{
    private const string PostgresConnectionVariable = "RVT_TEST_POSTGRES_CONNECTION";

    [Fact]
    // Function summary: Verifies every unmapped NOT NULL column has a database default EF can rely on.
    public void UnmappedNotNullColumns_HaveDatabaseDefaults()
    {
        var schemaSql = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "database", "postgres", "create_unmapped_schema.sql"));

        var offenders = ParseAddedColumns(StripSqlComments(schemaSql))
            .Where(column => column.IsNotNull && !column.HasDefault)
            .Select(column => column.Name)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "These columns are added to EF-created tables, are NOT NULL, and have no database default. EF maps " +
            "none of them, so it never supplies them on insert, and PostgreSQL will reject every EF insert into " +
            "the table with 23502. Give each one a DEFAULT, or map it onto the entity and populate it: " +
            string.Join(", ", offenders));
    }

    [RequiresPostgresFact]
    // Function summary: Verifies EF can insert a monitor and an alert rule into a real PostgreSQL schema.
    public async Task MonitorAndAlertRuleInserts_SucceedAgainstRealPostgres()
    {
        // Opt-in: CI has no PostgreSQL, so the check above is what guards the build. Point this at any real
        // database (dev, a restored backup, a from-scratch build) to prove EF's INSERT survives its schema.
        // Between them the two inserts cover both unmapped NOT NULL columns: monitor.battery_status, which no
        // EF code path inserts today, and rvt_alert_rule.created, which every alert level creation goes through.
        var connectionString = Environment.GetEnvironmentVariable(PostgresConnectionVariable);
        var options = new DbContextOptionsBuilder<RVTDbContext>().UseNpgsql(connectionString).Options;
        await using var context = new RVTDbContext(options);

        // Rolled back: this proves the schema accepts EF's INSERTs without leaving rows behind.
        await using var transaction = await context.Database.BeginTransactionAsync();

        var monitor = new MonitorEntity
        {
            SerialId = "TEST-UNMAPPED-DEFAULTS",
            Manufacturer = "Test",
            Model = "Test",
            FirmwareVersion = "0",
            TypeOfMonitor = MonitorTypeEnum.Dust,
            ListedAtTime = DateTime.UtcNow
        };
        context.MonitorsList.Add(monitor);

        context.RvtAlertRules.Add(new Alertlevel
        {
            MonitorId = monitor.Id,
            SerialId = monitor.SerialId,
            AlertField = "PM10",
            AlertType = AlertTypeEnum.Alert,
            AveragingPeriod = (int)AveragingPeriodsDustEnum._15_min,
            LimitOn = 10,
            LimitOff = 5,
            Weekdays = true,
            Saturdays = false,
            Sundays = false,
            IsActive = false,
            IsDeleted = false
        });

        // Throws DbUpdateException (23502) if an unmapped NOT NULL column has no default.
        await context.SaveChangesAsync();
        await transaction.RollbackAsync();
    }

    // Function summary: Reads the ADD COLUMN clauses that create_unmapped_schema.sql applies to EF-created tables.
    private static IEnumerable<AddedColumn> ParseAddedColumns(string sql)
    {
        const string marker = "ADD COLUMN IF NOT EXISTS ";

        // The CREATE TABLE statements in the same file are for tables EF does not map at all, so EF never inserts
        // into them and a bare NOT NULL there is fine. Only the ADD COLUMN clauses land on EF-created tables.
        var segments = sql.Split(marker, StringSplitOptions.None);

        // Segment 0 is whatever preceded the first marker, so it is not a column.
        for (var index = 1; index < segments.Length; index++)
        {
            var clause = ReadClause(segments[index]);
            var words = clause.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                continue;
            }

            yield return new AddedColumn(
                Name: words[0],
                IsNotNull: clause.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase),
                HasDefault: clause.Contains("DEFAULT", StringComparison.OrdinalIgnoreCase));
        }
    }

    // Function summary: Takes one column clause, stopping at the comma or semicolon that ends it.
    private static string ReadClause(string segment)
    {
        var depth = 0;
        var quoted = false;

        for (var i = 0; i < segment.Length; i++)
        {
            var character = segment[i];

            // A default like DEFAULT (now() AT TIME ZONE 'utc') carries both parentheses and a quoted literal,
            // and a comma inside either of them does not end the clause.
            if (character == '\'')
            {
                quoted = !quoted;
            }
            else if (!quoted && character == '(')
            {
                depth++;
            }
            else if (!quoted && character == ')')
            {
                depth--;
            }
            else if (!quoted && depth == 0 && (character == ',' || character == ';'))
            {
                return segment[..i];
            }
        }

        return segment;
    }

    private sealed record AddedColumn(string Name, bool IsNotNull, bool HasDefault);

    /// <summary>
    /// Marks a test that needs a real PostgreSQL database. xUnit v2 has no dynamic skip, so the decision is made
    /// here, at discovery: without a connection string the test is reported as skipped rather than quietly passing.
    /// </summary>
    private sealed class RequiresPostgresFactAttribute : FactAttribute
    {
        // Function summary: Skips the test unless a PostgreSQL connection string is configured.
        public RequiresPostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresConnectionVariable)))
            {
                Skip = $"Set {PostgresConnectionVariable} to run this against a real PostgreSQL database.";
            }
        }
    }

    // Function summary: Walks up from the test output directory to the repository root.
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RvtPortal.Spa.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
    }

    // Function summary: Removes line comments so commented-out DDL cannot satisfy or trip the parser.
    private static string StripSqlComments(string sql)
    {
        return string.Join(
            Environment.NewLine,
            sql.Split(["\r\n", "\n"], StringSplitOptions.None)
                .Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
    }
}
