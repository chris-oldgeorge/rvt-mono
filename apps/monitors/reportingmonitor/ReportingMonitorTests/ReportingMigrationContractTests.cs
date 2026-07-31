using System.Text.RegularExpressions;
using Npgsql;
using Rvt.Monitor.IntegrationTesting;

namespace ReportingMonitorTests;

/// <summary>
/// Holds the reporting monitor's PostgreSQL migration assets to the same contract the
/// other monitors are held to. Major updates: 2026-07-31 added alongside the scheduled
/// report period uniqueness backstop.
/// </summary>
public sealed partial class ReportingMigrationContractTests
{
    private const string _forwardScript = "2026-07-31-add-unique-scheduled-report-period.sql";
    private const string _rollbackScript = "2026-07-31-rollback-unique-scheduled-report-period.sql";

    /// <summary>
    /// Predates the dated forward/rollback naming this monitor now uses. It is named
    /// here rather than skipped by a pattern so that a new script cannot join it: the
    /// rollback-twin test asserts this is the whole exemption.
    /// </summary>
    private const string _legacyPrerequisiteScript = "reporting_service_prerequisites_20260625.sql";

    private static readonly string[] _supportedMigrations =
    [
        _forwardScript,
        _rollbackScript,
        _legacyPrerequisiteScript
    ];

