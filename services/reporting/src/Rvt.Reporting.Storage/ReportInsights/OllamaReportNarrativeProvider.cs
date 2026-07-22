using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rvt.Reporting.Core.Reports;

namespace Rvt.Reporting.Storage.ReportInsights;

/// <summary>
/// Creates optional development report narratives through a local Ollama service with deterministic fallback.
/// Major updates: 2026-06-25 added ID15 dev AI narrative support.
/// </summary>
public sealed class OllamaReportNarrativeProvider : IReportNarrativeProvider
{
    private readonly HttpClient _httpClient;
    private readonly OllamaReportNarrativeOptions _options;

    public OllamaReportNarrativeProvider(HttpClient httpClient, OllamaReportNarrativeOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<string> CreateNarrativeAsync(ReportNarrativeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var fallback = ReportInsightBuilder.BuildDefaultNarrative(context.SiteName, context.ExecutiveSummary);
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.Model))
        {
            return fallback;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));
            var endpoint = new Uri(new Uri(_options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute), "api/generate");
            var response = await _httpClient.PostAsJsonAsync(endpoint, BuildRequest(context), timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return fallback;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            return document.RootElement.TryGetProperty("response", out var responseElement)
                ? EmptyToFallback(responseElement.GetString(), fallback)
                : fallback;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return fallback;
        }
        catch (HttpRequestException)
        {
            return fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
        catch (UriFormatException)
        {
            return fallback;
        }
    }

    private object BuildRequest(ReportNarrativeContext context)
    {
        return new
        {
            model = _options.Model,
            stream = false,
            prompt = $"""
                Write one concise executive-summary paragraph for an environmental monitoring report.
                Avoid inventing facts. Use only this compact JSON data:
                {JsonSerializer.Serialize(context, ReportNarrativeJsonContext.Default.ReportNarrativeContext)}
                """
        };
    }

    private static string EmptyToFallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed class OllamaReportNarrativeOptions
{
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string Model { get; set; } = "llama3.2";

    public int TimeoutSeconds { get; set; } = 8;
}

[JsonSerializable(typeof(ReportNarrativeContext))]
internal sealed partial class ReportNarrativeJsonContext : JsonSerializerContext;
