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
        string? connectionString = PostgreSqlIntegrationDatabase.ResolveAdminConnectionString(
            "Host=environment",
            "Host=development");

        Assert.AreEqual("Host=environment", connectionString);
    }

    [TestMethod]
    public void ResolveAdminConnectionString_UsesTheLocalDevelopmentValueWhenTheEnvironmentIsUnset()
    {
        string? connectionString = PostgreSqlIntegrationDatabase.ResolveAdminConnectionString(
            environmentValue: null,
            developmentValue: "Host=development");

        Assert.AreEqual("Host=development", connectionString);
    }

    [TestMethod]
    public async Task CreateAsync_UsesGeneratedSchemaAsTheOnlySearchPath()
    {
        await using PostgreSqlIntegrationDatabase database = await PostgreSqlIntegrationDatabase.CreateAsync(
            "CREATE TABLE probe (id integer PRIMARY KEY);", "TRUNCATE TABLE probe;", TestContext.CancellationToken);

        await using NpgsqlConnection connection = database.OpenConnection();
        await connection.OpenAsync(TestContext.CancellationToken);
        await using NpgsqlCommand command = new("SHOW search_path;", connection);

        Assert.AreEqual(database.SchemaName, (string?)await command.ExecuteScalarAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DisposeAsync_DropsOnlyTheGeneratedSchema()
    {
        string schemaName;
        await using (PostgreSqlIntegrationDatabase database = await PostgreSqlIntegrationDatabase.CreateAsync(
            "CREATE TABLE probe (id integer PRIMARY KEY);", "TRUNCATE TABLE probe;", TestContext.CancellationToken))
        {
            schemaName = database.SchemaName;
        }

        await using NpgsqlConnection connection = new(
            PostgreSqlIntegrationDatabase.GetAdminConnectionString());
        await connection.OpenAsync(TestContext.CancellationToken);
        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = @schema);", connection);
        command.Parameters.AddWithValue("schema", schemaName);

        Assert.IsFalse((bool)(await command.ExecuteScalarAsync(TestContext.CancellationToken))!);
    }

    [TestMethod]
    public async Task FixtureCleanup_DropsItsOwnGeneratedSchema()
    {
        string schemaName;
        await using (PostgreSqlIntegrationDatabase database = await PostgreSqlIntegrationDatabase.CreateAsync(
            "CREATE TABLE probe (id integer PRIMARY KEY);", "TRUNCATE TABLE probe;", TestContext.CancellationToken))
        {
            schemaName = database.SchemaName;
        }

        await using NpgsqlConnection connection = new(
            PostgreSqlIntegrationDatabase.GetAdminConnectionString());
        await connection.OpenAsync(TestContext.CancellationToken);
        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = @schema);", connection);
        command.Parameters.AddWithValue("schema", schemaName);

        Assert.IsFalse((bool)(await command.ExecuteScalarAsync(TestContext.CancellationToken))!);
    }

    [TestMethod]
    public async Task DisposeAsync_RetriesFailedDropAndIncludesSchemaNameInTheError()
    {
        string schemaName = "rvt_integration_dispose_retry";
        int attempts = 0;
        PostgreSqlIntegrationDatabase database = new(
            "Host=unused", "Host=unused", schemaName, _ =>
            {
                attempts++;
                return attempts == 1
                    ? Task.FromException(new InvalidOperationException("drop failed"))
                    : Task.CompletedTask;
            });

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(database.DisposeAsync().AsTask);

        Assert.Contains(schemaName, exception.Message);
        await database.DisposeAsync();
        Assert.AreEqual(2, attempts);
    }

    public TestContext TestContext { get; set; } = null!;
}
