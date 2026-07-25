using System.Data;
using System.Data.Common;
using System.Reflection;
using Npgsql;
using Rvt.Monitor.Common.Data;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Rvt.Monitor.CommonTests.Data;

[TestClass]
public sealed class MonitorDbTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyIdentifierMap =
        new Dictionary<string, string>(StringComparer.Ordinal);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("postgres")]
    [DataRow(" POSTGRESQL ")]
    [DataRow("NPGSQL")]
    [DataRow("timescale")]
    [DataRow("TimescaleDB")]
    public void ValidateLegacyProvider_AcceptsOmittedAndPostgreSqlAliases(string? provider)
    {
        MonitorDb.ValidateLegacyProvider(provider, null);
    }

    [TestMethod]
    public void ValidateLegacyProvider_UsesPrimaryValueBeforeFallback()
    {
        MonitorDb.ValidateLegacyProvider("postgresql", "sqlserver");
    }

    [TestMethod]
    [DataRow("sqlserver")]
    [DataRow("MSSQL")]
    [DataRow("oracle")]
    public void ValidateLegacyProvider_RejectsUnsupportedValueWithGlobalSafeMessage(string provider)
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => MonitorDb.ValidateLegacyProvider(provider, null));

        Assert.AreEqual("PostgreSQL is the only supported database provider", exception.Message);
        Assert.DoesNotContain(provider, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void MonitorDbOptions_StoresOnlyIdentifierMap()
    {
        var options = new MonitorDbOptions(EmptyIdentifierMap);

        Assert.AreSame(EmptyIdentifierMap, options.IdentifierMap);
        CollectionAssert.AreEquivalent(
            new[] { nameof(MonitorDbOptions.IdentifierMap) },
            typeof(MonitorDbOptions)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray());
    }

    [TestMethod]
    [DoNotParallelize]
    public void FromEnvironment_ValidatesPrimaryThenFallbackAndStoresOnlyIdentifiers()
    {
        const string primaryKey = "RVT__DATABASE_PROVIDER";
        const string fallbackKey = "DatabaseProvider";
        var previousPrimary = Environment.GetEnvironmentVariable(primaryKey);
        var previousFallback = Environment.GetEnvironmentVariable(fallbackKey);
        var identifierMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["measurements"] = "air_q_noise_level"
        };

        try
        {
            Environment.SetEnvironmentVariable(primaryKey, "postgresql");
            Environment.SetEnvironmentVariable(fallbackKey, "sqlserver");

            var options = MonitorDbOptions.FromEnvironment(identifierMap);

            Assert.AreSame(identifierMap, options.IdentifierMap);
        }
        finally
        {
            Environment.SetEnvironmentVariable(primaryKey, previousPrimary);
            Environment.SetEnvironmentVariable(fallbackKey, previousFallback);
        }
    }

    [TestMethod]
    public void OpenConnection_UsesNpgsql()
    {
        var exception = Assert.ThrowsExactly<NpgsqlException>(() =>
            MonitorDb.OpenConnection(
                "Host=127.0.0.1;Port=1;Database=unreachable;Username=test;Password=test;Timeout=1"));

        Assert.IsNotNull(exception);
    }

    [TestMethod]
    public void CreateCommand_PreservesCanonicalSqlUnchanged()
    {
        const string sql = """
            SELECT id, offline
            FROM monitor
            WHERE offline = FALSE
              AND EXTRACT(DOW FROM @day) = 6;
            """;
        using DbConnection connection = new NpgsqlConnection();
        using var command = MonitorDb.CreateCommand(sql, connection);

        Assert.IsInstanceOfType<NpgsqlCommand>(command);
        Assert.AreEqual(sql, command.CommandText);
    }

    [TestMethod]
    public void AddWithValue_CreatesNpgsqlParameter()
    {
        using DbCommand command = new NpgsqlCommand();

        var parameter = command.Parameters.AddWithValue("@value", null);

        Assert.IsInstanceOfType<NpgsqlParameter>(parameter);
        Assert.AreEqual(DBNull.Value, parameter.Value);
    }

    [TestMethod]
    public void BulkInsert_RejectsUnsafeMappedTableBeforeOpeningConnection()
    {
        var options = new MonitorDbOptions(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["measurements"] = "air_q_noise_level; DROP TABLE monitor;--"
        });
        var table = new DataTable();
        table.Columns.Add("serial_id", typeof(string));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            MonitorDb.BulkInsert("not a connection string", "measurements", table, options));
    }

    [TestMethod]
    public void BulkInsert_RejectsUnsafeColumnBeforeOpeningConnection()
    {
        var options = new MonitorDbOptions(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["measurements"] = "air_q_noise_level"
        });
        var table = new DataTable();
        table.Columns.Add("serial_id; DROP TABLE monitor;--", typeof(string));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            MonitorDb.BulkInsert("not a connection string", "measurements", table, options));
    }

    [TestMethod]
    public void RequireMappedSqlIdentifier_ReturnsMappedIdentifierForAllowedKey()
    {
        var allowed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["noise"] = "air_q_noise_level",
            ["identity"] = "\"AspNetUsers\""
        };

        Assert.AreEqual(
            "air_q_noise_level",
            MonitorDb.RequireMappedSqlIdentifier("noise", allowed, "noise table"));
        Assert.AreEqual(
            "\"AspNetUsers\"",
            MonitorDb.RequireMappedSqlIdentifier("identity", allowed, "identity table"));
    }

    [TestMethod]
    public void RequireMappedSqlIdentifier_RejectsUnknownOrUnsafeMappedIdentifier()
    {
        var allowed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["noise"] = "air_q_noise_level",
            ["unsafe"] = "air_q_noise_level; DROP TABLE monitor;--"
        };

        Assert.ThrowsExactly<NotSupportedException>(
            () => MonitorDb.RequireMappedSqlIdentifier("unknown", allowed, "noise table"));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => MonitorDb.RequireMappedSqlIdentifier("unsafe", allowed, "noise table"));
    }

    [TestMethod]
    public void RequireSafeSqlIdentifier_AllowsCanonicalAndQuotedIdentifiers()
    {
        Assert.AreEqual(
            "air_q_noise_level",
            MonitorDb.RequireSafeSqlIdentifier("air_q_noise_level", "table"));
        Assert.AreEqual(
            "\"AspNetUsers\"",
            MonitorDb.RequireSafeSqlIdentifier("\"AspNetUsers\"", "table"));
        Assert.AreEqual(
            "monitoring.\"AspNetUsers\"",
            MonitorDb.RequireSafeSqlIdentifier("monitoring.\"AspNetUsers\"", "table"));
    }

    [TestMethod]
    public void RequireSafeSqlIdentifier_RejectsMalformedOrInjectedIdentifiers()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => MonitorDb.RequireSafeSqlIdentifier("\"AspNetUsers", "table"));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => MonitorDb.RequireSafeSqlIdentifier("monitor; DROP TABLE monitor;--", "table"));
    }

    [TestMethod]
    public void MonitorDb_ExposesNoRuntimeSqlRewriteEntryPoints()
    {
        var publicMethods = typeof(MonitorDb)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(method => method.Name)
            .ToArray();

        CollectionAssert.DoesNotContain(publicMethods, "ResolveProvider");
        CollectionAssert.DoesNotContain(publicMethods, "SelectProviderSql");
        CollectionAssert.DoesNotContain(publicMethods, "RewriteSql");
        CollectionAssert.DoesNotContain(publicMethods, "RewriteTableName");
        CollectionAssert.DoesNotContain(publicMethods, "RewriteIdentifier");
    }
}
