using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReportingMonitor.Api;
using ReportingMonitor.Api.UseCases;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace ReportingMonitorTests;

public sealed class TestReportingDispatcher
{
    [Fact]
    public async Task RunAsync_GenerateScheduledReports_FromSingletonComposition_InvokesScopedGenerationService()
    {
        var service = new RecordingReportGenerationService();
        using var provider = ReportingServiceProviderFactory.Create(services =>
        {
            services.RemoveAll<IReportGenerationService>();
            services.AddScoped<IReportGenerationService>(_ => service);
        });
        var dispatcher = provider.GetRequiredService<ReportingMonitorJobDispatcher>();

        var result = await dispatcher.RunAsync("GenerateScheduledReports", CancellationToken.None);

        Assert.Equal(0, result);
        Assert.Equal(1, service.CallCount);
        Assert.NotNull(service.TriggerUtc);
    }

    [Fact]
    public async Task RunAsync_GenerateScheduledReports_UsesCurrentUtcTimeAndReturnsZero()
    {
        var service = new RecordingReportGenerationService();
        var dispatcher = new ReportingMonitorJobDispatcher(new GenerateScheduledReportsHandler(service));

        var result = await dispatcher.RunAsync("GenerateScheduledReports", CancellationToken.None);

        Assert.Equal(0, result);
        Assert.Equal(1, service.CallCount);
        Assert.NotNull(service.TriggerUtc);
        Assert.Equal(TimeSpan.Zero, service.TriggerUtc.Value.Offset);
    }

    [Fact]
    public async Task RunAsync_UnknownJob_ReturnsTwoWithParameterlessDispatcher()
    {
        var result = await new ReportingMonitorJobDispatcher()
            .RunAsync("GenerateAllReports", CancellationToken.None);

        Assert.Equal(2, result);
    }

    private sealed class RecordingReportGenerationService : IReportGenerationService
    {
        public int CallCount { get; private set; }

        public DateTimeOffset? TriggerUtc { get; private set; }

        public Task<IReadOnlyList<GeneratedReport>> GenerateScheduledReportsAsync(DateTimeOffset triggerUtc, CancellationToken cancellationToken)
        {
            CallCount++;
            TriggerUtc = triggerUtc;
            return Task.FromResult<IReadOnlyList<GeneratedReport>>([]);
        }

        public Task<IReadOnlyList<GeneratedReport>> GenerateRuleAsync(Guid reportRuleId, DateTimeOffset triggerUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<OneTimeReportResponse> GenerateOneTimeReportAsync(OneTimeReportRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