    [Fact]
    public void MigrationAssets_ContainOnlyTheSupportedPostgreSqlScripts()
    {
        string?[] migrationFiles = [.. Directory
            .GetFiles(MigrationDirectory(), "*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(file => file, StringComparer.Ordinal)];

        Assert.Equal(_supportedMigrations, migrationFiles);
    }

    /// <summary>
    /// Transactionality is a property of every migration, not of the one that happened
    /// to get a test. A half-applied migration is the failure mode these scripts exist
    /// to avoid, so the assertion is made against the supported list itself: a new
    /// script inherits it by being listed.
    /// </summary>
    [Fact]
    public void EveryPostgreSqlMigration_IsWrappedInASingleTransaction()
    {
        foreach (string migration in _supportedMigrations)
        {
            string script = NormalizeSql(RemoveComments(ReadScript(migration)));

            Assert.StartsWith("BEGIN;", script, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("COMMIT;", script, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A forward migration without a rollback cannot be undone under pressure, which is
    /// when it matters. Rollback twins are named by inserting "rollback-" after the date.
    /// </summary>
    [Fact]
    public void EveryForwardPostgreSqlMigration_ShipsARollbackTwin()
    {
        string[] exempt = [.. _supportedMigrations.Where(file => !DatedMigrationPattern().IsMatch(file))];
        Assert.Equal([_legacyPrerequisiteScript], exempt);

        foreach (string migration in _supportedMigrations.Where(IsDatedForward))
        {
            Assert.Contains(RollbackTwinOf(migration), _supportedMigrations);
        }
    }

    /// <summary>
    /// Re-running a migration is what happens when a deploy is retried, so both scripts
    /// have to survive it. The duplicate cleanup is exercised with real losing rows,
    /// including report_sent rows that must survive being moved rather than orphaned.
    /// </summary>
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task PostgreSqlScripts_CollapseDuplicatesThenExecuteForwardAndRollbackIdempotently()
    {
        // The pre-migration shape: pk_report only, and no foreign key from report_sent.
        const string legacySchema = """
            CREATE TABLE report (
                id uuid NOT NULL,
                site_id uuid NOT NULL,
                report_rule_id uuid NULL,
                frequency integer NOT NULL,
                report_date timestamptz NOT NULL,
                report_from timestamptz NOT NULL,
                report_to timestamptz NOT NULL,
                report_link text NOT NULL,
                CONSTRAINT pk_report PRIMARY KEY (id)
            );

            CREATE TABLE report_sent (
                id uuid NOT NULL,
                report_id uuid NOT NULL,
                send_time timestamptz NOT NULL,
                address text NOT NULL,
                error_message text NULL,
                CONSTRAINT pk_report_sent PRIMARY KEY (id)
            );
            """;

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(45));
        await using PostgreSqlIntegrationDatabase database = await PostgreSqlIntegrationDatabase.CreateAsync(
            legacySchema,
            "SELECT 1;",
            timeout.Token);
        await SeedDuplicatesAsync(database, timeout.Token);

        string forward = ReadScript(_forwardScript);
        await ExecuteAsync(database, forward, timeout.Token);
        await ExecuteAsync(database, forward, timeout.Token);

        // The earliest report_date of the scheduled group survives; its two losing
        // copies are gone and the one-time pair is untouched.
        Assert.Equal("11111111-1111-1111-1111-111111111111", await ScalarAsync<string>(
            database,
            "SELECT id::text FROM report WHERE frequency = 1;",
            timeout.Token));
        Assert.Equal(2L, await ScalarAsync<long>(
            database,
            "SELECT COUNT(*) FROM report WHERE frequency = 5;",
            timeout.Token));
        // Every delivery record is still there, and every one of them now points at a
        // report that exists: history moved rather than being orphaned or deleted.
        Assert.Equal(4L, await ScalarAsync<long>(database, "SELECT COUNT(*) FROM report_sent;", timeout.Token));
        Assert.Equal(0L, await ScalarAsync<long>(
            database,
            "SELECT COUNT(*) FROM report_sent s WHERE NOT EXISTS (SELECT 1 FROM report r WHERE r.id = s.report_id);",
            timeout.Token));
        // All three deliveries of the collapsed group now hang off the surviving report.
        Assert.Equal(3L, await ScalarAsync<long>(
            database,
            "SELECT COUNT(*) FROM report_sent WHERE report_id = '11111111-1111-1111-1111-111111111111';",
            timeout.Token));

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            database,
            """
            INSERT INTO report (id, site_id, report_rule_id, frequency, report_date, report_from, report_to, report_link)
            VALUES (gen_random_uuid(), '44444444-4444-4444-4444-444444444444',
                    '22222222-2222-2222-2222-222222222222', 1,
                    now(), '2026-06-26 00:00+00', '2026-06-26 23:59+00', 'https://example.test/dup.pdf');
            """,
            timeout.Token));
        // One-time reports repeat over the same site and period by design, so the
        // filtered index must let a second one through.
        await ExecuteAsync(
            database,
            """
            INSERT INTO report (id, site_id, report_rule_id, frequency, report_date, report_from, report_to, report_link)
            VALUES (gen_random_uuid(), '44444444-4444-4444-4444-444444444444',
                    '33333333-3333-3333-3333-333333333333', 5,
                    now(), '2026-06-26 00:00+00', '2026-06-26 23:59+00', 'https://example.test/one-time-3.pdf');
            """,
            timeout.Token);

        string rollback = ReadScript(_rollbackScript);
        await ExecuteAsync(database, rollback, timeout.Token);
        await ExecuteAsync(database, rollback, timeout.Token);

        Assert.Equal(0L, await ScalarAsync<long>(
            database,
            "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = current_schema() "
            + "AND indexname = 'ux_report_scheduled_period';",
            timeout.Token));
        // The rollback drops the guard, not the data the forward migration kept.
        Assert.Equal(4L, await ScalarAsync<long>(database, "SELECT COUNT(*) FROM report_sent;", timeout.Token));
    }

    private static async Task SeedDuplicatesAsync(
        PostgreSqlIntegrationDatabase database,
        CancellationToken cancellationToken)
    {
        // One scheduled group of three: the keeper, a later duplicate, and a duplicate
        // that ties the keeper's report_date so the id tiebreak is the deciding rule.
        // Plus a one-time pair that shares everything and must survive intact.
        const string sql = """
            INSERT INTO report (id, site_id, report_rule_id, frequency, report_date, report_from, report_to, report_link)
            VALUES
                ('11111111-1111-1111-1111-111111111111', '44444444-4444-4444-4444-444444444444',
                 '22222222-2222-2222-2222-222222222222', 1,
                 '2026-06-27 06:00+00', '2026-06-26 00:00+00', '2026-06-26 23:59+00', 'https://example.test/keep.pdf'),
                ('99999999-9999-9999-9999-999999999999', '44444444-4444-4444-4444-444444444444',
                 '22222222-2222-2222-2222-222222222222', 1,
                 '2026-06-27 08:00+00', '2026-06-26 00:00+00', '2026-06-26 23:59+00', 'https://example.test/late.pdf'),
                ('88888888-8888-8888-8888-888888888888', '44444444-4444-4444-4444-444444444444',
                 '22222222-2222-2222-2222-222222222222', 1,
                 '2026-06-27 06:00+00', '2026-06-26 00:00+00', '2026-06-26 23:59+00', 'https://example.test/tie.pdf'),
                ('55555555-5555-5555-5555-555555555555', '44444444-4444-4444-4444-444444444444',
                 '33333333-3333-3333-3333-333333333333', 5,
                 '2026-06-27 06:00+00', '2026-06-26 00:00+00', '2026-06-26 23:59+00', 'https://example.test/one-time-1.pdf'),
                ('66666666-6666-6666-6666-666666666666', '44444444-4444-4444-4444-444444444444',
                 '33333333-3333-3333-3333-333333333333', 5,
                 '2026-06-27 07:00+00', '2026-06-26 00:00+00', '2026-06-26 23:59+00', 'https://example.test/one-time-2.pdf');

            INSERT INTO report_sent (id, report_id, send_time, address)
            VALUES
                ('aaaaaaaa-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', '2026-06-27 06:01+00', 'keep@example.test'),
                ('aaaaaaaa-0000-0000-0000-000000000002', '99999999-9999-9999-9999-999999999999', '2026-06-27 08:01+00', 'late@example.test'),
                ('aaaaaaaa-0000-0000-0000-000000000003', '88888888-8888-8888-8888-888888888888', '2026-06-27 06:01+00', 'tie@example.test'),
                ('aaaaaaaa-0000-0000-0000-000000000004', '55555555-5555-5555-5555-555555555555', '2026-06-27 06:01+00', 'one-time@example.test');
            """;

        await ExecuteAsync(database, sql, cancellationToken);
    }

    private static bool IsDatedForward(string migration) =>
        DatedMigrationPattern().IsMatch(migration) && !migration.Contains("-rollback-", StringComparison.Ordinal);

    private static string RollbackTwinOf(string migration)
    {
        const int datePrefixLength = 10;
        string date = migration[..datePrefixLength];
        string remainder = migration[(datePrefixLength + 1)..];
        if (remainder.StartsWith("add-", StringComparison.Ordinal))
        {
            remainder = remainder["add-".Length..];
        }

        return $"{date}-rollback-{remainder}";
    }

    private static async Task ExecuteAsync(
        PostgreSqlIntegrationDatabase database,
        string sql,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(sql, connection) { CommandTimeout = 30 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T> ScalarAsync<T>(
        PostgreSqlIntegrationDatabase database,
        string sql,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(sql, connection) { CommandTimeout = 30 };
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        Assert.NotNull(result);
        return (T)result;
    }

    private static string ReadScript(string fileName) =>
        File.ReadAllText(Path.Combine(MigrationDirectory(), fileName));

    private static string MigrationDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return Path.Combine(
                    directory.FullName,
                    "apps",
                    "monitors",
                    "reportingmonitor",
                    "database",
                    "postgres");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root from the test output directory.");
    }

    private static string RemoveComments(string script) =>
        LineCommentPattern().Replace(BlockCommentPattern().Replace(script, string.Empty), string.Empty);

    private static string NormalizeSql(string script) =>
        WhitespacePattern().Replace(script, " ").Trim();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}-", RegexOptions.CultureInvariant)]
    private static partial Regex DatedMigrationPattern();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BlockCommentPattern();

    [GeneratedRegex(@"--[^\r\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex LineCommentPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
