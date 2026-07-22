// File summary: Covers the SPA backend client that hands report generation requests to the reporting service.
// Major updates:
// - 2026-07-08 pending Updated report-generation client tests for the reporting adapter namespace.
// - 2026-06-25 pending Covered report generation responses that omit the generated count but include reports.
// - 2026-06-24 pending Added report-service HTTP contract coverage.

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Adapters.Reporting;

namespace RvtPortal.Spa.Tests;

public sealed class ReportGenerationClientTests
{
    [Fact]
    // Function summary: Verifies the report service client sends rule-generation requests with auth and trigger date.
    public async Task RequestGenerationAsync_SendsAuthenticatedRuleGenerationRequest()
    {
        const string baseUrl = "https://reports.internal";
        const string internalApiKey = "test-secret";
        const int reportCount = 1;
        var reportDate = new DateTime(2026, 6, 24);
        // triggerUtc is the report date serialized at UTC midnight in ISO-8601.
        var expectedTrigger = new DateTimeOffset(reportDate, TimeSpan.Zero)
            .ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

        var reportRuleId = Guid.NewGuid();
        var generatedReportId = Guid.NewGuid();
        using var handler = new CapturingHandler(new
        {
            count = reportCount,
            reports = new[]
            {
                new
                {
                    reportId = generatedReportId,
                    reportRuleId,
                    reportUri = "https://storage.example/report.pdf",
                    periodStartUtc = "2026-06-17T00:00:00Z",
                    periodEndUtc = "2026-06-23T23:59:59.999Z"
                }
            }
        });
        var httpClient = new HttpClient(handler);
        var client = new ReportingServiceReportGenerationClient(
            httpClient,
            Options.Create(new ReportGenerationServiceOptions
            {
                BaseUrl = baseUrl,
                InternalApiKey = internalApiKey
            }),
            TimeProvider.System);

        var response = await client.RequestGenerationAsync(reportRuleId, new ReportGenerationRequest
        {
            ReportDate = reportDate,
            SendToRecipients = true
        }, CancellationToken.None);

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal($"{baseUrl}/internal/reports/rules/{reportRuleId}/generate", handler.Request.RequestUri?.ToString());
        Assert.Equal(internalApiKey, handler.Request.Headers.GetValues("X-RVT-Internal-Key").Single());
        Assert.Equal(generatedReportId, response!.Id);
        Assert.Equal(reportRuleId, response.ReportRuleId);
        Assert.Equal("Completed", response.Status);
        Assert.Contains($"{reportCount} report", response.Message, StringComparison.OrdinalIgnoreCase);

        var payload = JsonSerializer.Deserialize<JsonElement>(handler.RequestBody);
        Assert.Equal(expectedTrigger, payload.GetProperty("triggerUtc").GetString());
    }

    [Fact]
    // Function summary: Verifies report generation uses the returned report rows when the service count is absent or zero.
    public async Task RequestGenerationAsync_UsesReportRowsWhenCountIsZero()
    {
        // The service omits the count, so the client derives it from the returned report rows.
        const int reportRowCount = 1;
        var reportRuleId = Guid.NewGuid();
        var generatedReportId = Guid.NewGuid();
        using var handler = new CapturingHandler(new
        {
            reports = new[]
            {
                new
                {
                    reportId = generatedReportId
                }
            }
        });
        var httpClient = new HttpClient(handler);
        var client = new ReportingServiceReportGenerationClient(
            httpClient,
            Options.Create(new ReportGenerationServiceOptions
            {
                BaseUrl = "https://reports.internal"
            }),
            TimeProvider.System);

        var response = await client.RequestGenerationAsync(reportRuleId, new ReportGenerationRequest
        {
            SendToRecipients = true
        }, CancellationToken.None);

        Assert.Equal(generatedReportId, response!.Id);
        Assert.Equal("Completed", response.Status);
        Assert.Contains($"{reportRowCount} report", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly object responseBody;

        public CapturingHandler(object responseBody)
        {
            this.responseBody = responseBody;
        }

        public HttpRequestMessage? Request { get; private set; }
        public string RequestBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseBody)
            };
        }
    }
}
