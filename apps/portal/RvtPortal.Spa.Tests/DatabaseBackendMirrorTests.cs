// File summary: Guards PostgreSQL monitor natural-key deployment scripts and their database invariants.
// Major updates:
// - 2026-07-26 current Removed retired-provider mirror checks while retaining PostgreSQL deployment coverage.

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
    // Function summary: Verifies the PostgreSQL monitor natural-key deployment script exists.
    public void MonitorNaturalKeyDeploymentScript_ExistsForPostgres()
    {
        const string relativePath = "database/postgres/monitor_natural_key_changes_20260618.sql";
        string path = Path.Combine(FindRepositoryRoot(), relativePath);

        Assert.True(File.Exists(path), $"Missing database deployment script: {relativePath}");
    }

    [Fact]
    // Function summary: Verifies the PostgreSQL script defines monitor natural-key unique indexes.
    public void MonitorNaturalKeyDeploymentScript_DefinesUniqueIndexes()
    {
        string postgresSql = NormalizeSql(ReadRepositoryFile("database/postgres/monitor_natural_key_changes_20260618.sql"));

        foreach (NaturalKeyIndex index in NaturalKeyIndexes)
        {
            string postgresColumns = string.Join(", ", index.Columns);

            Assert.Contains($"CREATE UNIQUE INDEX IF NOT EXISTS {index.IndexName}", postgresSql, StringComparison.Ordinal);
            Assert.Contains($"ON {index.Table} ({postgresColumns})", postgresSql, StringComparison.Ordinal);
        }
    }

    [Fact]
    // Function summary: Verifies the PostgreSQL script populates the AirQ natural-key column before enforcing uniqueness.
    public void MonitorNaturalKeyDeploymentScript_BackfillsAirQStatusSerialId()
    {
        string postgresSql = NormalizeSql(ReadRepositoryFile("database/postgres/monitor_natural_key_changes_20260618.sql"));

        Assert.Contains("ALTER TABLE air_q_monitor_status ADD COLUMN IF NOT EXISTS serial_id varchar(64)", postgresSql, StringComparison.Ordinal);
        Assert.Contains("UPDATE air_q_monitor_status SET serial_id = id WHERE serial_id IS NULL AND id IS NOT NULL", postgresSql, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies the PostgreSQL script audits monitor natural keys before unique indexes are applied.
    public void MonitorNaturalKeyDeploymentScript_AuditsNaturalKeys()
    {
        string postgresSql = ReadRepositoryFile("database/postgres/monitor_natural_key_changes_20260618.sql");

        foreach (NaturalKeyIndex index in NaturalKeyIndexes)
        {
            Assert.Contains(index.Table, postgresSql, StringComparison.Ordinal);

            foreach (string column in index.Columns)
            {
                Assert.Contains(column, postgresSql, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [InlineData("database/postgres/monitor_natural_key_changes_20260618.sql")]
    // Function summary: Verifies the PostgreSQL script preserves removed duplicate rows before enforcing monitor natural keys.
    public void MonitorNaturalKeyDeploymentScript_QuarantinesKnownDuplicateTables(string relativePath)
    {
        string sql = ReadRepositoryFile(relativePath);
        string[] quarantineTables = new[]
        {
            "duplicate_quarantine_svantek_noise_level",
            "duplicate_quarantine_omnidots_peak_level",
            "duplicate_quarantine_svantek_noise_8_hour_average"
        };

        foreach (string? quarantineTable in quarantineTables)
        {
            Assert.Contains(quarantineTable, sql, StringComparison.Ordinal);
        }
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        string path = Path.Combine(FindRepositoryRoot(), relativePath);
        Assert.True(File.Exists(path), $"Missing repository file: {relativePath}");
        return File.ReadAllText(path);
    }

    private static string NormalizeSql(string sql)
    {
        return Regex.Replace(sql, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
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
