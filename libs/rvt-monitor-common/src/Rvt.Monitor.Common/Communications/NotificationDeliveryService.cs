namespace Rvt.Monitor.Common.Communications;

public sealed class NotificationDeliveryService(
    INotificationMessageComposer composer,
    IEmailDeliveryPort emailDelivery,
    ISmsDeliveryPort smsDelivery) : INotificationDeliveryService
{
    public Task SendAsync(
        NotificationDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Destination);

        var notification = composer.Compose(
            request.Kind,
            request.Channel,
            request.MonitorName,
            request.CallbackUrl);

        return request.Channel switch
        {
            NotificationChannel.Email => emailDelivery.SendAsync(
                new EmailDeliveryRequest(
                    request.Destination,
                    notification.Subject,
                    notification.PlainTextBody,
                    notification.HtmlBody,
                    []),
                cancellationToken),
            NotificationChannel.Sms => smsDelivery.SendAsync(
                new SmsDeliveryRequest(request.Destination, notification.PlainTextBody),
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Channel, "Unsupported channel.")
        };
    }
}
