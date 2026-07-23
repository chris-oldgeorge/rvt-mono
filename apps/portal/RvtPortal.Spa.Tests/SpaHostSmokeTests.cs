// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-07-22 pending Covered fail-fast production validation for the public SPA origin.

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RvtPortal.Spa.Adapters.Reporting;

namespace RvtPortal.Spa.Tests;

public class SpaHostSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    // Function summary: Initializes this type with the dependencies required by its workflow.
    public SpaHostSmokeTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("environment", "Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(localdb)\\mssqllocaldb;Database=RvtPortalSpaTests;Trusted_Connection=True;MultipleActiveResultSets=true");
        });
    }

    [Fact]
    // Function summary: Handles the swagger document is available workflow for this module.
    public async Task SwaggerDocument_IsAvailable()
    {
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("RVTmonitoring SPA API", body);
    }

    [Fact]
    // Function summary: Verifies liveness and readiness expose their distinct probe contracts.
    public async Task HealthEndpoints_ExposeLivenessAndReadiness()
    {
        using var healthyFactory = new SpaTestApplicationFactory();
        var client = healthyFactory.CreateClient();

        using var liveness = await client.GetAsync("/api/health/live");
        using var readiness = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
        Assert.Contains("status", await liveness.Content.ReadAsStringAsync());
        Assert.Contains("checks", await readiness.Content.ReadAsStringAsync());
    }

    [Fact]
    // Function summary: Verifies an unavailable dependency only fails readiness, never process liveness.
    public async Task HealthEndpoints_UnhealthyReadyDependencyFailsReadinessOnly()
    {
        using var unhealthyFactory = new SpaTestApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddHealthChecks().AddCheck(
                "forced readiness failure",
                () => HealthCheckResult.Unhealthy("test dependency unavailable"),
                tags: ["ready"]));
        });
        var client = unhealthyFactory.CreateClient();

        using var liveness = await client.GetAsync("/api/health/live");
        using var readiness = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        var body = await readiness.Content.ReadAsStringAsync();
        Assert.Contains("forced readiness failure", body, StringComparison.Ordinal);
        Assert.DoesNotContain("test dependency unavailable", body, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies report generation dependencies can be resolved from an API request scope.
    public void ReportGenerationClient_ResolvesFromScope()
    {
        using var scope = factory.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IReportGenerationClient>();

        Assert.IsType<ReportingServiceReportGenerationClient>(client);
    }

    [Fact]
    // Function summary: Verifies production startup rejects a missing configured public SPA origin before serving requests.
    public void ProductionHost_WithoutPublicBaseUrl_FailsConfigurationValidation()
    {
        using var productionFactory = new SpaTestApplicationFactory("Production");
        using var invalidHost = productionFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Spa:PublicBaseUrl", "");
            builder.UseSetting("AllowedHosts", "localhost;127.0.0.1");
            builder.UseSetting("RvtProduction:DataProtectionBlobUri", "https://storage.example.test/keys/key.xml");
        });

        var exception = Assert.ThrowsAny<Exception>(() => invalidHost.CreateClient());

        Assert.Contains("Spa:PublicBaseUrl", exception.ToString(), StringComparison.Ordinal);
    }
}
