using Microsoft.Extensions.DependencyInjection;
using ReportingMonitor.Api.UseCases;
using Rvt.Monitor.Common.Scheduling;

namespace ReportingMonitor.Api;

public sealed class ReportingMonitorJobDispatcher : IMonitorJobDispatcher
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly GenerateScheduledReportsHandler? _scheduledReportsHandler;

    // Used only by Quartz schedule validation, which reads SupportedJobNames and never runs jobs.
    public ReportingMonitorJobDispatcher()
    {
    }

    public ReportingMonitorJobDispatcher(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    internal ReportingMonitorJobDispatcher(GenerateScheduledReportsHandler scheduledReportsHandler) =>
        _scheduledReportsHandler = scheduledReportsHandler;

    public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "GenerateScheduledReports"
    };

    public async Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
    {
        switch (jobName.Trim())
        {
            case "GenerateScheduledReports":
                if (_scheduledReportsHandler is not null)
                {
                    await _scheduledReportsHandler.HandleAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
                    return 0;
                }

                if (_scopeFactory is null)
                {
                    throw new InvalidOperationException(
                        "ReportingMonitorJobDispatcher was created without a service scope factory and cannot run jobs.");
                }

                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var handler = scope.ServiceProvider.GetRequiredService<GenerateScheduledReportsHandler>();
                    await handler.HandleAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
                }

                return 0;
            default:
                await Console.Error.WriteLineAsync($"Unknown reporting monitor job '{jobName}'.");
                return 2;
        }
    }
}
