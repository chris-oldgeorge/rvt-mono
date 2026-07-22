// File summary: Fails startup when the EF model maps to relations or columns the database does not have.
// Major updates:
// - 2026-07-14 pending Added so schema drift surfaces at boot instead of on the first request that touches it.


using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;

namespace RvtPortal.Spa;

/// <summary>
/// Verifies at startup that every relation and column the two EF models map to actually exists.
///
/// Without this, a model change - or a change to the canonical naming rules - can silently map an entity onto a
/// table or column that is not there, and nothing notices until a request touches it and returns a 500 from
/// deep inside a query. Checking at boot turns that into an immediate, legible failure listing exactly what is
/// missing. Non-relational providers (the InMemory test provider) have no information schema and are skipped.
/// </summary>
public sealed class SchemaValidationHostedService : IHostedService
{
    private readonly IServiceProvider services;
    private readonly ILogger<SchemaValidationHostedService> logger;

    // Function summary: Initializes the startup schema validation service.
    public SchemaValidationHostedService(IServiceProvider services, ILogger<SchemaValidationHostedService> logger)
    {
        this.services = services;
        this.logger = logger;
    }

    // Function summary: Validates both EF models against the live schema, throwing when anything is missing.
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();

        SchemaMismatch[] mismatches;
        try
        {
            // Resolving the contexts constructs the DbConnection, which can itself throw on a bad connection
            // string - so it belongs inside the guard, not outside it.
            var domainContext = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
            var searchContext = scope.ServiceProvider.GetRequiredService<RVTSearchContext>();

            mismatches = (await RvtSchemaValidator.ValidateAsync(domainContext, cancellationToken))
                .Concat(await RvtSchemaValidator.ValidateAsync(searchContext, cancellationToken))
                .ToArray();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Not being able to READ the schema is not what this guards against, and refusing to boot over it
            // would be worse than the problem it solves: a transient outage (or a misconfigured connection
            // string) would stop the app from starting at all, rather than letting it come up and report
            // unhealthy. Drift is the only thing worth failing on here, and drift can only be judged once the
            // schema has actually been read - so anything that stops us reading it downgrades to a warning.
            logger.LogWarning(
                exception,
                "Skipped startup schema validation: the database schema could not be read. Mapping drift will " +
                "not be detected until the next start that can reach it.");
            return;
        }

        if (mismatches.Length == 0)
        {
            logger.LogInformation("Schema validation passed: the EF models match the database.");
            return;
        }

        var detail = string.Join(Environment.NewLine + "  ", mismatches.Select(mismatch => mismatch.ToString()));
        throw new InvalidOperationException(
            $"The database does not match the EF model ({mismatches.Length} problem(s)). Apply the pending " +
            $"migrations and post-load scripts, or set Database:ValidateSchemaOnStartup=false to start anyway:" +
            Environment.NewLine + "  " + detail);
    }

    // Function summary: Nothing to tear down.
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
