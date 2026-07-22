// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added SQL Server/PostgreSQL provider support.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-04 pending Added security hotspot regression coverage for PostgreSQL routine call validation.
// - 2026-06-09 pending Added canonical PostgreSQL routine-name mapping checks for the DBR routine port.
// - 2026-06-09 pending Added canonical SQL Server stored-procedure name mapping checks for the SQL Server cutover.

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RVT.DataAccess.Configuration;

namespace RvtPortal.Spa.Tests;

public sealed class DatabaseProviderConfigurationTests
{
    [Theory]
    [InlineData(null, RvtDatabaseProvider.SqlServer)]
    [InlineData("", RvtDatabaseProvider.SqlServer)]
    [InlineData("SqlServer", RvtDatabaseProvider.SqlServer)]
    [InlineData("MSSQL", RvtDatabaseProvider.SqlServer)]
    [InlineData("Postgres", RvtDatabaseProvider.Postgres)]
    [InlineData("PostgreSQL", RvtDatabaseProvider.Postgres)]
    [InlineData("Npgsql", RvtDatabaseProvider.Postgres)]
    // Function summary: Handles the parse provider accepts supported aliases workflow for this module.
    public void ParseProviderAcceptsSupportedAliases(string? value, RvtDatabaseProvider expectedProvider)
    {
        Assert.Equal(expectedProvider, RvtDatabaseOptions.ParseProvider(value));
    }

