using Rvt.Monitor.Common.Data;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Rvt.Monitor.CommonTests.Data;

// Summary: Verifies shared monitor database provider selection and PostgreSQL SQL rewriting behavior.
// Major updates:
// - 2026-06-18 Canonical Timescale pass: added coverage for bracketed SQL Server syntax, canonical columns, booleans, DATEPART, and Identity alias preservation.
[TestClass]
public sealed class MonitorDbTests
{
    [TestMethod]
    public void ResolveProvider_DefaultsToPostgreSql()
    {
        Assert.AreEqual(MonitorDatabaseProvider.PostgreSql, MonitorDb.ResolveProvider(null, null));
    }

    [TestMethod]
    [DataRow("postgres")]
    [DataRow("postgresql")]
    [DataRow("timescale")]
    [DataRow("timescaledb")]
    public void ResolveProvider_AcceptsPostgreSqlAliases(string provider)
    {
        Assert.AreEqual(MonitorDatabaseProvider.PostgreSql, MonitorDb.ResolveProvider(provider, null));
    }

    [TestMethod]
    public void ResolveProvider_RejectsUnsupportedProvider()
    {
        var exception = Assert.ThrowsExactly<NotSupportedException>(() => MonitorDb.ResolveProvider("oracle", null));
        Assert.Contains("Unsupported monitor database provider", exception.Message);
    }

    [TestMethod]
    public void RewriteSql_UsesMonitorSpecificMapForPostgreSql()
    {
        var options = new MonitorDbOptions(
            MonitorDatabaseProvider.PostgreSql,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MonitorsList"] = "monitor",
                ["AirQNoiseLevels"] = "air_q_noise_level",
                ["AspNetUsers"] = "\"AspNetUsers\""
            });

        var sql = MonitorDb.RewriteSql(
            "SELECT * FROM dbo.MonitorsList m JOIN dbo.AirQNoiseLevels n ON n.SerialId = m.SerialId JOIN dbo.AspNetUsers u ON u.Id = m.UserId",
            options);

        Assert.AreEqual(
            "SELECT * FROM monitor m JOIN air_q_noise_level n ON n.serial_id = m.serial_id JOIN \"AspNetUsers\" u ON u.Id = m.user_id",
            sql);
    }

    [TestMethod]
    public void RewriteSql_RewritesBracketedSchemaAndCommonColumnsForPostgreSql()
    {
        var options = new MonitorDbOptions(
            MonitorDatabaseProvider.PostgreSql,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MonitorsList"] = "monitor",
                ["Deployments"] = "deployment",
                ["Contracts"] = "contract",
                ["Sites"] = "site"
            });

        var sql = MonitorDb.RewriteSql(
            @"SELECT M.[Id], [FleetNr], [SerialId], [TypeOfMonitor], M.Offline, SiteiD, SiteName
                FROM [dbo].[MonitorsList] M
                INNER JOIN [dbo].[Deployments] D ON D.MonitorId = M.Id AND D.EndDate IS NULL
                LEFT JOIN [dbo].[Contracts] C ON C.Id = D.ContractId
                LEFT JOIN [dbo].[Sites] S ON S.Id = C.SiteiD
               WHERE M.TypeOfMonitor = @TypeOfMonitor
                 AND LastDataTime15Min >= @LastDataTime
                 AND Offline = 0
                 AND DATEPART(dw,@day) = 7",
            options);

        Assert.AreEqual(
            @"SELECT M.id, fleet_row_count, serial_id, type_of_monitor, M.offline, site_id, site_name
                FROM monitor M
                INNER JOIN deployment D ON D.monitor_id = M.id AND D.end_date IS NULL
                LEFT JOIN contract C ON C.id = D.contract_id
                LEFT JOIN site S ON S.id = C.site_id
               WHERE M.type_of_monitor = @TypeOfMonitor
                 AND last_data_time_15_min >= @LastDataTime
                 AND offline = FALSE
                 AND (EXTRACT(DOW FROM @day) + 1) = 7",
            sql);
    }

    [TestMethod]
    public void RewriteSql_LeavesSqlServerTextUnchanged()
    {
        var options = new MonitorDbOptions(MonitorDatabaseProvider.SqlServer, new Dictionary<string, string>());
        const string sql = "SELECT * FROM dbo.MonitorsList";

        Assert.AreEqual(sql, MonitorDb.RewriteSql(sql, options));
    }

    [TestMethod]
    public void SelectProviderSql_ReturnsPostgreSqlTextForTimescale()
    {
        var options = new MonitorDbOptions(MonitorDatabaseProvider.PostgreSql, new Dictionary<string, string>());

        Assert.AreEqual("postgres", MonitorDb.SelectProviderSql("sqlserver", "postgres", options));
    }

    [TestMethod]
    public void SelectProviderSql_ReturnsSqlServerTextForSqlServer()
    {
        var options = new MonitorDbOptions(MonitorDatabaseProvider.SqlServer, new Dictionary<string, string>());

        Assert.AreEqual("sqlserver", MonitorDb.SelectProviderSql("sqlserver", "postgres", options));
    }

    [TestMethod]
    public void RequireMappedSqlIdentifier_ReturnsMappedIdentifierForAllowedKey()
    {
        var allowed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LAeq"] = "LAeq",
            ["PeakLevels"] = "[dbo].[OmnidotsPeakLevels]"
        };

        Assert.AreEqual("LAeq", MonitorDb.RequireMappedSqlIdentifier("LAeq", allowed, "noise column"));
        Assert.AreEqual("[dbo].[OmnidotsPeakLevels]", MonitorDb.RequireMappedSqlIdentifier("PeakLevels", allowed, "peak table"));
    }

    [TestMethod]
    public void RequireMappedSqlIdentifier_RejectsUnknownOrInjectedKey()
    {
        var allowed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LAeq"] = "LAeq"
        };

        Assert.ThrowsExactly<NotSupportedException>(
            () => MonitorDb.RequireMappedSqlIdentifier("LAeq); DROP TABLE dbo.MonitorsList;--", allowed, "noise column"));
        Assert.ThrowsExactly<NotSupportedException>(
            () => MonitorDb.RequireMappedSqlIdentifier("NotAColumn", allowed, "noise column"));
    }

    [TestMethod]
    public void RequireMappedSqlIdentifier_RejectsUnsafeMappedIdentifier()
    {
        var allowed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LAeq"] = "LAeq); DROP TABLE dbo.MonitorsList;--"
        };

        Assert.ThrowsExactly<InvalidOperationException>(
            () => MonitorDb.RequireMappedSqlIdentifier("LAeq", allowed, "noise column"));
    }

    [TestMethod]
    public void RequireSafeSqlIdentifier_AllowsSchemaQualifiedIdentifiers()
    {
        Assert.AreEqual("dbo.MonitorsList", MonitorDb.RequireSafeSqlIdentifier("dbo.MonitorsList", "table"));
        Assert.AreEqual("[dbo].[OmnidotsPeakLevels]", MonitorDb.RequireSafeSqlIdentifier("[dbo].[OmnidotsPeakLevels]", "table"));
    }

    [TestMethod]
    public void RequireSafeSqlIdentifier_RejectsMalformedOrInjectedIdentifiers()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => MonitorDb.RequireSafeSqlIdentifier("[dbo.OmnidotsPeakLevels", "table"));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => MonitorDb.RequireSafeSqlIdentifier("dbo.MonitorsList; DROP TABLE dbo.MonitorsList;--", "table"));
    }
}
