using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReportingMonitor.Api.Security;
using ReportingMonitor.Api.UseCases;
using Rvt.Monitor.Common.Configuration;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace ReportingMonitor.Api;

public static class ReportingMonitorApi
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        var services = endpoints.ServiceProvider;
        var filter = new InternalApiKeyFilter(
            services.GetRequiredService<ReportingMonitorOptions>(),
            services.GetRequiredService<IHostEnvironment>());

        endpoints.MapGet("/liveness", () => Results.Text(LivenessText(), "text/plain"));
        endpoints.MapGet("/readiness", async ([FromServices] IReportingHealthQueries healthQueries, CancellationToken cancellationToken) =>
        {
            var ready = await healthQueries.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            return ready
                ? Results.Ok(new { status = "ready" })
                : Results.Json(new { status = "not-ready" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        var reports = endpoints.MapGroup("/internal/reports").AddEndpointFilter(filter);
        reports.MapPost("/run-scheduled", GenerateScheduledAsync);
        reports.MapPost("/rules/{reportRuleId:guid}/generate", GenerateRuleAsync);
        reports.MapPost("/one-time", async (OneTimeReportRequest request, GenerateOneTimeReportHandler handler, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false));
            }
            catch (OneTimeReportValidationException exception)
            {
                return CreateOneTimeReportValidationProblem(exception);
            }
        });

        return endpoints;
    }

    internal static IResult CreateOneTimeReportValidationProblem(OneTimeReportValidationException exception) =>
        Results.ValidationProblem(exception.Errors
            .GroupBy(static error => error.Field)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static error => error.Message).ToArray()));

    internal static async Task<IResult> GenerateScheduledAsync(
        GenerateScheduledReportsHandler handler,
        CancellationToken cancellationToken)
    {
        var generated = await handler.HandleAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        return GeneratedReportsResult(generated);
    }

    internal static async Task<IResult> GenerateRuleAsync(
        Guid reportRuleId,
        RuleGenerationRequest? request,
        GenerateRuleReportHandler handler,
        CancellationToken cancellationToken)
    {
        var triggerUtc = request?.TriggerUtc ?? DateTimeOffset.UtcNow;
        var generated = await handler.HandleAsync(reportRuleId, triggerUtc, cancellationToken).ConfigureAwait(false);
        return GeneratedReportsResult(generated);
    }

    private static IResult GeneratedReportsResult(IReadOnlyList<GeneratedReport> generated) =>
        Results.Ok(new { generated.Count, reports = generated });

    private static string LivenessText() => RvtConfig.SERVICE_NAME + RvtConfig.SERVICE_VERSION;
}

public sealed record RuleGenerationRequest(DateTimeOffset? TriggerUtc);