    [Fact]
    // Function summary: Handles the from configuration reads provider and connection string name workflow for this module.
    public void FromConfigurationReadsProviderAndConnectionStringName()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Postgres",
                ["Database:ConnectionStringName"] = "PostgresConnection",
                ["Database:PostgresRoutineSchema"] = "rvt",
                ["ConnectionStrings:PostgresConnection"] = "Host=localhost;Database=rvt;Username=rvt;Password=local"
            })
            .Build();

        var options = RvtDatabaseOptions.FromConfiguration(configuration);

        Assert.Equal(RvtDatabaseProvider.Postgres, options.Provider);
        Assert.Equal("PostgresConnection", options.ConnectionStringName);
        Assert.Equal("rvt", options.PostgresRoutineSchema);
        Assert.Equal("Host=localhost;Database=rvt;Username=rvt;Password=local", options.ConnectionString);
    }

    [Theory]
    [InlineData(RvtDatabaseProvider.SqlServer, "SqlServerOptionsExtension")]
    [InlineData(RvtDatabaseProvider.Postgres, "NpgsqlOptionsExtension")]
    // Function summary: Applies RVT database provider configures expected ef provider to the current configuration.
    public void UseRvtDatabaseProviderConfiguresExpectedEfProvider(
        RvtDatabaseProvider provider,
        string expectedOptionsExtensionName)
    {
        var builder = new DbContextOptionsBuilder();

        builder.UseRvtDatabaseProvider(new RvtDatabaseOptions
        {
            Provider = provider,
            ConnectionString = provider == RvtDatabaseProvider.SqlServer
                ? "Server=localhost;Database=rvt;User Id=rvt;Password=local;TrustServerCertificate=True;"
                : "Host=localhost;Database=rvt;Username=rvt;Password=local"
        });

        Assert.Contains(
            builder.Options.Extensions,
            extension => extension.GetType().Name.Contains(expectedOptionsExtensionName, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(RvtDatabaseProvider.SqlServer, "Microsoft.Data.SqlClient.SqlConnection")]
    [InlineData(RvtDatabaseProvider.Postgres, "Npgsql.NpgsqlConnection")]
    // Function summary: Handles the connection factory creates provider specific connections workflow for this module.
    public void ConnectionFactoryCreatesProviderSpecificConnections(
        RvtDatabaseProvider provider,
        string expectedConnectionType)
    {
        var factory = new RvtDatabaseConnectionFactory(new RvtDatabaseOptions
        {
            Provider = provider,
            ConnectionString = provider == RvtDatabaseProvider.SqlServer
                ? "Server=localhost;Database=rvt;User Id=rvt;Password=local;TrustServerCertificate=True;"
                : "Host=localhost;Database=rvt;Username=rvt;Password=local"
        });

        using var connection = factory.CreateConnection();

        Assert.Equal(expectedConnectionType, connection.GetType().FullName);
    }

    [Fact]
    // Function summary: Validates PostgreSQL routine names before they are used in dynamic command text.
    public void StoredRoutineExecutor_RejectsUnsafePostgresRoutineNames()
    {
        var executor = CreatePostgresRoutineExecutor();
        var method = typeof(RvtStoredRoutineExecutor).GetMethod("BuildPostgresRoutineName", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(executor, ["public.Routine;drop table Users"]));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    // Function summary: Validates routine parameter names before they are interpolated into PostgreSQL call text.
    public void StoredRoutineExecutor_RejectsUnsafeRoutineParameterNames()
    {
        var method = typeof(RvtStoredRoutineExecutor).GetMethod("NormalizeParameterName", BindingFlags.Static | BindingFlags.NonPublic)!;

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, ["siteId);drop table Users"]));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Theory]
    [InlineData("MonitorStatusTimeCheck", "\"public\".\"monitor_status_time_check\"")]
    [InlineData("MonitorStatusForMonth", "\"public\".\"monitor_status_for_month\"")]
    [InlineData("PeakRecordBreachAndAlerts", "\"public\".\"peak_record_breach_and_alerts\"")]
    [InlineData("public.MonitorStatusTimeCheck", "\"public\".\"monitor_status_time_check\"")]
    // Function summary: Maps legacy routine names to canonical PostgreSQL routine identifiers before quoting.
    public void StoredRoutineExecutor_MapsLegacyRoutineNamesToCanonicalPostgresNames(string routineName, string expectedRoutineName)
    {
        var executor = CreatePostgresRoutineExecutor();
        var method = typeof(RvtStoredRoutineExecutor).GetMethod("BuildPostgresRoutineName", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var postgresRoutineName = method.Invoke(executor, [routineName]);

        Assert.Equal(expectedRoutineName, postgresRoutineName);
    }

    [Theory]
    [InlineData("MonitorStatusTimeCheck", "[monitor_status_time_check]")]
    [InlineData("MonitorStatusForMonth", "[monitor_status_for_month]")]
    [InlineData("PeakRecordBreachAndAlerts", "[peak_record_breach_and_alerts]")]
    [InlineData("dbo.MonitorStatusTimeCheck", "[dbo].[monitor_status_time_check]")]
    // Function summary: Maps legacy routine names to canonical SQL Server stored-procedure identifiers before execution.
    public void StoredRoutineExecutor_MapsLegacyRoutineNamesToCanonicalSqlServerNames(string routineName, string expectedRoutineName)
    {
        var executor = CreateSqlServerRoutineExecutor();
        var method = typeof(RvtStoredRoutineExecutor).GetMethod("BuildSqlServerRoutineName", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var sqlServerRoutineName = method.Invoke(executor, [routineName]);

        Assert.Equal(expectedRoutineName, sqlServerRoutineName);
    }

    // Function summary: Builds a SQL Server routine executor for provider safety tests.
    private static RvtStoredRoutineExecutor CreateSqlServerRoutineExecutor()
    {
        var options = new RvtDatabaseOptions
        {
            Provider = RvtDatabaseProvider.SqlServer,
            ConnectionString = "Server=localhost;Database=rvt;User Id=sa;Password=Password1!;TrustServerCertificate=True"
        };

        return new RvtStoredRoutineExecutor(
            new RvtDatabaseConnectionFactory(options),
            Microsoft.Extensions.Options.Options.Create(options));
    }

    // Function summary: Builds a PostgreSQL routine executor for provider safety tests.
    private static RvtStoredRoutineExecutor CreatePostgresRoutineExecutor()
    {
        var options = new RvtDatabaseOptions
        {
            Provider = RvtDatabaseProvider.Postgres,
            ConnectionString = "Host=localhost;Database=rvt;Username=rvt;Password=local",
            PostgresRoutineSchema = "public"
        };

        return new RvtStoredRoutineExecutor(
            new RvtDatabaseConnectionFactory(options),
            Microsoft.Extensions.Options.Options.Create(options));
    }
}
