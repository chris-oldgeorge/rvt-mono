using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ReportingMonitor.Api;

public sealed class ReportingMonitorOptions
{
    public const string SectionName = "RVT";

    public bool EmailEnabled { get; init; } = true;
    public bool EmailTestMode { get; init; }
    public string? EmailTestReportToEmail { get; init; }
    public string? InternalApiKey { get; init; }
    public string? SpaBackendBaseUrl { get; init; }
    public string? SpaReportContentApiKey { get; init; }
    public bool AiSummaryEnabled { get; init; }
    public string AiSummaryBaseUrl { get; init; } = "http://localhost:11434";
    public string AiSummaryModel { get; init; } = "llama3.2";
    public int AiSummaryTimeoutSeconds { get; init; } = 8;

    public static ReportingMonitorOptions Bind(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        return new ReportingMonitorOptions
        {
            EmailEnabled = section.GetValue<bool?>("EMAIL_ENABLED") ?? true,
            EmailTestMode = section.GetValue<bool?>("EMAIL_TEST_MODE") ?? false,
            EmailTestReportToEmail = section["EMAIL_TEST_REPORT_TO_EMAIL"],
            InternalApiKey = section["INTERNAL_API_KEY"],
            SpaBackendBaseUrl = section["SPA_BACKEND_BASE_URL"],
            SpaReportContentApiKey = section["SPA_REPORT_CONTENT_API_KEY"],
            AiSummaryEnabled = section.GetValue<bool?>("AI_SUMMARY_ENABLED") ?? false,
            AiSummaryBaseUrl = section["AI_SUMMARY_BASE_URL"] ?? "http://localhost:11434",
            AiSummaryModel = section["AI_SUMMARY_MODEL"] ?? "llama3.2",
            AiSummaryTimeoutSeconds = section.GetValue<int?>("AI_SUMMARY_TIMEOUT_SECONDS") ?? 8
        };
    }

    public void Validate()
    {
        var failures = new List<string>();
        if (AiSummaryTimeoutSeconds <= 0)
        {
            failures.Add("AiSummaryTimeoutSeconds must be positive.");
        }

        if (AiSummaryEnabled && string.IsNullOrWhiteSpace(AiSummaryBaseUrl))
        {
            failures.Add("AiSummaryBaseUrl is required when AI summaries are enabled.");
        }

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(SectionName, typeof(ReportingMonitorOptions), failures);
        }
    }
}
