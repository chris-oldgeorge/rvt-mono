// File summary: Covers regression tests for API host startup and React migration parity.
// Major updates:
// - 2026-07-26 pending Updated smoke-host database configuration for PostgreSQL-only startup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-07-22 pending Covered fail-fast production validation for the public SPA origin.

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RvtPortal.Spa.Adapters.Reporting;

using RvtPortal.Spa.Tests.Support;

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
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=rvt_portal_tests;Username=rvt;Password=test-only");
            builder.UseSetting("EmailConfiguration:SENDGRID_API_KEY", "test-sendgrid-api-key");
            builder.UseSetting("EmailConfiguration:Sending_Email_Address", "portal-tests@example.test");
        });
    }

    [Fact]
    // Function summary: Handles the swagger document is available workflow for this module.
    public async Task SwaggerDocument_IsAvailable()
    {
        HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("RVTmonitoring SPA API", body);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies liveness and readiness expose their distinct probe contracts.
    public async Task HealthEndpoints_ExposeLivenessAndReadiness()
    {
        using SpaTestApplicationFactory healthyFactory = new();
        HttpClient client = healthyFactory.CreateClient();

        using HttpResponseMessage liveness = await client.GetAsync("/api/health/live");
        using HttpResponseMessage readiness = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
        Assert.Contains("status", await liveness.Content.ReadAsStringAsync());
        Assert.Contains("checks", await readiness.Content.ReadAsStringAsync());
    }

    [RequiresPostgresFact]
    // Function summary: Verifies an unavailable dependency only fails readiness, never process liveness.
    public async Task HealthEndpoints_UnhealthyReadyDependencyFailsReadinessOnly()
    {
        // Dispose the SpaTestApplicationFactory itself, not just the derived factory: only the parent's
        // disposal drops the throwaway PostgreSQL schema.
        using SpaTestApplicationFactory parentFactory = new();
        using WebApplicationFactory<Program> unhealthyFactory = parentFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddHealthChecks().AddCheck(
                "forced readiness failure",
                () => HealthCheckResult.Unhealthy("test dependency unavailable"),
                tags: ["ready"]));
        });
        HttpClient client = unhealthyFactory.CreateClient();

        using HttpResponseMessage liveness = await client.GetAsync("/api/health/live");
        using HttpResponseMessage readiness = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        string body = await readiness.Content.ReadAsStringAsync();
        Assert.Contains("forced readiness failure", body, StringComparison.Ordinal);
        Assert.DoesNotContain("test dependency unavailable", body, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies report generation dependencies can be resolved from an API request scope.
    public void ReportGenerationClient_ResolvesFromScope()
    {
        using IServiceScope scope = factory.Services.CreateScope();

        IReportGenerationClient client = scope.ServiceProvider.GetRequiredService<IReportGenerationClient>();

        Assert.IsType<ReportingServiceReportGenerationClient>(client);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies production startup rejects a missing configured public SPA origin before serving requests.
    public void ProductionHost_WithoutPublicBaseUrl_FailsConfigurationValidation()
    {
        using SpaTestApplicationFactory productionFactory = new("Production");
        using WebApplicationFactory<Program> invalidHost = productionFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Spa:PublicBaseUrl", "");
            builder.UseSetting("AllowedHosts", "localhost;127.0.0.1");
            builder.UseSetting("RvtProduction:DataProtectionBlobUri", "https://storage.example.test/keys/key.xml");
        });

        Exception exception = Assert.ThrowsAny<Exception>(invalidHost.CreateClient);

        Assert.Contains("Spa:PublicBaseUrl", exception.ToString(), StringComparison.Ordinal);
    }
}
