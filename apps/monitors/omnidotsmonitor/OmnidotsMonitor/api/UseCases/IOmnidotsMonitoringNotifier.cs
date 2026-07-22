namespace Omnidots.Api.UseCases;

public interface IOmnidotsMonitoringNotifier
{
    Task SendNoDataWarningAsync(
        string recipient,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
