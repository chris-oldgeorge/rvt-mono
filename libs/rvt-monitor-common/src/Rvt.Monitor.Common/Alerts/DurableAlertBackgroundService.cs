using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rvt.Monitor.Common.Hosting;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.Common.Alerts;

public sealed class DurableAlertBackgroundService : BackgroundService
{
    private readonly DurableAlertDispatcher dispatcher;
    private readonly DurableAlertCleanupService cleanupService;
    private readonly DurableAlertOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<DurableAlertBackgroundService> logger;
    private readonly bool isEnabled;
    private DateTime? lastCleanupDate;

    public DurableAlertBackgroundService(
        DurableAlertDispatcher dispatcher,
        DurableAlertCleanupService cleanupService,
        MonitorExecutionModeContext executionMode,
        IConfiguration configuration,
        IOptions<DurableAlertOptions> options,
        TimeProvider timeProvider,
        ILogger<DurableAlertBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(cleanupService);
        ArgumentNullException.ThrowIfNull(executionMode);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this.dispatcher = dispatcher;
        this.cleanupService = cleanupService;
        this.options = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
        isEnabled = executionMode.Mode == MonitorExecutionMode.Api &&
            !MonitorInfrastructureOptions.IsQuartzSchedulerEnabled(configuration);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!isEnabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunIterationAsync(timeProvider.GetUtcNow().UtcDateTime, stoppingToken);
            await Task.Delay(
                TimeSpan.FromSeconds(options.PollIntervalSeconds),
                timeProvider,
                stoppingToken);
        }
    }

    internal async Task RunIterationAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        if (!isEnabled)
        {
            return;
        }

        try
        {
            await dispatcher.DispatchAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Durable alert dispatch iteration failed ({ExceptionType}); the worker will continue.",
                exception.GetType().Name);
        }

        if (lastCleanupDate == utcNow.Date)
        {
            return;
        }

        lastCleanupDate = utcNow.Date;
        try
        {
            await cleanupService.CleanupAsync(utcNow, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Durable alert cleanup iteration failed ({ExceptionType}); the worker will continue.",
                exception.GetType().Name);
        }
    }
}
