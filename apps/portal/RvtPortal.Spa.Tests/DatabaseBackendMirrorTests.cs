// File summary: Guards PostgreSQL and SQL Server database deployment scripts from drifting apart.
// Major updates:
// - 2026-06-18 pending Added monitor natural-key mirror checks for PostgreSQL and SQL Server.

using System.Text.RegularExpressions;

namespace RvtPortal.Spa.Tests;

public sealed class DatabaseBackendMirrorTests
{
    private static readonly NaturalKeyIndex[] NaturalKeyIndexes =
    [
        new("monitor", "ux_monitor_serial_id_type_of_monitor", ["serial_id", "type_of_monitor"]),
        new("air_q_monitor_status", "ux_air_q_monitor_status_serial_id", ["serial_id"]),
        new("omnidots_monitor_status", "ux_omnidots_monitor_status_serial_id", ["serial_id"]),
        new("omnidots_sensor", "ux_omnidots_sensor_serial_id", ["serial_id"]),
        new("svantek_monitor_status", "ux_svantek_monitor_status_serial_id", ["serial_id"]),
        new("air_q_noise_level", "ux_air_q_noise_level_serial_id_sample_time", ["serial_id", "sample_time"]),
        new("svantek_noise_level", "ux_svantek_noise_level_serial_id_sample_time", ["serial_id", "sample_time"]),
        new("my_atm_dust_level", "ux_my_atm_dust_level_serial_id_sample_time_avrg", ["serial_id", "sample_time", "avrg"]),
        new("my_atm_accessory_info", "ux_my_atm_accessory_info_serial_id_sample_time", ["serial_id", "sample_time"]),
        new("omnidots_peak_level", "ux_omnidots_peak_level_serial_id_sample_time", ["serial_id", "sample_time"]),
        new("omnidots_veff_level", "ux_omnidots_veff_level_serial_id_sample_time", ["serial_id", "sample_time"]),
        new("omnidots_vdv_level", "ux_omnidots_vdv_level_serial_id_sample_time", ["serial_id", "sample_time"]),
        new("air_q_noise_8_hour_average", "ux_air_q_noise_8_hour_average_serial_id_sample_time", ["serial_id", "sample_time"]),
        new("svantek_noise_8_hour_average", "ux_svantek_noise_8_hour_average_serial_id_sample_time", ["serial_id", "sample_time"])
    ];

    [Fact]
    // Function summary: Verifies monitor natural-key deployment scripts exist for both database providers.
    public void MonitorNaturalKeyDeploymentScripts_ExistForBothProviders()
    {
        foreach (var relativePath in NaturalKeyScriptPaths())
        {
            var path = Path.Combine(FindRepositoryRoot(), relativePath);
            Assert.True(File.Exists(path), $"Missing database deployment script: {relativePath}");
        }
    }

    [Fact]
    // Function summary: Verifies PostgreSQL and SQL Server scripts define the same monitor natural-key unique indexes.
    public void MonitorNaturalKeyDeploymentScripts_DefineSameUniqueIndexes()
    {
        var postgresSql = NormalizeSql(ReadRepositoryFile("database/postgres/monitor_natural_key_changes_20260618.sql"));
        var sqlServerSql = NormalizeSql(ReadRepositoryFile("database/sqlserver/monitor_natural_key_changes_20260618.sql"));

        foreach (var index in NaturalKeyIndexes)
        {
            var postgresColumns = string.Join(", ", index.Columns);
            var sqlServerColumns = string.Join(", ", index.Columns.Select(column => $"[{column}]"));

            Assert.Contains($"CREATE UNIQUE INDEX IF NOT EXISTS {index.IndexName}", postgresSql, StringComparison.Ordinal);
            Assert.Contains($"ON {index.Table} ({postgresColumns})", postgresSql, StringComparison.Ordinal);

            Assert.Contains($"CREATE UNIQUE INDEX [{index.IndexName}]", sqlServerSql, StringComparison.Ordinal);
            Assert.Contains($"ON dbo.[{index.Table}] ({sqlServerColumns})", sqlServerSql, StringComparison.Ordinal);
        }
    }

    [Fact]
    // Function summary: Verifies provider scripts populate the AirQ natural-key column before enforcing uniqueness.
    public void MonitorNaturalKeyDeploymentScripts_BackfillAirQStatusSerialId()
    {
        var postgresSql = NormalizeSql(ReadRepositoryFile("database/postgres/monitor_natural_key_changes_20260618.sql"));
        var sqlServerSql = NormalizeSql(ReadRepositoryFile("database/sqlserver/monitor_natural_key_changes_20260618.sql"));

        Assert.Contains("ALTER TABLE air_q_monitor_status ADD COLUMN IF NOT EXISTS serial_id varchar(64)", postgresSql, StringComparison.Ordinal);
        Assert.Contains("UPDATE air_q_monitor_status SET serial_id = id WHERE serial_id IS NULL AND id IS NOT NULL", postgresSql, StringComparison.Ordinal);

        Assert.Contains("ALTER TABLE dbo.[air_q_monitor_status] ADD [serial_id] nvarchar(64) NULL", sqlServerSql, StringComparison.Ordinal);
        Assert.Contains("UPDATE dbo.[air_q_monitor_status] SET [serial_id] = CONVERT(nvarchar(64), [id]) WHERE [serial_id] IS NULL AND [id] IS NOT NULL", sqlServerSql, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies both provider scripts audit the same monitor natural keys before unique indexes are applied.
    public void MonitorNaturalKeyAuditScripts_CheckSameNaturalKeys()
    {
        var postgresSql = ReadRepositoryFile("database/postgres/monitor_natural_key_changes_20260618.sql");
        var sqlServerSql = ReadRepositoryFile("database/sqlserver/monitor_natural_key_changes_20260618.sql");

        foreach (var index in NaturalKeyIndexes)
        {
            Assert.Contains(index.Table, postgresSql, StringComparison.Ordinal);
            Assert.Contains(index.Table, sqlServerSql, StringComparison.Ordinal);

            foreach (var column in index.Columns)
            {
                Assert.Contains(column, postgresSql, StringComparison.Ordinal);
                Assert.Contains(column, sqlServerSql, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [InlineData("database/postgres/monitor_natural_key_changes_20260618.sql")]
    [InlineData("database/sqlserver/monitor_natural_key_changes_20260618.sql")]
    // Function summary: Verifies scripts preserve removed duplicate rows before enforcing monitor natural keys.
    public void MonitorNaturalKeyDeploymentScripts_QuarantineKnownDuplicateTables(string relativePath)
    {
        var sql = ReadRepositoryFile(relativePath);
        var quarantineTables = new[]
        {
            "duplicate_quarantine_svantek_noise_level",
            "duplicate_quarantine_omnidots_peak_level",
            "duplicate_quarantine_svantek_noise_8_hour_average"
        };

        foreach (var quarantineTable in quarantineTables)
        {
            Assert.Contains(quarantineTable, sql, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> NaturalKeyScriptPaths()
    {
        yield return "database/postgres/monitor_natural_key_changes_20260618.sql";
        yield return "database/sqlserver/monitor_natural_key_changes_20260618.sql";
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var path = Path.Combine(FindRepositoryRoot(), relativePath);
        Assert.True(File.Exists(path), $"Missing repository file: {relativePath}");
        return File.ReadAllText(path);
    }

    private static string NormalizeSql(string sql)
    {
        return Regex.Replace(sql, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
    }

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

    private sealed record NaturalKeyIndex(string Table, string IndexName, string[] Columns);
}
