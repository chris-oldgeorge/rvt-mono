using System.Net;
using System.Text;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Storage.ReportInsights;

namespace Rvt.Reporting.Core.Tests.Reports;

/// <summary>
/// Verifies dev AI narrative generation and deterministic fallback behavior.
/// Major updates: 2026-06-25 added ID15 Ollama narrative provider coverage.
/// </summary>
public sealed class ReportNarrativeProviderTests
{
    [Fact]
    public async Task OllamaProvider_ReturnsAiParagraphWhenEnabledAndServiceResponds()
    {
        OllamaReportNarrativeProvider provider = new(
            new HttpClient(new StubHandler("""{"response":"AI summary paragraph."}""", HttpStatusCode.OK)),
            new OllamaReportNarrativeOptions { Enabled = true, BaseUrl = "http://ollama.test", Model = "dev-model" });
        ReportNarrativeContext context = Context();

        string narrative = await provider.CreateNarrativeAsync(context, CancellationToken.None);

        Assert.Equal("AI summary paragraph.", narrative);
    }

    [Fact]
    public async Task OllamaProvider_ReturnsDeterministicParagraphWhenDisabled()
    {
        OllamaReportNarrativeProvider provider = new(
            new HttpClient(new StubHandler("""{"response":"unused"}""", HttpStatusCode.OK)),
            new OllamaReportNarrativeOptions { Enabled = false });
        ReportNarrativeContext context = Context();

        string narrative = await provider.CreateNarrativeAsync(context, CancellationToken.None);

        Assert.Contains("North Site", narrative, StringComparison.Ordinal);
        Assert.Contains("Dust", narrative, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OllamaProvider_ReturnsDeterministicParagraphWhenServiceFails()
    {
        OllamaReportNarrativeProvider provider = new(
            new HttpClient(new StubHandler("{}", HttpStatusCode.InternalServerError)),
            new OllamaReportNarrativeOptions { Enabled = true, BaseUrl = "http://ollama.test", Model = "dev-model" });
        ReportNarrativeContext context = Context();

        string narrative = await provider.CreateNarrativeAsync(context, CancellationToken.None);

        Assert.Contains("North Site", narrative, StringComparison.Ordinal);
        Assert.Contains("Dust", narrative, StringComparison.Ordinal);
    }

    private static ReportNarrativeContext Context()
    {
        DateTimeOffset fromUtc = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        ReportExecutiveSummary summary = new(
            fromUtc,
            fromUtc.AddDays(1),
            [
                new MonitorTypeExecutiveSummary(MonitorType.Dust, 1, 1, 0, DateOnly.FromDateTime(fromUtc.UtcDateTime), 1, 12, 1, ReportTrafficLightStatus.Red)
            ]);

        return new ReportNarrativeContext("North Site", summary, []);
    }

    private sealed class StubHandler(string content, HttpStatusCode statusCode) : HttpMessageHandler
    {
        private readonly string _content = content;
        private readonly HttpStatusCode _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            });
        }
    }
}
