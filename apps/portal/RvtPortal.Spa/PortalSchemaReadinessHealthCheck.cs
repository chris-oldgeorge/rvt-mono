// File summary: Reports portal EF schema drift through the dependency readiness probe.
// Major updates:
// - 2026-07-23 Added a schema readiness check so deployments do not pass readiness with known model drift.

using Microsoft.Extensions.Diagnostics.HealthChecks;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;

namespace RvtPortal.Spa;

public sealed class PortalSchemaReadinessHealthCheck : IHealthCheck
{
    private readonly RVTDbContext domainContext;
    private readonly RVTSearchContext searchContext;
    private readonly ILogger<PortalSchemaReadinessHealthCheck> logger;

    // Function summary: Initializes schema readiness checking with the two EF models that map portal-owned relations.
    public PortalSchemaReadinessHealthCheck(
        RVTDbContext domainContext,
        RVTSearchContext searchContext,
        ILogger<PortalSchemaReadinessHealthCheck> logger)
    {
        this.domainContext = domainContext;
        this.searchContext = searchContext;
        this.logger = logger;
    }

    // Function summary: Reports an unhealthy dependency state when mapped schema relations cannot be validated.
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var domainMismatches = await RvtSchemaValidator.ValidateAsync(domainContext, cancellationToken);
            var searchMismatches = await RvtSchemaValidator.ValidateAsync(searchContext, cancellationToken);
            if (domainMismatches.Count == 0 && searchMismatches.Count == 0)
            {
                return HealthCheckResult.Healthy();
            }

            logger.LogError(
                "Portal readiness schema validation found {MismatchCount} model-to-database mismatch(es).",
                domainMismatches.Count + searchMismatches.Count);
            return HealthCheckResult.Unhealthy("Portal schema validation failed.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Portal readiness schema validation could not read the database schema.");
            return HealthCheckResult.Unhealthy("Portal schema validation could not complete.");
        }
    }
}
