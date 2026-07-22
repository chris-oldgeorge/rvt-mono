using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Omnidots.Api.Http;
using Omnidots.Api.UseCases;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace Omnidots.Api;

// Summary: Maps the Omnidots monitor minimal API endpoints.
// Major updates:
// - 2026-07-12 DI composition: endpoint dependencies resolve from the host container.
// - 2026-07-15 HTTP hardening: bounded JSON, focused handlers, safe errors, and no-queue rate limits.
public static class MonitorApiEndpoints
{
    public static WebApplication MapOmnidotsMonitorApi(this WebApplication app)
    {
        app.UseRateLimiter();

        app.MapGet("/liveness", () => Results.Text(LivenessText(), "text/plain"));

        app.MapPost("/configure-measuring-point", ConfigureMeasuringPoint)
            .RequireRateLimiting(OmnidotsRateLimiterOptionsSetup.ConfigurePolicy);
        app.MapPost("/webhook", Webhook)
            .RequireRateLimiting(OmnidotsRateLimiterOptionsSetup.WebhookPolicy);

        return app;
    }

    private static string LivenessText() => RvtConfig.SERVICE_NAME + RvtConfig.SERVICE_VERSION;

    private static async Task<Results<Ok<ConfigureMeasuringPointResult>, ProblemHttpResult>> ConfigureMeasuringPoint(
        HttpRequest request,
        [FromServices] ConfigureMeasuringPointHandler handler,
        ILoggerFactory loggerFactory)
    {
        try
        {
            var body = await BoundedJsonRequestReader.ReadAsync(
                request,
                request.HttpContext.RequestAborted);
            return TypedResults.Ok(await handler.RunAsync(
                body,
                request.HttpContext.RequestAborted));
        }
        catch (OmnidotsUnsupportedMediaTypeException)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogWarning("Configure measuring point request rejected for unsupported media type.");
            return UnsupportedMediaTypeProblem();
        }
        catch (OmnidotsRequestBodyTooLargeException)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogWarning("Configure measuring point request rejected because the body is too large.");
            return RequestBodyTooLargeProblem();
        }
        catch (OmnidotsConfigurationAuthenticationException)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogWarning("Configure measuring point authentication rejected.");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized measuring point configuration request.");
        }
        catch (JsonException)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogWarning("Configure measuring point request rejected by validation.");
            return ConfigurationProblem();
        }
        catch (OmnidotsVendorConfigurationException)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogError("Configure measuring point vendor request failed.");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Measuring point vendor request failed.");
        }
        catch (OperationCanceledException) when (request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogError("Configure measuring point request failed.");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Measuring point configuration failed.");
        }
    }

    private static async Task<Results<Ok<ProcessWebhookResult>, ProblemHttpResult>> Webhook(
        HttpRequest request,
        [FromServices] ProcessWebhookHandler handler,
        ILoggerFactory loggerFactory)
    {
        try
        {
            var body = await BoundedJsonRequestReader.ReadAsync(
                request,
                request.HttpContext.RequestAborted);
            if (!request.Headers.TryGetValue(OmnidotsProtocol.SIGNATURE_HEADER, out var values) ||
                values.Count != 1)
            {
                throw new OmnidotsWebhookAuthenticationException();
            }

            await handler.RunAsync(
                body,
                values[0] ?? string.Empty,
                request.HttpContext.RequestAborted);
            return TypedResults.Ok(new ProcessWebhookResult(Processed: true));
        }
        catch (OmnidotsUnsupportedMediaTypeException)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogWarning("Webhook request rejected for unsupported media type.");
            return UnsupportedMediaTypeProblem();
        }
        catch (OmnidotsRequestBodyTooLargeException)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogWarning("Webhook request rejected because the body is too large.");
            return RequestBodyTooLargeProblem();
        }
        catch (OmnidotsWebhookAuthenticationException)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogWarning("Webhook authentication rejected.");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized webhook request.");
        }
        catch (JsonException)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogWarning("Authenticated webhook payload rejected as malformed JSON.");
            return WebhookValidationProblem();
        }
        catch (AdapterException)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogWarning("Authenticated webhook payload rejected by validation.");
            return WebhookValidationProblem();
        }
        catch (AlertTransientPersistenceException)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogWarning("Authenticated webhook persistence is temporarily unavailable.");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Webhook temporarily unavailable.");
        }
        catch (OperationCanceledException) when (request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            loggerFactory.CreateLogger("OmnidotsMonitorApi")
                .LogError("Authenticated webhook processing failed.");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Webhook processing failed.");
        }
    }

    private static ProblemHttpResult ConfigurationProblem() => TypedResults.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid measuring point configuration request.");

    private static ProblemHttpResult WebhookValidationProblem() => TypedResults.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid webhook payload.");

    private static ProblemHttpResult RequestBodyTooLargeProblem() => TypedResults.Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "Request body too large.");

    private static ProblemHttpResult UnsupportedMediaTypeProblem() => TypedResults.Problem(
        statusCode: StatusCodes.Status415UnsupportedMediaType,
        title: "Unsupported media type.");
}
