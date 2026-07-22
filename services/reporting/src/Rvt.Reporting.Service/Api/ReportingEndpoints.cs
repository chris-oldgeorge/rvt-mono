using Rvt.Reporting.Core.Reports;

namespace Rvt.Reporting.Service.Api;

/// <summary>
/// Minimal internal API surface for scheduled, rule-specific, and one-time report generation.
/// Major updates: 2026-06-24 added ACS-compatible endpoints replacing Azure Function HTTP triggers.
/// </summary>
public static class ReportingEndpoints
{
    public static RouteGroupBuilder MapReportingEndpoints(this RouteGroupBuilder group)
    {
        var reports = group.MapGroup("/reports");

        reports.MapPost("/run-scheduled", async (IReportGenerationService service, CancellationToken cancellationToken) =>
        {
            var generated = await service.GenerateScheduledReportsAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { generated.Count, reports = generated });
        });

        reports.MapPost("/rules/{reportRuleId:guid}/generate", async (Guid reportRuleId, RuleGenerationRequest? request, IReportGenerationService service, CancellationToken cancellationToken) =>
        {
            var triggerUtc = request?.TriggerUtc ?? DateTimeOffset.UtcNow;
            var generated = await service.GenerateRuleAsync(reportRuleId, triggerUtc, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { generated.Count, reports = generated });
        });

        reports.MapPost("/one-time", async (OneTimeReportRequest request, IReportGenerationService service, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await service.GenerateOneTimeReportAsync(request, cancellationToken).ConfigureAwait(false));
            }
            catch (OneTimeReportValidationException exception)
            {
                return Results.ValidationProblem(exception.Errors.ToDictionary(error => error.Field, error => new[] { error.Message }));
            }
        });

        return group;
    }
}

public sealed record RuleGenerationRequest(DateTimeOffset? TriggerUtc);
