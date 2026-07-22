// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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
    // Function summary: Handles the health endpoint returns server time workflow for this module.
    public async Task HealthEndpoint_ReturnsServerTime()
    {
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("serverTimeUtc", body);
    }
}
