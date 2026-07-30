namespace MyAtm.Delivery;

/// <summary>
/// Purges completed delivery rows past their retention window. The three other
/// monitors run the shared <c>DurableAlertCleanupService</c> daily; MyAtm's own
/// outbox had no delete anywhere in production code, so completed rows
/// accumulated forever while <c>ClaimNextDueAsync</c> ordered over the growing
/// table every minute.
/// </summary>
public sealed class MonitorDeliveryCleanupService
{
    private readonly IMonitorDeliveryOutboxCommands _commands;
    private readonly MonitorDeliveryOptions _options;
    private readonly TimeProvider _timeProvider;

    public MonitorDeliveryCleanupService(
        IMonitorDeliveryOutboxCommands commands,
        MonitorDeliveryOptions options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _commands = commands;
        _options = options;
        _timeProvider = timeProvider;
    }

    public Task<int> CleanupAsync(DateTime utcNow, CancellationToken cancellationToken = default) =>
        _commands.DeleteCompletedBeforeAsync(
            _options.Producer,
            utcNow.AddDays(-_options.CompletedRetentionDays),
            cancellationToken);

    public Task<int> CleanupAsync(CancellationToken cancellationToken = default) =>
        CleanupAsync(_timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
}
