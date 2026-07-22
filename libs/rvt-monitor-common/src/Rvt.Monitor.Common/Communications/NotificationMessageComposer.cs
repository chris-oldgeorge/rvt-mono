namespace Rvt.Monitor.Common.Communications;

public sealed class NotificationMessageComposer : INotificationMessageComposer
{
    private const string HtmlTemplate = "<!DOCTYPE html>\r\n<html lang=\"en\">" +
        "<head>" +
        "<meta charset=\"UTF-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">" +
        "<meta http-equiv=\"X-UA-Compatible\" content=\"ie=edge\">" +
        "<title>RVT Cloud</title>" +
        "</head> " +
        "<body>{Content}" +
        "</body>" +
        "</html>";

    private static readonly IReadOnlyDictionary<(NotificationMessageKind, NotificationChannel), Template> Templates =
        new Dictionary<(NotificationMessageKind, NotificationChannel), Template>
        {
            [(NotificationMessageKind.Alert, NotificationChannel.Email)] = new(
                "Alert received from {Monitor}",
                string.Empty,
                Html("Your monitor has detected an alert above the safe limit set. <br/><br/>Click <a href='{callbackUrl}'>here</a> to view the details. <br/><br/>Many thanks From the RVT Group")),
            [(NotificationMessageKind.Alert, NotificationChannel.Sms)] = new(
                "Alert received from  {Monitor}",
                "Alert received from  {Monitor}" + Environment.NewLine +
                "Your monitor has detected an alert above the safe limit set." + Environment.NewLine +
                "Click here '{callbackUrl}' to view the details." + Environment.NewLine +
                "Many thanks From the RVT Group",
                string.Empty),
            [(NotificationMessageKind.Caution, NotificationChannel.Email)] = new(
                "Caution received from {Monitor}",
                string.Empty,
                Html("Your monitor has detected an alert above the safe limit set. <br/><br/>Click <a href='{callbackUrl}'>here</a> to view the details. <br/><br/>Many thanks From the RVT Group")),
            [(NotificationMessageKind.Caution, NotificationChannel.Sms)] = new(
                "Caution received from {Monitor}",
                "Caution received from {Monitor}" + Environment.NewLine +
                "Your monitor has detected an alert above the safe limit set." + Environment.NewLine +
                "Click here {callbackUrl} to view the details." + Environment.NewLine +
                "Many thanks From the RVT Group",
                string.Empty),
            [(NotificationMessageKind.Offline, NotificationChannel.Email)] = new(
                "Connectivity Alert. Your device {Monitor} has gone offline!",
                string.Empty,
                Html("No data has been received from the monitor for 24 hours.<br/><br/>")),
            [(NotificationMessageKind.Offline, NotificationChannel.Sms)] = new(
                "Connectivity Alert. Your device {Monitor} has gone offline!",
                "Connectivity Alert." + Environment.NewLine +
                "Your device {Monitor} has gone offline!" + Environment.NewLine +
                "No data has been received from the monitor for 24 hours.",
                string.Empty),
            [(NotificationMessageKind.BatteryCaution, NotificationChannel.Email)] = new(
                "Battery Caution for {Monitor}  ",
                string.Empty,
                Html("Your battery is near caution level.<br/><br/>Many thanks From the RVT Group<br/><br/>")),
            [(NotificationMessageKind.BatteryCaution, NotificationChannel.Sms)] = new(
                "Battery Caution ",
                "Battery Caution for {Monitor}" + Environment.NewLine +
                "Your battery is near caution level." + Environment.NewLine +
                "Many thanks From the RVT Group",
                string.Empty),
            [(NotificationMessageKind.BatteryAlert, NotificationChannel.Email)] = new(
                "Battery Alert for {Monitor}",
                string.Empty,
                Html("Your battery is at alert level.<br/><br/>Many thanks From the RVT Group<br/><br/>")),
            [(NotificationMessageKind.BatteryAlert, NotificationChannel.Sms)] = new(
                "Battery Alert.",
                "Battery Alert for {Monitor}" + Environment.NewLine +
                "Your battery is at alert level." + Environment.NewLine +
                "Many thanks From the RVT Group",
                string.Empty)
        };

    public ComposedNotification Compose(
        NotificationMessageKind kind,
        NotificationChannel channel,
        string monitorName,
        string callbackUrl)
    {
        ArgumentNullException.ThrowIfNull(monitorName);
        ArgumentNullException.ThrowIfNull(callbackUrl);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (!Enum.IsDefined(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }

        var template = Templates[(kind, channel)];
        return new ComposedNotification(
            Substitute(template.Subject, monitorName, callbackUrl),
            Substitute(template.PlainTextBody, monitorName, callbackUrl),
            Substitute(template.HtmlBody, monitorName, callbackUrl));
    }

    private static string Html(string content) => HtmlTemplate.Replace("{Content}", content, StringComparison.Ordinal);

    private static string Substitute(string value, string monitorName, string callbackUrl) => value
        .Replace("{Monitor}", monitorName, StringComparison.Ordinal)
        .Replace("{callbackUrl}", callbackUrl, StringComparison.Ordinal);

    private sealed record Template(string Subject, string PlainTextBody, string HtmlBody);
}
