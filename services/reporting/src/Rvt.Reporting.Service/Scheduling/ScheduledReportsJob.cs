using Quartz;
using Rvt.Reporting.Core.Reports;

namespace Rvt.Reporting.Service.Scheduling;

/// <summary>
/// Quartz job that replaces the Azure Function timer trigger for scheduled report generation.
/// Major updates: 2026-06-24 added non-overlapping singleton scheduler job.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ScheduledReportsJob : IJob
{
    public const string Name = "scheduled-reports";

    private static readonly Action<ILogger, DateTimeOffset, Exception?> LogStarted =
        LoggerMessage.Define<DateTimeOffset>(LogLevel.Information, new EventId(1000, nameof(LogStarted)), "Scheduled report generation started at {StartedAtUtc}");

    private static readonly Action<ILogger, int, Exception?> LogFinished =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1001, nameof(LogFinished)), "Scheduled report generation finished with {ReportCount} generated reports");

    private readonly IReportGenerationService _reportGenerationService;
    private readonly ILogger<ScheduledReportsJob> _logger;

    public ScheduledReportsJob(IReportGenerationService reportGenerationService, ILogger<ScheduledReportsJob> logger)
    {
        _reportGenerationService = reportGenerationService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        LogStarted(_logger, DateTimeOffset.UtcNow, null);
        var reports = await _reportGenerationService.GenerateScheduledReportsAsync(DateTimeOffset.UtcNow, context.CancellationToken).ConfigureAwait(false);
        LogFinished(_logger, reports.Count, null);
    }
}
