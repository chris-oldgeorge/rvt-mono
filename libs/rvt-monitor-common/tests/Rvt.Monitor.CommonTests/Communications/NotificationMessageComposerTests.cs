using Rvt.Monitor.Common.Communications;

namespace Rvt.Monitor.CommonTests.Communications;

[TestClass]
public sealed class NotificationMessageComposerTests
{
    private const string MonitorName = "fleet-1";
    private const string CallbackUrl = "https://portal.example/Notification/View/1";
    private const string HtmlPrefix = "<!DOCTYPE html>\r\n<html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"><meta http-equiv=\"X-UA-Compatible\" content=\"ie=edge\"><title>RVT Cloud</title></head> <body>";
    private const string HtmlSuffix = "</body></html>";

    [DataTestMethod]
    [DynamicData(nameof(Templates), DynamicDataSourceType.Method)]
    public void Compose_PreservesLegacyTemplates(
        NotificationMessageKind kind,
        NotificationChannel channel,
        string expectedSubject,
        string expectedPlainText,
        string expectedHtml)
    {
        var result = new NotificationMessageComposer().Compose(
            kind,
            channel,
            MonitorName,
            CallbackUrl);

        Assert.AreEqual(expectedSubject, result.Subject);
        Assert.AreEqual(expectedPlainText, result.PlainTextBody);
        Assert.AreEqual(expectedHtml, result.HtmlBody);
    }

    [TestMethod]
    public void Compose_UndefinedKind_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new NotificationMessageComposer().Compose(
                (NotificationMessageKind)999,
                NotificationChannel.Email,
                MonitorName,
                CallbackUrl));
    }

    [TestMethod]
    public void Compose_UndefinedChannel_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new NotificationMessageComposer().Compose(
                NotificationMessageKind.Alert,
                (NotificationChannel)999,
                MonitorName,
                CallbackUrl));
    }

    public static IEnumerable<object[]> Templates()
    {
        var alertHtml = Html(
            "Your monitor has detected an alert above the safe limit set. <br/><br/>" +
            $"Click <a href='{CallbackUrl}'>here</a> to view the details. <br/><br/>" +
            "Many thanks From the RVT Group");
        yield return Row(
            NotificationMessageKind.Alert,
            NotificationChannel.Email,
            $"Alert received from {MonitorName}",
            string.Empty,
            alertHtml);
        yield return Row(
            NotificationMessageKind.Alert,
            NotificationChannel.Sms,
            $"Alert received from  {MonitorName}",
            $"Alert received from  {MonitorName}{Environment.NewLine}" +
            $"Your monitor has detected an alert above the safe limit set.{Environment.NewLine}" +
            $"Click here '{CallbackUrl}' to view the details.{Environment.NewLine}" +
            "Many thanks From the RVT Group",
            string.Empty);
        yield return Row(
            NotificationMessageKind.Caution,
            NotificationChannel.Email,
            $"Caution received from {MonitorName}",
            string.Empty,
            alertHtml);
        yield return Row(
            NotificationMessageKind.Caution,
            NotificationChannel.Sms,
            $"Caution received from {MonitorName}",
            $"Caution received from {MonitorName}{Environment.NewLine}" +
            $"Your monitor has detected an alert above the safe limit set.{Environment.NewLine}" +
            $"Click here {CallbackUrl} to view the details.{Environment.NewLine}" +
            "Many thanks From the RVT Group",
            string.Empty);
        yield return Row(
            NotificationMessageKind.Offline,
            NotificationChannel.Email,
            $"Connectivity Alert. Your device {MonitorName} has gone offline!",
            string.Empty,
            Html("No data has been received from the monitor for 24 hours.<br/><br/>"));
        yield return Row(
            NotificationMessageKind.Offline,
            NotificationChannel.Sms,
            $"Connectivity Alert. Your device {MonitorName} has gone offline!",
            $"Connectivity Alert.{Environment.NewLine}" +
            $"Your device {MonitorName} has gone offline!{Environment.NewLine}" +
            "No data has been received from the monitor for 24 hours.",
            string.Empty);
        yield return Row(
            NotificationMessageKind.BatteryCaution,
            NotificationChannel.Email,
            $"Battery Caution for {MonitorName}  ",
            string.Empty,
            Html("Your battery is near caution level.<br/><br/>Many thanks From the RVT Group<br/><br/>"));
        yield return Row(
            NotificationMessageKind.BatteryCaution,
            NotificationChannel.Sms,
            "Battery Caution ",
            $"Battery Caution for {MonitorName}{Environment.NewLine}" +
            $"Your battery is near caution level.{Environment.NewLine}" +
            "Many thanks From the RVT Group",
            string.Empty);
        yield return Row(
            NotificationMessageKind.BatteryAlert,
            NotificationChannel.Email,
            $"Battery Alert for {MonitorName}",
            string.Empty,
            Html("Your battery is at alert level.<br/><br/>Many thanks From the RVT Group<br/><br/>"));
        yield return Row(
            NotificationMessageKind.BatteryAlert,
            NotificationChannel.Sms,
            "Battery Alert.",
            $"Battery Alert for {MonitorName}{Environment.NewLine}" +
            $"Your battery is at alert level.{Environment.NewLine}" +
            "Many thanks From the RVT Group",
            string.Empty);
    }

    private static object[] Row(
        NotificationMessageKind kind,
        NotificationChannel channel,
        string subject,
        string plainText,
        string html) => [kind, channel, subject, plainText, html];

    private static string Html(string content) => $"{HtmlPrefix}{content}{HtmlSuffix}";
}
