using System.Globalization;
using System.Text.Json;
using AirQ.Api.Security;
using AirQ.Api.UseCases;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rvt.Monitor.Common.Configuration;

namespace AirQ.Api;

// Summary: Maps the AirQ monitor minimal API endpoints.
// Major updates:
// - 2026-07-14 API key protection: import requests use a narrow date-import port.
public static class MonitorApiEndpoints
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapAirQMonitorApi(this IEndpointRouteBuilder endpoints)
    {
        var configuration = endpoints.ServiceProvider.GetRequiredService<IConfiguration>();
        var apiKey = configuration["RVT:MONITOR_API_KEY"]
            ?? configuration["RVT__MONITOR_API_KEY"];
        var apiKeyValidator = AirQApiKeyValidator.Create(apiKey);

        endpoints.MapGet("/liveness", () => Results.Text(LivenessText(), "text/plain"));

        endpoints.MapPost("/store-noise-levels-for-date",
            async context =>
            {
                var result = await StoreNoiseLevelsForDateAsync(context, apiKeyValidator);
                await result.ExecuteAsync(context);
            });

        return endpoints;
    }

    public static async Task<IResult> StoreNoiseLevelsForDateAsync(
        HttpContext context,
        AirQApiKeyValidator apiKeyValidator)
    {
        if (!apiKeyValidator.IsAuthorized(context.Request.Headers["X-Api-Key"]))
        {
            return Results.Unauthorized();
        }

        StoreNoiseLevelsForDateRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StoreNoiseLevelsForDateRequest>(
                context.Request.Body,
                RequestJsonOptions,
                context.RequestAborted);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        if (!TryGetCanonicalDate(request?.Date, out var canonicalDate))
        {
            return Results.BadRequest();
        }

        var importer = context.RequestServices.GetRequiredService<IAirQDateImporter>();
        importer.StoreNoiseLevelsForDate(canonicalDate);
        return Results.Ok();
    }

    private static bool TryGetCanonicalDate(string? value, out string canonicalDate)
    {
        canonicalDate = string.Empty;
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return false;
        }

        canonicalDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return StringComparer.Ordinal.Equals(value, canonicalDate);
    }

    private static string LivenessText() => RvtConfig.SERVICE_NAME + RvtConfig.SERVICE_VERSION;
}

public sealed record StoreNoiseLevelsForDateRequest(string? Date);
