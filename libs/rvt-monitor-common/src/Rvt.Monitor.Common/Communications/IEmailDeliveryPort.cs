namespace Rvt.Monitor.Common.Communications;

public interface IEmailDeliveryPort
{
    Task SendAsync(
        EmailDeliveryRequest request,
        CancellationToken cancellationToken = default);
}
