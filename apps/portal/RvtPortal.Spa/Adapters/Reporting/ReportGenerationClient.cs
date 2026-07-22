// File summary: Provides the outbound HTTP adapter for requesting manual report generation.
// Major updates:
// - 2026-07-08 pending Moved reporting-service HTTP integration into the reporting adapter namespace.
// - 2026-06-25 pending Counted returned report rows when the report service omits or returns zero generated count.
// - 2026-06-24 pending Wired manual report generation requests to the containerized reporting service.
// - 2026-06-24 pending Added deferred report-generation request handling ahead of report-service integration.

using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Adapters.Reporting;

public interface IReportGenerationClient
{
    // Function summary: Requests report generation for a saved report rule.
    Task<ReportGenerationRequestResponse?> RequestGenerationAsync(Guid reportRuleId, ReportGenerationRequest request, CancellationToken cancellationToken);
}

public sealed class ReportGenerationServiceOptions
{
    public string? BaseUrl { get; set; }
    public string? InternalApiKey { get; set; }
}

public sealed class ReportGenerationServiceException : InvalidOperationException
{
    // Function summary: Initializes this exception with the downstream status that should be returned to SPA callers.
    public ReportGenerationServiceException(string message, int statusCode = StatusCodes.Status502BadGateway)
        : base(message)
    {
        StatusCode = statusCode;
    }

    // Function summary: Initializes this exception with an inner HTTP exception and downstream status.
    public ReportGenerationServiceException(string message, Exception innerException, int statusCode = StatusCodes.Status502BadGateway)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed class ReportingServiceReportGenerationClient : IReportGenerationClient
{
    private const string InternalKeyHeaderName = "X-RVT-Internal-Key";
    private readonly HttpClient httpClient;
    private readonly ReportGenerationServiceOptions options;
    private readonly TimeProvider timeProvider;

    // Function summary: Initializes this type with HTTP and configuration dependencies for the report service.
    public ReportingServiceReportGenerationClient(
        HttpClient httpClient,
        IOptions<ReportGenerationServiceOptions> options,
        TimeProvider timeProvider)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.timeProvider = timeProvider;
    }

    // Function summary: Sends a manual report-rule generation request to the migrated reporting service.
    public async Task<ReportGenerationRequestResponse?> RequestGenerationAsync(Guid reportRuleId, ReportGenerationRequest request, CancellationToken cancellationToken)
    {
        if (request.SendToRecipients == false)
        {
            throw new ReportGenerationServiceException(
                "Manual report generation currently sends to the assigned report recipients. A no-email generation mode is not available yet.",
                StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new ReportGenerationServiceException("Report generation service URL is not configured.", StatusCodes.Status503ServiceUnavailable);
        }

        var triggerUtc = request.ReportDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(request.ReportDate.Value, DateTimeKind.Utc))
            : timeProvider.GetUtcNow();
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(options.BaseUrl, UriKind.Absolute), $"/internal/reports/rules/{reportRuleId}/generate"))
        {
            Content = JsonContent.Create(new ReportingServiceRuleGenerationPayload(triggerUtc))
        };

        if (!string.IsNullOrWhiteSpace(options.InternalApiKey))
        {
            httpRequest.Headers.TryAddWithoutValidation(InternalKeyHeaderName, options.InternalApiKey);
        }

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ReportGenerationServiceException($"Report generation service could not be reached: {exception.Message}", exception);
        }

        using (httpResponse)
        {
            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new ReportGenerationServiceException(
                    $"Report generation service returned {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}.",
                    StatusCodes.Status502BadGateway);
            }

            var serviceResponse = await httpResponse.Content.ReadFromJsonAsync<ReportingServiceRuleGenerationResponse>(cancellationToken);
            var generatedReports = serviceResponse?.Reports ?? [];
            var firstReport = generatedReports.FirstOrDefault();
            var generatedCount = GeneratedReportCount(serviceResponse, generatedReports);
            return new ReportGenerationRequestResponse
            {
                Id = firstReport?.ReportId ?? Guid.NewGuid(),
                ReportRuleId = reportRuleId,
                Status = generatedCount > 0 ? "Completed" : "Accepted",
                Message = BuildGenerationMessage(generatedCount),
                RequestedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };
        }
    }

    private static int GeneratedReportCount(ReportingServiceRuleGenerationResponse? serviceResponse, IReadOnlyCollection<ReportingServiceGeneratedReport> generatedReports)
    {
        if (serviceResponse is null)
        {
            return 0;
        }

        return serviceResponse.Count > 0 ? serviceResponse.Count : generatedReports.Count;
    }

    private static string BuildGenerationMessage(int generatedCount)
    {
        if (generatedCount == 1)
        {
            return "Report generation completed. 1 report generated.";
        }

        if (generatedCount > 1)
        {
            return $"Report generation completed. {generatedCount} reports generated.";
        }

        return "Report generation request was accepted, but no report was generated for the selected trigger date.";
    }

    private sealed record ReportingServiceRuleGenerationPayload(DateTimeOffset TriggerUtc);

    private sealed class ReportingServiceRuleGenerationResponse
    {
        public int Count { get; set; }
        public List<ReportingServiceGeneratedReport> Reports { get; set; } = [];
    }

    private sealed class ReportingServiceGeneratedReport
    {
        public Guid ReportId { get; set; }
    }
}
