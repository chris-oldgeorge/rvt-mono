namespace Rvt.Monitor.Common.Communications;

public interface ISmsDeliveryPort
{
    Task SendAsync(
        SmsDeliveryRequest request,
        CancellationToken cancellationToken = default);
}
