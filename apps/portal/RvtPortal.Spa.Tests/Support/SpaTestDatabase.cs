// File summary: Creates and drops the throwaway PostgreSQL schemas that back the Spa workflow test host.
// Major updates:
// - 2026-07-30 pending Added when SpaTestApplicationFactory replatformed from EF InMemory onto real PostgreSQL.

using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests.Support;

/// <summary>
/// Builds one throwaway PostgreSQL schema per Spa test host on the shared integration database
/// (<c>RVT__POSTGRES_INTEGRATION_CONNECTION</c>). Each schema holds the three EF models' tables - DDL derived
/// from the models themselves, the same shape the migrations produce, including the unique indexes the raw-SQL
/// upserts' ON CONFLICT clauses target - plus empty stand-in tables for the view-mapped search entities, so the
/// startup schema validator and any query against them behave exactly as they would against the deployed views
/// while the suite seeds nothing through them (it never could: they are keyless). SearchPath scoping keeps
/// parallel hosts fully isolated and leaves <c>public</c> (the migrated reference schema) untouched.
/// </summary>
internal static class SpaTestDatabase
{
    /// <summary>
    /// Generating the DDL requires building all three EF models, which costs about a second - once per test
    /// run, never per schema.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<string>> _schemaScripts = new(BuildSchemaScripts);

    // Function summary: Creates the named schema with every relation the portal models map, returning the scoped connection string.
    public static string CreateSchema(string schemaName)
    {
        string baseConnectionString = Environment.GetEnvironmentVariable(
                RequiresPostgresFactAttribute.ConnectionVariable)
            ?? throw new InvalidOperationException(
                $"Set {RequiresPostgresFactAttribute.ConnectionVariable} to run tests that boot the Spa test " +
                "host; gate them with [RequiresPostgresFact] so they skip instead of reaching this.");
        string scopedConnectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schemaName
        }.ConnectionString;

        Execute(baseConnectionString, $"CREATE SCHEMA \"{schemaName}\";");
        using NpgsqlConnection connection = new(scopedConnectionString);
        connection.Open();
        foreach (string script in _schemaScripts.Value)
        {
            using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = script;
            command.ExecuteNonQuery();
        }

        return scopedConnectionString;
    }

    // Function summary: Drops the schema and releases the pooled connections its scoped connection string accumulated.
    public static void DropSchema(string schemaName, string scopedConnectionString)
    {
        string baseConnectionString = Environment.GetEnvironmentVariable(
            RequiresPostgresFactAttribute.ConnectionVariable)!;
        Execute(baseConnectionString, $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE;");

        // Every schema carries a distinct connection string and therefore its own pool; without this the
        // pools of disposed hosts would sit on idle server connections until the test process exits.
        using NpgsqlConnection pooled = new(scopedConnectionString);
        NpgsqlConnection.ClearPool(pooled);
    }

    // Function summary: Runs one SQL statement on the unscoped integration connection.
    private static void Execute(string connectionString, string sql)
    {
        using NpgsqlConnection connection = new(connectionString);
        connection.Open();
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    // Function summary: Derives the per-schema DDL from the three EF models.
    private static IReadOnlyList<string> BuildSchemaScripts()
    {
        using RVTDbContext domain = new(TestDbContexts.ModelOnlyNpgsql<RVTDbContext>());
        using RVTSearchContext search = new(TestDbContexts.ModelOnlyNpgsql<RVTSearchContext>());
        using ApplicationDbContext identity = new(TestDbContexts.ModelOnlyNpgsql<ApplicationDbContext>());

        // The three models map disjoint table sets (the workflow suite would have collided long ago
        // otherwise), so the scripts run as-is with no cross-context deduplication.
        return
        [
            domain.Database.GenerateCreateScript(),
            search.Database.GenerateCreateScript(),
            identity.Database.GenerateCreateScript(),
            ViewStandInScript(domain, search),
            UnmappedNoiseRelationsScript(),
        ];
    }

    /// <summary>
    /// The two raw noise arrival tables. No portal EF model maps them - the monitors write them and the portal
    /// reads noise through Timescale aggregates - but portal SQL does read them directly: the site-archive
    /// exports and the deployment measurement probe. Without them here, that SQL cannot even parse in a test
    /// schema. Shapes match <c>database/postgres/create_unmapped_schema.sql</c>.
    /// </summary>
    public static string UnmappedNoiseRelationsScript()
    {
        return $"""
            CREATE TABLE {NoiseLevelColumns("air_q_noise_level")};
            CREATE TABLE {NoiseLevelColumns("svantek_noise_level")};
            """;
    }

    // Function summary: Returns the canonical column list shared by the two raw noise measurement relations.
    private static string NoiseLevelColumns(string tableName)
    {
        return $"""
            "{tableName}"
            (
                serial_id character varying(32) NOT NULL,
                sample_time timestamp without time zone NOT NULL,
                laeq double precision,
                lamax double precision,
                la_90 double precision,
                la_10 double precision,
                lceq double precision,
                lcmax double precision,
                lc_90 double precision,
                lc_10 double precision
            )
            """;
    }

    /// <summary>
    /// GenerateCreateScript covers tables only; entities mapped with ToView (the search read models the
    /// SchemaDeploy post-load scripts create in real databases) would otherwise be missing and fail the
    /// startup schema validator. A same-shaped empty table is indistinguishable from the deployed view for
    /// this suite: keyless entities cannot be seeded through EF, so these relations were always empty under
    /// the InMemory host too, and the dedicated Postgres adapter suites cover the real view semantics.
    /// </summary>
    private static string ViewStandInScript(params DbContext[] contexts)
    {
        StringBuilder script = new();
        foreach (DbContext context in contexts)
        {
            foreach (IEntityType entityType in context.Model.GetEntityTypes())
            {
                StoreObjectIdentifier? store = StoreObjectIdentifier.Create(entityType, StoreObjectType.View);
                if (store is null)
                {
                    continue;
                }

                List<string> columns = new();
                foreach (IProperty property in entityType.GetProperties())
                {
                    string? column = property.GetColumnName(store.Value);
                    if (column is null)
                    {
                        continue;
                    }

                    // Every column is nullable on purpose: the stand-ins hold no rows, and staying lax here
                    // means a future view shape change cannot break the test host for an unrelated reason.
                    columns.Add($"\"{column}\" {property.GetColumnType(store.Value)} NULL");
                }

                script.Append(
                    CultureInfo.InvariantCulture,
                    $"CREATE TABLE \"{store.Value.Name}\" ({string.Join(", ", columns)});\n");
            }
        }

        return script.ToString();
    }
}
