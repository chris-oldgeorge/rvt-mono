using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class DurableAlertDispatcherTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 15, 14, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public async Task DispatchAsync_WhenQueueIsEmpty_ClaimsOnceAndReturns()
    {
        var store = new Mock<IAlertOutboxStore>();
        store.Setup(x => x.ClaimNextDueAsync(UtcNow, TimeSpan.FromSeconds(120), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimedAlertDelivery?)null);
        var dispatcher = CreateDispatcher(store.Object, []);

        await dispatcher.DispatchAsync(CancellationToken.None);

        store.Verify(x => x.ClaimNextDueAsync(
            UtcNow,
            TimeSpan.FromSeconds(120),
            It.IsAny<CancellationToken>()), Times.Once);
        store.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DispatchAsync_StopsAtDefaultBatchCapOfFifty()
    {
        var message = CreateDelivery("MqttAlert", "alert");
        var store = new Mock<IAlertOutboxStore>();
        store.Setup(x => x.ClaimNextDueAsync(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        store.Setup(x => x.CompleteAsync(
                message.Id,
                message.LeaseId,
                UtcNow,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var adapter = CreateAdapter("MqttAlert");
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object]);

        await dispatcher.DispatchAsync(CancellationToken.None);

        store.Verify(x => x.ClaimNextDueAsync(
            UtcNow,
            TimeSpan.FromSeconds(120),
            It.IsAny<CancellationToken>()), Times.Exactly(50));
        adapter.Verify(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()), Times.Exactly(50));
        store.Verify(x => x.CompleteAsync(
            message.Id,
            message.LeaseId,
            UtcNow,
            null,
            It.IsAny<CancellationToken>()), Times.Exactly(50));
    }

    [TestMethod]
    public async Task DispatchAsync_SamplesFreshTimeBeforeEveryClaimAndAfterEveryDelivery()
    {
        var first = CreateDelivery("MqttAlert", "alert");
        var second = CreateDelivery("MqttAlert", "alert");
        var claims = new Queue<ClaimedAlertDelivery?>([first, second, null]);
        var store = new Mock<IAlertOutboxStore>();
        store.Setup(x => x.ClaimNextDueAsync(
                It.IsAny<DateTime>(),
                TimeSpan.FromSeconds(120),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => claims.Dequeue());
        store.Setup(x => x.CompleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var clock = new ManualTimeProvider(UtcNow);
        var adapter = CreateAdapter("MqttAlert");
        adapter.Setup(x => x.DeliverAsync(It.IsAny<ClaimedAlertDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<ClaimedAlertDelivery, CancellationToken>((message, _) =>
                clock.Advance(message.Id == first.Id ? TimeSpan.FromSeconds(45) : TimeSpan.FromSeconds(15)))
            .ReturnsAsync((AlertDeliveryAudit?)null);
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object], timeProvider: clock);

        await dispatcher.DispatchAsync(CancellationToken.None);

        store.Verify(x => x.ClaimNextDueAsync(
            UtcNow,
            TimeSpan.FromSeconds(120),
            It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.CompleteAsync(
            first.Id,
            first.LeaseId,
            UtcNow.AddSeconds(45),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.ClaimNextDueAsync(
            UtcNow.AddSeconds(45),
            TimeSpan.FromSeconds(120),
            It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.CompleteAsync(
            second.Id,
            second.LeaseId,
            UtcNow.AddSeconds(60),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.ClaimNextDueAsync(
            UtcNow.AddSeconds(60),
            TimeSpan.FromSeconds(120),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DispatchAsync_SelectsAdapterByExactKind()
    {
        var message = CreateDelivery("Email", "ops@example.test");
        var store = StoreWithSingleClaim(message);
        var mqttAdapter = CreateAdapter("MqttAlert");
        var emailAdapter = CreateAdapter("Email");
        var dispatcher = CreateDispatcher(store.Object, [mqttAdapter.Object, emailAdapter.Object]);

        await dispatcher.DispatchAsync(CancellationToken.None);

        emailAdapter.Verify(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        mqttAdapter.Verify(x => x.DeliverAsync(It.IsAny<ClaimedAlertDelivery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DispatchAsync_WhenAdapterIsMissing_UsesBoundedRetryPath()
    {
        var message = CreateDelivery("Unknown", "private-destination");
        var store = StoreWithSingleClaim(message);
        var dispatcher = CreateDispatcher(store.Object, []);

        await dispatcher.DispatchAsync(CancellationToken.None);

        store.Verify(x => x.RetryAsync(
            message.Id,
            message.LeaseId,
            UtcNow.AddSeconds(30),
            "Alert delivery failed (InvalidOperationException).",
            false,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DispatchAsync_WhenEnvelopeIsMalformed_StoresOnlyExceptionClassification()
    {
        const string rawFailure = "raw payload contains secret-value";
        var message = CreateDelivery("Email", "ops@example.test") with { Payload = "{bad-json" };
        var store = StoreWithSingleClaim(message);
        var adapter = CreateAdapter("Email");
        adapter.Setup(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException(rawFailure));
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object]);

        await dispatcher.DispatchAsync(CancellationToken.None);

        store.Verify(x => x.RetryAsync(
            message.Id,
            message.LeaseId,
            UtcNow.AddSeconds(30),
            It.Is<string>(error =>
                error == "Alert delivery failed (JsonException)." &&
                !error.Contains(rawFailure, StringComparison.Ordinal)),
            false,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [DataTestMethod]
    [DataRow("malformed")]
    [DataRow("empty")]
    [DataRow("mismatched")]
    public async Task DispatchAsync_WhenTerminalEnvelopeIsInvalid_UsesAuthoritativeNotificationId(
        string payloadCase)
    {
        var rawPayload = payloadCase switch
        {
            "malformed" => "{raw-payload-secret",
            "empty" => string.Empty,
            "mismatched" => JsonSerializer.Serialize(CreateEnvelope() with
            {
                NotificationId = Guid.NewGuid()
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(payloadCase))
        };
        var message = CreateDelivery("Email", "ops@example.test", attemptCount: 8) with
        {
            Payload = rawPayload
        };
        var store = StoreWithSingleClaim(message);
        var adapter = CreateAdapter("Email");
        Exception failure = payloadCase == "mismatched"
            ? new InvalidOperationException("Alert delivery notification ID does not match its occurrence.")
            : new JsonException(rawPayload);
        var failureName = failure.GetType().Name;
        adapter.Setup(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object]);

        var exception = await Assert.ThrowsExactlyAsync<AggregateException>(
            () => dispatcher.DispatchAsync(CancellationToken.None));

        store.Verify(x => x.RetryAsync(
            message.Id,
            message.LeaseId,
            It.IsAny<DateTime>(),
            It.Is<string>(error =>
                error == $"Alert delivery failed ({failureName})." &&
                (rawPayload.Length == 0 || !error.Contains(rawPayload, StringComparison.Ordinal))),
            true,
            It.Is<AlertDeliveryAudit>(audit =>
                audit.NotificationId == message.NotificationId &&
                audit.Address == message.Destination &&
                audit.Message == $"Alert delivery failed ({failureName})."),
            It.IsAny<CancellationToken>()), Times.Once);
        if (rawPayload.Length > 0)
        {
            Assert.IsFalse(exception.ToString().Contains(rawPayload, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task DispatchAsync_WhenCallerCancels_PropagatesWithoutRetrying()
    {
        using var cancellationSource = new CancellationTokenSource();
        var message = CreateDelivery("MqttAlert", "alert");
        var store = new Mock<IAlertOutboxStore>();
        store.Setup(x => x.ClaimNextDueAsync(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var adapter = CreateAdapter("MqttAlert");
        adapter.Setup(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()))
            .Callback(() => cancellationSource.Cancel())
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object]);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => dispatcher.DispatchAsync(cancellationSource.Token));

        store.Verify(x => x.RetryAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<DateTime>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<AlertDeliveryAudit?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DispatchAsync_WhenDeliveryTimeoutCancels_DoesNotTreatItAsHostCancellation()
    {
        var message = CreateDelivery("MqttAlert", "alert");
        var store = StoreWithSingleClaim(message);
        var adapter = CreateAdapter("MqttAlert");
        adapter.Setup(x => x.DeliverAsync(message, It.Is<CancellationToken>(token => token.CanBeCanceled)))
            .ThrowsAsync(new OperationCanceledException("delivery timeout"));
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object]);

        await dispatcher.DispatchAsync(CancellationToken.None);

        store.Verify(x => x.RetryAsync(
            message.Id,
            message.LeaseId,
            UtcNow.AddSeconds(30),
            "Alert delivery failed (OperationCanceledException).",
            false,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DispatchAsync_CancelsDeliveryAtConfiguredTimeoutAndRetries()
    {
        var message = CreateDelivery("MqttAlert", "alert");
        var store = StoreWithSingleClaim(message);
        var adapter = CreateAdapter("MqttAlert");
        adapter.Setup(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()))
            .Returns<ClaimedAlertDelivery, CancellationToken>(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return null;
            });
        var dispatcher = CreateDispatcher(
            store.Object,
            [adapter.Object],
            new DurableAlertOptions
            {
                DeliveryTimeoutSeconds = 1,
                LeaseSeconds = 2
            });
        var stopwatch = Stopwatch.StartNew();

        await dispatcher.DispatchAsync(CancellationToken.None);

        stopwatch.Stop();
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(750), stopwatch.Elapsed);
        Assert.IsLessThan(TimeSpan.FromSeconds(5), stopwatch.Elapsed);
        store.Verify(x => x.RetryAsync(
            message.Id,
            message.LeaseId,
            UtcNow.AddSeconds(30),
            "Alert delivery failed (TaskCanceledException).",
            false,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [DataTestMethod]
    [DataRow(1, 30)]
    [DataRow(2, 60)]
    [DataRow(3, 120)]
    [DataRow(6, 960)]
    [DataRow(7, 1800)]
    public async Task DispatchAsync_AppliesExponentialRetryWithConfiguredCap(
        int attemptCount,
        int expectedDelaySeconds)
    {
        const string rawFailure = "provider leaked destination@example.test";
        var message = CreateDelivery("MqttAlert", "alert", attemptCount);
        var store = StoreWithSingleClaim(message);
        var adapter = CreateAdapter("MqttAlert");
        adapter.Setup(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(rawFailure));
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object]);

        await dispatcher.DispatchAsync(CancellationToken.None);

        store.Verify(x => x.RetryAsync(
            message.Id,
            message.LeaseId,
            UtcNow.AddSeconds(expectedDelaySeconds),
            It.Is<string>(error =>
                error == "Alert delivery failed (HttpRequestException)." &&
                !error.Contains(rawFailure, StringComparison.Ordinal)),
            false,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [DataTestMethod]
    [DataRow(DeliveryFailureKind.Permanent)]
    [DataRow(DeliveryFailureKind.Configuration)]
    public async Task DispatchAsync_NonTransientTypedFailureDeadLettersImmediately(
        DeliveryFailureKind failureKind)
    {
        var message = CreateDelivery("Email", "ops@example.test", attemptCount: 1);
        var store = StoreWithSingleClaim(message);
        var adapter = CreateAdapter("Email");
        adapter.Setup(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("SendGrid", failureKind, "400"));
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object]);

        await Assert.ThrowsExactlyAsync<AggregateException>(
            () => dispatcher.DispatchAsync(CancellationToken.None));

        store.Verify(x => x.RetryAsync(
            message.Id,
            message.LeaseId,
            UtcNow,
            $"SendGrid email delivery failed ({failureKind}, code 400).",
            true,
            It.IsAny<AlertDeliveryAudit?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DispatchAsync_TransientRetryAfterRaisesDelayWithinConfiguredCap()
    {
        var message = CreateDelivery("Email", "ops@example.test", attemptCount: 1);
        var store = StoreWithSingleClaim(message);
        var adapter = CreateAdapter("Email");
        adapter.Setup(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException(
                "SendGrid",
                DeliveryFailureKind.Transient,
                "429",
                TimeSpan.FromMinutes(2)));
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object]);

        await dispatcher.DispatchAsync(CancellationToken.None);

        store.Verify(x => x.RetryAsync(
            message.Id,
            message.LeaseId,
            UtcNow.AddMinutes(2),
            "SendGrid email delivery failed (Transient, code 429).",
            false,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DispatchAsync_TransientRetryAfterIsCapped()
    {
        var message = CreateDelivery("Email", "ops@example.test", attemptCount: 1);
        var store = StoreWithSingleClaim(message);
        var adapter = CreateAdapter("Email");
        adapter.Setup(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException(
                "SendGrid",
                DeliveryFailureKind.Transient,
                "429",
                TimeSpan.FromHours(1)));
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object]);

        await dispatcher.DispatchAsync(CancellationToken.None);

        store.Verify(x => x.RetryAsync(
            message.Id,
            message.LeaseId,
            UtcNow.AddMinutes(30),
            "SendGrid email delivery failed (Transient, code 429).",
            false,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DispatchAsync_TransientTypedFailureAtMaxAttemptsDeadLetters()
    {
        var message = CreateDelivery("Email", "ops@example.test", attemptCount: 8);
        var store = StoreWithSingleClaim(message);
        var adapter = CreateAdapter("Email");
        adapter.Setup(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException(
                "SendGrid",
                DeliveryFailureKind.Transient,
                "503"));
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object]);

        await Assert.ThrowsExactlyAsync<AggregateException>(
            () => dispatcher.DispatchAsync(CancellationToken.None));

        store.Verify(x => x.RetryAsync(
            message.Id,
            message.LeaseId,
            UtcNow,
            "SendGrid email delivery failed (Transient, code 503).",
            true,
            It.IsAny<AlertDeliveryAudit?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DispatchAsync_BasesRetryAndFailureAuditOnFreshFailureTime()
    {
        var message = CreateDelivery("Email", "ops@example.test", attemptCount: 8);
        var store = StoreWithSingleClaim(message);
        var clock = new ManualTimeProvider(UtcNow);
        var adapter = CreateAdapter("Email");
        adapter.Setup(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()))
            .Callback(() => clock.Advance(TimeSpan.FromSeconds(20)))
            .ThrowsAsync(new HttpRequestException("provider failure"));
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object], timeProvider: clock);

        await Assert.ThrowsExactlyAsync<AggregateException>(
            () => dispatcher.DispatchAsync(CancellationToken.None));

        var outcomeTime = UtcNow.AddSeconds(20);
        store.Verify(x => x.RetryAsync(
            message.Id,
            message.LeaseId,
            outcomeTime,
            "Alert delivery failed (HttpRequestException).",
            true,
            It.Is<AlertDeliveryAudit>(audit =>
                audit.NotificationId == CreateEnvelope().NotificationId &&
                audit.Address == message.Destination &&
                audit.Message == "Alert delivery failed (HttpRequestException)." &&
                audit.SentAt == outcomeTime),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DispatchAsync_WhenCompletionOwnershipIsLost_LogsAndDoesNotRetry()
    {
        var message = CreateDelivery("MqttAlert", "alert");
        var store = StoreWithSingleClaim(message, completeResult: false);
        var adapter = CreateAdapter("MqttAlert");
        var logger = new TestLogger<DurableAlertDispatcher>();
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object], logger: logger);

        await dispatcher.DispatchAsync(CancellationToken.None);

        Assert.IsTrue(logger.Messages.Any(entry =>
            entry.Contains("ownership", StringComparison.OrdinalIgnoreCase)));
        store.Verify(x => x.RetryAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<DateTime>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<AlertDeliveryAudit?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DispatchAsync_WhenRetryOwnershipIsLost_DoesNotReportADeadLetter()
    {
        var message = CreateDelivery("Email", "ops@example.test", attemptCount: 8);
        var store = StoreWithSingleClaim(message);
        store.Setup(x => x.RetryAsync(
                message.Id,
                message.LeaseId,
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                true,
                It.IsAny<AlertDeliveryAudit?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var adapter = CreateAdapter("Email");
        adapter.Setup(x => x.DeliverAsync(message, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider failure"));
        var logger = new TestLogger<DurableAlertDispatcher>();
        var dispatcher = CreateDispatcher(store.Object, [adapter.Object], logger: logger);

        await dispatcher.DispatchAsync(CancellationToken.None);

        Assert.IsTrue(logger.Messages.Any(entry =>
            entry.Contains("ownership", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(logger.Messages.Any(entry =>
            entry.Contains("dead-lettered", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task DispatchAsync_DeadLettersContinuesThenThrowsSafeAggregate()
    {
        const string destination = "alerts@example.test";
        const string rawFailure = "provider rejected alerts@example.test with secret-token";
        var deadLetter = CreateDelivery("Email", destination, attemptCount: 8);
        var successful = CreateDelivery("MqttAlert", "alert");
        var claims = new Queue<ClaimedAlertDelivery?>([deadLetter, successful, null]);
        var store = new Mock<IAlertOutboxStore>();
        store.Setup(x => x.ClaimNextDueAsync(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => claims.Dequeue());
        store.Setup(x => x.RetryAsync(
                deadLetter.Id,
                deadLetter.LeaseId,
                UtcNow,
                It.IsAny<string>(),
                true,
                It.IsAny<AlertDeliveryAudit?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        store.Setup(x => x.CompleteAsync(
                successful.Id,
                successful.LeaseId,
                UtcNow,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var emailAdapter = CreateAdapter("Email");
        emailAdapter.Setup(x => x.DeliverAsync(deadLetter, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(rawFailure));
        var mqttAdapter = CreateAdapter("MqttAlert");
        var logger = new TestLogger<DurableAlertDispatcher>();
        var dispatcher = CreateDispatcher(
            store.Object,
            [emailAdapter.Object, mqttAdapter.Object],
            logger: logger);

        var exception = await Assert.ThrowsExactlyAsync<AggregateException>(
            () => dispatcher.DispatchAsync(CancellationToken.None));

        mqttAdapter.Verify(x => x.DeliverAsync(successful, It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.RetryAsync(
            deadLetter.Id,
            deadLetter.LeaseId,
            UtcNow,
            It.Is<string>(error =>
                error == "Alert delivery failed (InvalidOperationException)." &&
                !error.Contains(rawFailure, StringComparison.Ordinal)),
            true,
            It.Is<AlertDeliveryAudit>(audit =>
                audit.NotificationId == CreateEnvelope().NotificationId &&
                audit.Address == destination &&
                audit.Message == "Alert delivery failed (InvalidOperationException)." &&
                audit.SentAt == UtcNow),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsFalse(exception.ToString().Contains(destination, StringComparison.Ordinal));
        Assert.IsFalse(exception.ToString().Contains(rawFailure, StringComparison.Ordinal));
        Assert.IsTrue(logger.Messages.Any(entry =>
            entry.Contains("dead-lettered", StringComparison.OrdinalIgnoreCase) &&
            !entry.Contains("aler****", StringComparison.Ordinal) &&
            !entry.Contains(destination, StringComparison.Ordinal) &&
            !entry.Contains(rawFailure, StringComparison.Ordinal)));
    }

    private static DurableAlertDispatcher CreateDispatcher(
        IAlertOutboxStore store,
        IEnumerable<IAlertDeliveryAdapter> adapters,
        DurableAlertOptions? options = null,
        ILogger<DurableAlertDispatcher>? logger = null,
        TimeProvider? timeProvider = null) => new(
            store,
            adapters,
            Options.Create(options ?? new DurableAlertOptions()),
            timeProvider ?? new ManualTimeProvider(UtcNow),
            logger ?? new TestLogger<DurableAlertDispatcher>());

    private static Mock<IAlertDeliveryAdapter> CreateAdapter(string kind)
    {
        var adapter = new Mock<IAlertDeliveryAdapter>();
        adapter.SetupGet(x => x.Kind).Returns(kind);
        adapter.Setup(x => x.DeliverAsync(It.IsAny<ClaimedAlertDelivery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertDeliveryAudit?)null);
        return adapter;
    }

    private static Mock<IAlertOutboxStore> StoreWithSingleClaim(
        ClaimedAlertDelivery message,
        bool completeResult = true)
    {
        var claims = new Queue<ClaimedAlertDelivery?>([message, null]);
        var store = new Mock<IAlertOutboxStore>();
        store.Setup(x => x.ClaimNextDueAsync(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => claims.Dequeue());
        store.Setup(x => x.CompleteAsync(
                message.Id,
                message.LeaseId,
                It.IsAny<DateTime>(),
                It.IsAny<AlertDeliveryAudit?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(completeResult);
        store.Setup(x => x.RetryAsync(
                message.Id,
                message.LeaseId,
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<AlertDeliveryAudit?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return store;
    }

    private static ClaimedAlertDelivery CreateDelivery(
        string kind,
        string destination,
        int attemptCount = 1) => new(
            Id: Guid.NewGuid(),
            OccurrenceId: Guid.NewGuid(),
            NotificationId: CreateEnvelope().NotificationId,
            DeliveryKey: Guid.NewGuid().ToString("N"),
            Kind: kind,
            Destination: destination,
            Payload: JsonSerializer.Serialize(CreateEnvelope()),
            Status: "Leased",
            AttemptCount: attemptCount,
            NextAttemptAt: UtcNow,
            LeaseId: Guid.NewGuid(),
            LeaseUntil: UtcNow.AddSeconds(120),
            CompletedAt: null,
            LastError: null,
            CreatedAt: UtcNow.AddMinutes(-1));

    private static AlertDeliveryEnvelope CreateEnvelope() => new(
        Version: 1,
        NotificationId: Guid.Parse("22222222-2222-8222-8222-222222222222"),
        Timestamp: UtcNow.AddMinutes(-2),
        AlertType: AlertType.Alert,
        SerialId: "serial-1",
        CustomerId: 9,
        FleetNr: "fleet-1",
        Message: "Alert message");

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class ManualTimeProvider(DateTime utcNow) : TimeProvider
    {
        private DateTimeOffset current = new(utcNow);

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period) => TimeProvider.System.CreateTimer(callback, state, dueTime, period);
    }
}
