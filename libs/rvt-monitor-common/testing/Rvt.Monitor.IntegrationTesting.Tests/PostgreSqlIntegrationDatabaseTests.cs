using Npgsql;
using Rvt.Monitor.IntegrationTesting;

namespace Rvt.Monitor.IntegrationTesting.Tests;

[TestClass]
[TestCategory("PostgreSqlIntegration")]
public sealed class PostgreSqlIntegrationDatabaseTests
{
    [TestMethod]
    public void ResolveAdminConnectionString_PrefersTheExplicitEnvironmentValue()
    {
        var connectionString = PostgreSqlIntegrationDatabase.ResolveAdminConnectionString(
            "Host=environment",
            "Host=development");

        Assert.AreEqual("Host=environment", connectionString);
    }

    [TestMethod]
    public void ResolveAdminConnectionString_UsesTheLocalDevelopmentValueWhenTheEnvironmentIsUnset()
    {
        var connectionString = PostgreSqlIntegrationDatabase.ResolveAdminConnectionString(
            environmentValue: null,
            developmentValue: "Host=development");

        Assert.AreEqual("Host=development", connectionString);
    }

    [TestMethod]
    public async Task CreateAsync_UsesGeneratedSchemaAsTheOnlySearchPath()
    {
        await using var database = await PostgreSqlIntegrationDatabase.CreateAsync(
            "CREATE TABLE probe (id integer PRIMARY KEY);", "TRUNCATE TABLE probe;");

        await using var connection = database.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SHOW search_path;", connection);

        Assert.AreEqual(database.SchemaName, (string?)await command.ExecuteScalarAsync());
    }

    [TestMethod]
    public async Task DisposeAsync_DropsOnlyTheGeneratedSchema()
    {
        string schemaName;
        await using (var database = await PostgreSqlIntegrationDatabase.CreateAsync(
            "CREATE TABLE probe (id integer PRIMARY KEY);", "TRUNCATE TABLE probe;"))
        {
            schemaName = database.SchemaName;
        }

        await using var connection = new NpgsqlConnection(
            PostgreSqlIntegrationDatabase.GetAdminConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = @schema);", connection);
        command.Parameters.AddWithValue("schema", schemaName);

        Assert.IsFalse((bool)(await command.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task FixtureCleanup_DropsItsOwnGeneratedSchema()
    {
        string schemaName;
        await using (var database = await PostgreSqlIntegrationDatabase.CreateAsync(
            "CREATE TABLE probe (id integer PRIMARY KEY);", "TRUNCATE TABLE probe;"))
        {
            schemaName = database.SchemaName;
        }

        await using var connection = new NpgsqlConnection(
            PostgreSqlIntegrationDatabase.GetAdminConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = @schema);", connection);
        command.Parameters.AddWithValue("schema", schemaName);

        Assert.IsFalse((bool)(await command.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task DisposeAsync_RetriesFailedDropAndIncludesSchemaNameInTheError()
    {
        var schemaName = "rvt_integration_dispose_retry";
        var attempts = 0;
        var database = new PostgreSqlIntegrationDatabase(
            "Host=unused", "Host=unused", schemaName, _ =>
            {
                attempts++;
                return attempts == 1
                    ? Task.FromException(new InvalidOperationException("drop failed"))
                    : Task.CompletedTask;
            });

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(database.DisposeAsync().AsTask);

        StringAssert.Contains(exception.Message, schemaName);
        await database.DisposeAsync();
        Assert.AreEqual(2, attempts);
    }
}
