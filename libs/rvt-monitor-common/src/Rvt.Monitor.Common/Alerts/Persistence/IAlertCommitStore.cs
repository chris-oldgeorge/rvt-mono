namespace Rvt.Monitor.Common.Alerts.Persistence;

public interface IAlertCommitStore
{
    Task<AlertCommitResult> CommitAsync(
        AlertCommitRequest request,
        CancellationToken cancellationToken = default);
}
