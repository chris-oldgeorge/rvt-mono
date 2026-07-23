namespace Rvt.Communication.Abstractions;

public interface ISmsDeliveryPort
{
    Task SendAsync(SmsDeliveryRequest request, CancellationToken cancellationToken = default);
}
