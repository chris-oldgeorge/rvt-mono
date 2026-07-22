namespace Rvt.Monitor.Common.Alerts;

public interface IAlertIngressPort
{
    Task<AlertIngressResult> AcceptAsync(
        AlertSignal signal,
        CancellationToken cancellationToken = default);
}
