// File summary: Covers the Portal's PostgreSQL-only configuration, connection, EF Core, and routine contracts.
// Major updates:
// - 2026-07-26 pending Replaced provider-selection coverage with PostgreSQL-only configuration and runtime guards.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System.Data;
using System.Data.Common;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

[Collection(DesignTimeDatabaseEnvironmentCollection.Name)]
public sealed class DatabaseProviderConfigurationTests
{
    private const string RetiredEngine = "Sql" + "Server";
    private const string RetiredEngineAlias = "MS" + "SQL";
    private const string ConnectionString =
        "Host=database.example;Database=rvt;Username=sentinel-user;Password=DO-NOT-LEAK-42";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("postgres")]
    [InlineData(" POSTGRES ")]
    [InlineData("PostgreSQL")]
    [InlineData("POSTGRESQL")]
    [InlineData("npgsql")]
    [InlineData("NPGSQL")]
    [InlineData("timescale")]
    [InlineData(" TIMESCALE ")]
    [InlineData("TimescaleDB")]
    // Function summary: Verifies omitted and PostgreSQL-compatible legacy provider settings remain accepted.
    public void FromConfiguration_AcceptsOmittedAndPostgresLegacyProviderAliases(string? provider)
    {
        IConfiguration configuration = BuildConfiguration(provider);

        RvtDatabaseOptions options = RvtDatabaseOptions.FromConfiguration(configuration);

        Assert.Equal(ConnectionString, options.ConnectionString);
    }

    [Theory]
    [InlineData("Database:Provider", RetiredEngine)]
    [InlineData("Database:Provider", RetiredEngineAlias)]
    [InlineData("Database:Provider", "oracle")]
    [InlineData("RvtDatabase:Provider", RetiredEngine)]
    // Function summary: Rejects every non-PostgreSQL legacy provider without exposing configured credentials.
    public void FromConfiguration_RejectsUnsupportedLegacyProviderWithoutCredentials(
        string providerKey,
        string provider)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [providerKey] = provider,
                ["Database:ConnectionString"] = ConnectionString
            })
            .Build();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => RvtDatabaseOptions.FromConfiguration(configuration));

        Assert.Equal("PostgreSQL is the only supported database provider", exception.Message);
        Assert.DoesNotContain(ConnectionString, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("DO-NOT-LEAK-42", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Reads connection, retry, timeout, schema-validation, and routine-schema settings.
    public void FromConfiguration_ReadsPostgresOptionsWithoutProviderSelection()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionStringName"] = "PostgresConnection",
                ["Database:PostgresRoutineSchema"] = "rvt",
                ["Database:EnableRetryOnFailure"] = "false",
                ["Database:MaxRetryCount"] = "4",
                ["Database:CommandTimeoutSeconds"] = "75",
                ["Database:ValidateSchemaOnStartup"] = "false",
                ["ConnectionStrings:PostgresConnection"] = ConnectionString
            })
            .Build();

        RvtDatabaseOptions options = RvtDatabaseOptions.FromConfiguration(configuration);

        Assert.Equal("PostgresConnection", options.ConnectionStringName);
        Assert.Equal(ConnectionString, options.ConnectionString);
        Assert.Equal("rvt", options.PostgresRoutineSchema);
        Assert.False(options.EnableRetryOnFailure);
        Assert.Equal(4, options.MaxRetryCount);
        Assert.Equal(75, options.CommandTimeoutSeconds);
        Assert.False(options.ValidateSchemaOnStartup);
    }

    [Fact]
    // Function summary: Creates only Npgsql connections from both supported connection factory entry points.
    public void ConnectionFactories_CreateNpgsqlConnections()
    {
        RvtDatabaseOptions options = CreateOptions();
        RvtDatabaseConnectionFactory factory = new RvtDatabaseConnectionFactory(options);

        using DbConnection factoryConnection = factory.CreateConnection();
        using DbConnection extensionConnection = options.CreateDbConnection();

        Assert.IsType<NpgsqlConnection>(factoryConnection);
        Assert.IsType<NpgsqlConnection>(extensionConnection);
        Assert.Equal("\"identifier\"\"part\"", factory.DelimitIdentifier("identifier\"part"));
    }

    [Fact]
    // Function summary: Keeps both UseRvtDatabaseProvider overloads while configuring Npgsql in each case.
    public void UseRvtDatabaseProvider_AlwaysConfiguresNpgsql()
    {
        RvtDatabaseOptions options = CreateOptions();
        DbContextOptionsBuilder connectionStringBuilder = new DbContextOptionsBuilder();
        DbContextOptionsBuilder sharedConnectionBuilder = new DbContextOptionsBuilder();
        using NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);

        connectionStringBuilder.UseRvtDatabaseProvider(options);
        sharedConnectionBuilder.UseRvtDatabaseProvider(options, connection);

        AssertNpgsql(connectionStringBuilder.Options);
        AssertNpgsql(sharedConnectionBuilder.Options);
        Assert.Equal(
            75,
            connectionStringBuilder.Options.Extensions.OfType<RelationalOptionsExtension>().Single().CommandTimeout);
        Assert.Equal(
            75,
            sharedConnectionBuilder.Options.Extensions.OfType<RelationalOptionsExtension>().Single().CommandTimeout);
    }

    [Fact]
    // Function summary: Keeps all three design-time contexts on Npgsql with independent migration histories.
    public void DesignTimeFactories_UseNpgsqlAndDistinctMigrationHistories()
    {
        string? previousConnection = Environment.GetEnvironmentVariable("RVT_EF_CONNECTION");
        Environment.SetEnvironmentVariable("RVT_EF_CONNECTION", ConnectionString);

        try
        {
            using RVTDbContext domainContext = new RVTDbContextDesignTimeFactory().CreateDbContext([]);
            using RVTSearchContext searchContext = new RVTSearchContextDesignTimeFactory().CreateDbContext([]);
            using ApplicationDbContext identityContext = new ApplicationDbContextDesignTimeFactory().CreateDbContext([]);

            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", domainContext.Database.ProviderName);
            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", searchContext.Database.ProviderName);
            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", identityContext.Database.ProviderName);
            Assert.Equal(HistoryRepository.DefaultTableName, GetMigrationsHistoryTable(domainContext));
            Assert.Equal(
                RvtDatabaseServiceCollectionExtensions.SearchMigrationsHistoryTable,
                GetMigrationsHistoryTable(searchContext));
            Assert.Equal(
                RvtDatabaseServiceCollectionExtensions.IdentityMigrationsHistoryTable,
                GetMigrationsHistoryTable(identityContext));
        }
        finally
        {
            Environment.SetEnvironmentVariable("RVT_EF_CONNECTION", previousConnection);
        }
    }

    [Fact]
    // Function summary: Fails EF tooling early with an actionable message when its connection is absent.
    public void DesignTimeOptions_WithoutConnection_ThrowsActionableFailure()
    {
        string? previousConnection = Environment.GetEnvironmentVariable("RVT_EF_CONNECTION");
        Environment.SetEnvironmentVariable("RVT_EF_CONNECTION", null);

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                RvtDesignTimeDatabaseOptions.FromEnvironment);

            Assert.Contains("RVT_EF_CONNECTION", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Password=", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RVT_EF_CONNECTION", previousConnection);
        }
    }

    [Fact]
    // Function summary: Configures PostgreSQL function SQL as a text command for every stored routine call.
    public void StoredRoutineExecutor_ConfiguresPostgresFunctionTextCommand()
    {
        RvtStoredRoutineExecutor executor = CreateRoutineExecutor();
        MethodInfo method = typeof(RvtStoredRoutineExecutor).GetMethod(
            "ConfigureCommand",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        using NpgsqlCommand command = new NpgsqlCommand();
        IReadOnlyCollection<RvtRoutineParameter> parameters =
        [
            new("siteId", Guid.Empty),
            new("@fromDate", DateTime.UnixEpoch)
        ];

        method.Invoke(executor, [command, "MonitorStatusTimeCheck", parameters]);

        Assert.Equal(CommandType.Text, command.CommandType);
        Assert.Equal(
            "select * from \"public\".\"monitor_status_time_check\"(@siteId, @fromDate)",
            command.CommandText);
    }

    [Fact]
    // Function summary: Validates PostgreSQL routine names before they are used in dynamic command text.
    public void StoredRoutineExecutor_RejectsUnsafePostgresRoutineNames()
    {
        RvtStoredRoutineExecutor executor = CreateRoutineExecutor();
        MethodInfo method = typeof(RvtStoredRoutineExecutor).GetMethod(
            "BuildPostgresRoutineName",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(executor, ["public.Routine;drop table Users"]));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    // Function summary: Validates routine parameter names before they are interpolated into PostgreSQL call text.
    public void StoredRoutineExecutor_RejectsUnsafeRoutineParameterNames()
    {
        MethodInfo method = typeof(RvtStoredRoutineExecutor).GetMethod(
            "NormalizeParameterName",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(null, ["siteId);drop table Users"]));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Theory]
    [InlineData("MonitorStatusTimeCheck", "\"public\".\"monitor_status_time_check\"")]
    [InlineData("MonitorStatusForMonth", "\"public\".\"monitor_status_for_month\"")]
    [InlineData("PeakRecordBreachAndAlerts", "\"public\".\"peak_record_breach_and_alerts\"")]
    [InlineData("public.MonitorStatusTimeCheck", "\"public\".\"monitor_status_time_check\"")]
    // Function summary: Maps legacy routine names to canonical PostgreSQL routine identifiers before quoting.
    public void StoredRoutineExecutor_MapsLegacyRoutineNamesToCanonicalPostgresNames(
        string routineName,
        string expectedRoutineName)
    {
        RvtStoredRoutineExecutor executor = CreateRoutineExecutor();
        MethodInfo method = typeof(RvtStoredRoutineExecutor).GetMethod(
            "BuildPostgresRoutineName",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        object? postgresRoutineName = method.Invoke(executor, [routineName]);

        Assert.Equal(expectedRoutineName, postgresRoutineName);
    }

    // Function summary: Builds configuration with an optional legacy provider value.
    private static IConfiguration BuildConfiguration(string? provider)
    {
        Dictionary<string, string?> values = new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = ConnectionString
        };
        if (provider is not null)
        {
            values["Database:Provider"] = provider;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    // Function summary: Builds representative PostgreSQL-only options for provider configuration tests.
    private static RvtDatabaseOptions CreateOptions()
    {
        return new RvtDatabaseOptions
        {
            ConnectionString = ConnectionString,
            CommandTimeoutSeconds = 75,
            PostgresRoutineSchema = "public"
        };
    }

    // Function summary: Builds the stored routine executor used by PostgreSQL safety tests.
    private static RvtStoredRoutineExecutor CreateRoutineExecutor()
    {
        RvtDatabaseOptions options = CreateOptions();
        return new RvtStoredRoutineExecutor(
            new RvtDatabaseConnectionFactory(options),
            Microsoft.Extensions.Options.Options.Create(options));
    }

    // Function summary: Asserts EF Core selected the Npgsql provider.
    private static void AssertNpgsql(DbContextOptions options)
    {
        Assert.Contains(
            options.Extensions,
            extension => extension.GetType().Name.Contains("NpgsqlOptionsExtension", StringComparison.Ordinal));
    }

    // Function summary: Reads the configured migrations-history table from a context's relational options.
    private static string GetMigrationsHistoryTable(DbContext context)
    {
        RelationalOptionsExtension extension = context.GetService<IDbContextOptions>()
            .Extensions
            .OfType<RelationalOptionsExtension>()
            .Single();

        return extension.MigrationsHistoryTableName ?? HistoryRepository.DefaultTableName;
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DesignTimeDatabaseEnvironmentCollection
{
    public const string Name = "Design-time database environment";
}
