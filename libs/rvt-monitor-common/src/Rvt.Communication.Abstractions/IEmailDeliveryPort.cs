namespace Rvt.Communication.Abstractions;

public interface IEmailDeliveryPort
{
    Task SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default);
}
