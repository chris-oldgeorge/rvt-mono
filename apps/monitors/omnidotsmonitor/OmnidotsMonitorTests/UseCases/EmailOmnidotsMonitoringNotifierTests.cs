using Omnidots.Api.UseCases;
using Rvt.Monitor.Common.Communications;

namespace OmnidotsAdapterTests.UseCases;

[TestClass]
public sealed class EmailOmnidotsMonitoringNotifierTests
{
    [TestMethod]
    public async Task SendNoDataWarningAsync_MapsExistingSubjectAndIsoBodyToEmailPort()
    {
        var port = new RecordingEmailPort();
        var notifier = new EmailOmnidotsMonitoringNotifier(port);
        var utcNow = new DateTime(2026, 7, 16, 12, 34, 56, DateTimeKind.Utc);

        await notifier.SendNoDataWarningAsync(
            "operations@example.test",
            utcNow,
            CancellationToken.None);

        var request = port.Requests.Single();
        Assert.AreEqual("operations@example.test", request.Recipient);
        Assert.AreEqual("Omnidots monitoring: no data for an hour!", request.Subject);
        Assert.AreEqual(
            "No data for any monitor detected at 2026-07-16T12:34:56.0000000Z",
            request.PlainTextBody);
        Assert.IsEmpty(request.Attachments);
    }

    [TestMethod]
    public async Task SendNoDataWarningAsync_PropagatesRequestedCancellation()
    {
        var port = new RecordingEmailPort();
        var notifier = new EmailOmnidotsMonitoringNotifier(port);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            notifier.SendNoDataWarningAsync(
                "operations@example.test",
                DateTime.UtcNow,
                cancellation.Token));
    }

    private sealed class RecordingEmailPort : IEmailDeliveryPort
    {
        public List<EmailDeliveryRequest> Requests { get; } = [];

        public Task SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }
}
