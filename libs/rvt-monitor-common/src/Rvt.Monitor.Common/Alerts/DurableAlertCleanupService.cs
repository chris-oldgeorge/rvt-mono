using Microsoft.Extensions.Options;
using Rvt.Monitor.Common.Alerts.Persistence;

namespace Rvt.Monitor.Common.Alerts;

public sealed class DurableAlertCleanupService(
    IAlertOutboxStore store,
    IOptions<DurableAlertOptions> options,
    TimeProvider timeProvider)
{
    public Task<int> CleanupAsync(DateTime utcNow, CancellationToken cancellationToken = default) =>
        store.DeleteCompletedBeforeAsync(
            utcNow.AddDays(-options.Value.CompletedRetentionDays),
            cancellationToken);

    public Task<int> CleanupAsync(CancellationToken cancellationToken = default) =>
        CleanupAsync(timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
}
