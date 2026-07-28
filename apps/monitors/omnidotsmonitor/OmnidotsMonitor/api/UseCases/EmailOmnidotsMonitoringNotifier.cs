using System.Globalization;
using Rvt.Communication.Abstractions;

namespace Omnidots.Api.UseCases;

public sealed class EmailOmnidotsMonitoringNotifier(IEmailDeliveryPort emailDelivery)
    : IOmnidotsMonitoringNotifier
{
    public Task SendNoDataWarningAsync(
        string recipient,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string body = $"No data for any monitor detected at {utcNow.ToString("O", CultureInfo.InvariantCulture)}";
        return emailDelivery.SendAsync(
            new EmailDeliveryRequest(
            recipient,
            "Omnidots monitoring: no data for an hour!",
            body,
            body,
            []),
            cancellationToken);
    }
}
