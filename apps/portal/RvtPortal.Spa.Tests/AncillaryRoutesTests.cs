// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using System.Net;
using System.Text.Json;

namespace RvtPortal.Spa.Tests;

public class AncillaryRoutesTests
{
    [Theory]
    [InlineData("/test")]
    [InlineData("/test/exception")]
    [InlineData("/test/msgtest")]
    [InlineData("/test/blob")]
    [InlineData("/demo/dash")]
    [InlineData("/demo/installstatus")]
    [InlineData("/home/exception")]
    [InlineData("/home/reset")]
    // Function summary: Handles the retired mvc utility routes return safe not found problem details workflow for this module.
    public async Task RetiredMvcUtilityRoutes_ReturnSafeNotFoundProblemDetails(string path)
    {
        using var factory = new SpaTestApplicationFactory();
        var client = factory.CreateClient();

        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Legacy MVC utility route retired.", document.RootElement.GetProperty("title").GetString());
        Assert.True(document.RootElement.TryGetProperty("correlationId", out _));
        Assert.DoesNotContain("Exception test", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/test/send-test-ajax?email=operator%40rvt.test")]
    [InlineData("/api/test/msg-test")]
    [InlineData("/api/test/blob?upload=true")]
    [InlineData("/api/not-a-real-endpoint")]
    // Function summary: Handles the unknown API routes return problem details instead of SPA fallback workflow for this module.
    public async Task UnknownApiRoutes_ReturnProblemDetailsInsteadOfSpaFallback(string path)
    {
        using var factory = new SpaTestApplicationFactory();
        var client = factory.CreateClient();

        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("API endpoint not found.", document.RootElement.GetProperty("title").GetString());
        Assert.True(document.RootElement.TryGetProperty("correlationId", out _));
        Assert.DoesNotContain("RVTmonitoring SPA host is running", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    // Function summary: Handles the error endpoint returns safe problem details workflow for this module.
    public async Task ErrorEndpoint_ReturnsSafeProblemDetails()
    {
        using var factory = new SpaTestApplicationFactory();
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/error");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("An unexpected server error occurred.", document.RootElement.GetProperty("title").GetString());
        Assert.True(document.RootElement.TryGetProperty("correlationId", out _));
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
    }
}
