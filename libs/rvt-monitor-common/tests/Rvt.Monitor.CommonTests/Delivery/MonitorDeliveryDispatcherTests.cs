using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Delivery;

[TestClass]
public sealed class MonitorDeliveryDispatcherTests
{
    private static readonly TimeSpan TimingTolerance = TimeSpan.FromSeconds(2);

    [TestMethod]
    public async Task DispatchDueAsync_WhenNoRowIsDue_ClaimsOnceWithoutMutatingOrDelivering()
    {
        var harness = new DispatcherHarness();

        await harness.Dispatcher.DispatchDueAsync();

        Assert.AreEqual(1, harness.Queries.Claims.Count);
        Assert.AreEqual(MonitorDeliveryProducers.Svantek, harness.Queries.Claims.Single().Producer);
        Assert.AreEqual(TimeSpan.FromMinutes(2), harness.Queries.Claims.Single().LeaseDuration);
        Assert.IsEmpty(harness.Commands.Outcomes);
        Assert.IsEmpty(harness.FailureSink.Failures);
        harness.Mqtt.VerifyNoOtherCalls();
        harness.Messages.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DispatchDueAsync_ClaimsImmediatelyBeforeEachDeliveryAndFormatsMyAtmAlertMqtt()
    {
        var events = new List<string>();
        var harness = new DispatcherHarness(ValidOptions() with
        {
            Producer = MonitorDeliveryProducers.MyAtm
        });
        var message = Message(producer: MonitorDeliveryProducers.MyAtm) with
        {
            Destination = "ignored-row-topic"
        };
        harness.Queries.Enqueue(message);
        harness.Queries.OnClaim = () => events.Add("claim");
        harness.Commands.OnOutcome = _ => events.Add("complete");
        string? topic = null;
        string? json = null;
        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string publishedTopic, string payload, CancellationToken _) =>
            {
                events.Add("deliver");
                topic = publishedTopic;
                json = payload;
            })
            .Returns(Task.CompletedTask);

        await harness.Dispatcher.DispatchDueAsync();

        Assert.AreEqual("rvt/alerts", topic);
        using var document = JsonDocument.Parse(json!);
        Assert.AreEqual("157206", document.RootElement.GetProperty("SerialNumber").GetString());
        Assert.AreEqual("Dust Alert LAeq level=75", document.RootElement.GetProperty("Message").GetString());
        Assert.AreEqual(JsonValueKind.Null, document.RootElement.GetProperty("CustomerId").ValueKind);
        CollectionAssert.AreEqual(
            new[] { "claim", "deliver", "complete", "claim" },
            events);
        Assert.AreEqual(2, harness.Queries.Claims.Count);
        Assert.IsNull(harness.Commands.Completions.Single().Audit);
    }

    [TestMethod]
    public async Task DispatchDueAsync_FormatsSvantekAlertMqttWithNoisePrefixAndCustomerId()
    {
        var harness = new DispatcherHarness();
        var payload = DeliveryFixture.ValidPayload with { CustomerId = 41 };
        harness.Queries.Enqueue(Message(payload: payload));
        string? json = null;
        harness.Mqtt.Setup(client => client.PublishAsync(
                "rvt/alerts", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string body, CancellationToken _) => json = body)
            .Returns(Task.CompletedTask);

        await harness.Dispatcher.DispatchDueAsync();

        using var document = JsonDocument.Parse(json!);
        Assert.AreEqual(41, document.RootElement.GetProperty("CustomerId").GetInt32());
        Assert.AreEqual("Noise Alert LAeq level=75", document.RootElement.GetProperty("Message").GetString());
    }

    [TestMethod]
    public async Task DispatchDueAsync_PublishesDataInsertedWithoutNotificationOrAudit()
    {
        var harness = new DispatcherHarness();
        var payload = DeliveryFixture.ValidPayload with { NotificationId = Guid.Empty };
        var message = Message(MonitorDeliveryKind.MqttDataInserted, payload: payload) with
        {
            NotificationId = null,
            Destination = "ignored-row-topic"
        };
        harness.Queries.Enqueue(message);
        string? json = null;
        harness.Mqtt.Setup(client => client.PublishAsync(
                "rvt/inserted", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string body, CancellationToken _) => json = body)
            .Returns(Task.CompletedTask);

        await harness.Dispatcher.DispatchDueAsync();

        using var document = JsonDocument.Parse(json!);
        Assert.AreEqual("Dto Inserted", document.RootElement.GetProperty("Message").GetString());
        Assert.IsNull(harness.Commands.Completions.Single().Audit);
    }

    [TestMethod]
    public async Task DispatchDueAsync_CompletesEmailWithMatchingAuditAndMyAtmOfflineUrl()
    {
        var harness = new DispatcherHarness(ValidOptions() with
        {
            Producer = MonitorDeliveryProducers.MyAtm,
            PortalBaseUrl = "https://portal.example.test/root"
        });
        var payload = DeliveryFixture.ValidPayload with { AlertType = AlertType.Offline };
        var message = Message(
            MonitorDeliveryKind.Email,
            producer: MonitorDeliveryProducers.MyAtm,
            destination: "person@example.test",
            payload: payload);
        harness.Queries.Enqueue(message);
        NotificationDeliveryRequest? request = null;
        harness.Messages.Setup(service => service.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback((NotificationDeliveryRequest selectedRequest, CancellationToken _) =>
                request = selectedRequest)
            .Returns(Task.CompletedTask);

        await harness.Dispatcher.DispatchDueAsync();

        Assert.AreEqual(NotificationMessageKind.Offline, request!.Kind);
        Assert.AreEqual(NotificationChannel.Email, request.Channel);
        Assert.AreEqual("person@example.test", request.Destination);
        Assert.AreEqual("SV-1", request.MonitorName);
        Assert.AreEqual(
            $"https://portal.example.test/root/Notification/View/{DeliveryFixture.NotificationId}",
            request.CallbackUrl);
        var audit = harness.Commands.Completions.Single().Audit;
        Assert.IsNotNull(audit);
        Assert.AreEqual(DeliveryFixture.NotificationId, audit.NotificationId);
        Assert.AreEqual("person@example.test", audit.Address);
        Assert.AreEqual(NotificationConstants.SENT_OK, audit.Result);
        Assert.AreEqual(DateTimeKind.Utc, audit.SentAt.Kind);
    }

    [TestMethod]
    public async Task DispatchDueAsync_CompletesSmsWithMatchingAuditAndSvantekAlertUrl()
    {
        var harness = new DispatcherHarness();
        var message = Message(MonitorDeliveryKind.Sms, destination: "447700900000");
        harness.Queries.Enqueue(message);
        NotificationDeliveryRequest? request = null;
        harness.Messages.Setup(service => service.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback((NotificationDeliveryRequest selectedRequest, CancellationToken _) =>
                request = selectedRequest)
            .Returns(Task.CompletedTask);

        await harness.Dispatcher.DispatchDueAsync();

        Assert.AreEqual(NotificationMessageKind.Alert, request!.Kind);
        Assert.AreEqual(NotificationChannel.Sms, request.Channel);
        Assert.AreEqual("447700900000", request.Destination);
        Assert.AreEqual("SV-1", request.MonitorName);
        Assert.AreEqual(
            $"https://portal.example.test/Notification/View/{DeliveryFixture.NotificationId}",
            request.CallbackUrl);
        Assert.AreEqual("447700900000", harness.Commands.Completions.Single().Audit!.Address);
    }

    [TestMethod]
    public async Task DispatchDueAsync_SvantekOfflineContactHasNoNotificationUrlOrAuditWhenRowReferenceIsMissing()
    {
        var harness = new DispatcherHarness();
        var payload = DeliveryFixture.ValidPayload with { AlertType = AlertType.Offline };
        var message = Message(MonitorDeliveryKind.Email, payload: payload) with { NotificationId = null };
        harness.Queries.Enqueue(message);
        NotificationDeliveryRequest? request = null;
        harness.Messages.Setup(service => service.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback((NotificationDeliveryRequest selectedRequest, CancellationToken _) =>
                request = selectedRequest)
            .Returns(Task.CompletedTask);

        await harness.Dispatcher.DispatchDueAsync();

        Assert.AreEqual(string.Empty, request!.CallbackUrl);
        Assert.IsNull(harness.Commands.Completions.Single().Audit);
    }

    [TestMethod]
    public async Task DispatchDueAsync_StopsAfterDefaultBatchOfFifty()
    {
        var harness = new DispatcherHarness();
        for (var index = 0; index < 51; index++)
        {
            harness.Queries.Enqueue(Message(MonitorDeliveryKind.MqttDataInserted) with
            {
                Id = Guid.NewGuid()
            });
        }

        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await harness.Dispatcher.DispatchDueAsync();

        Assert.AreEqual(50, harness.Queries.Claims.Count);
        Assert.HasCount(50, harness.Commands.Completions);
        Assert.AreEqual(1, harness.Queries.Remaining);
    }

    [TestMethod]
    public async Task DispatchDueAsync_DeliveryTimeoutBecomesFencedRetry()
    {
        var harness = new DispatcherHarness(ValidOptions() with
        {
            DeliveryTimeout = TimeSpan.FromMilliseconds(20),
            LeaseDuration = TimeSpan.FromSeconds(1)
        });
        var message = Message();
        harness.Queries.Enqueue(message);
        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string _, CancellationToken cancellationToken) =>
                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

        await harness.Dispatcher.DispatchDueAsync();

        var retry = harness.Commands.Retries.Single();
        Assert.AreEqual(message.Id, retry.Id);
        Assert.AreEqual(message.LeaseId, retry.LeaseId);
        Assert.AreEqual("Delivery failed (TaskCanceledException).", retry.Error);
        Assert.IsEmpty(harness.Commands.Completions);
        Assert.IsEmpty(harness.Commands.DeadLetters);
        Assert.HasCount(1, harness.FailureSink.Failures);
        Assert.IsFalse(harness.FailureSink.Failures.Single().Terminal);
    }

    [TestMethod]
    public async Task DispatchDueAsync_RetryDelayIsExponentialAndCapped()
    {
        var harness = new DispatcherHarness();
        harness.Queries.Enqueue(Message(attemptCount: 1) with { Id = Guid.NewGuid() });
        harness.Queries.Enqueue(Message(attemptCount: 2) with { Id = Guid.NewGuid() });
        harness.Queries.Enqueue(Message(attemptCount: 7) with { Id = Guid.NewGuid() });
        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());
        var startedAt = DateTime.UtcNow;

        await harness.Dispatcher.DispatchDueAsync();

        Assert.HasCount(3, harness.Commands.Retries);
        AssertTimestampNear(startedAt.AddSeconds(30), harness.Commands.Retries[0].NextAttemptAt);
        AssertTimestampNear(startedAt.AddSeconds(60), harness.Commands.Retries[1].NextAttemptAt);
        AssertTimestampNear(startedAt.AddMinutes(30), harness.Commands.Retries[2].NextAttemptAt);
        Assert.IsEmpty(harness.Commands.DeadLetters);
    }

    [DataTestMethod]
    [DataRow(DeliveryFailureKind.Permanent)]
    [DataRow(DeliveryFailureKind.Configuration)]
    public async Task DispatchDueAsync_NonTransientTypedFailureDeadLettersImmediately(
        DeliveryFailureKind failureKind)
    {
        var harness = new DispatcherHarness();
        var message = Message(MonitorDeliveryKind.Email, attemptCount: 1);
        harness.Queries.Enqueue(message);
        harness.Messages.Setup(service => service.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("SendGrid", failureKind, "400"));

        await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(
            () => harness.Dispatcher.DispatchDueAsync());

        Assert.IsEmpty(harness.Commands.Retries);
        Assert.AreEqual(
            $"SendGrid email delivery failed ({failureKind}, code 400).",
            harness.Commands.DeadLetters.Single().Error);
    }

    [TestMethod]
    public async Task DispatchDueAsync_TransientRetryAfterRaisesDelayWithinConfiguredCap()
    {
        var harness = new DispatcherHarness();
        harness.Queries.Enqueue(Message(MonitorDeliveryKind.Email, attemptCount: 1));
        harness.Messages.Setup(service => service.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException(
                "SendGrid",
                DeliveryFailureKind.Transient,
                "429",
                TimeSpan.FromMinutes(2)));
        var startedAt = DateTime.UtcNow;

        await harness.Dispatcher.DispatchDueAsync();

        var retry = harness.Commands.Retries.Single();
        AssertTimestampNear(startedAt.AddMinutes(2), retry.NextAttemptAt);
        Assert.AreEqual(
            "SendGrid email delivery failed (Transient, code 429).",
            retry.Error);
        Assert.IsEmpty(harness.Commands.DeadLetters);
    }

    [TestMethod]
    public async Task DispatchDueAsync_TransientRetryAfterIsCapped()
    {
        var harness = new DispatcherHarness();
        harness.Queries.Enqueue(Message(MonitorDeliveryKind.Email, attemptCount: 1));
        harness.Messages.Setup(service => service.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException(
                "SendGrid",
                DeliveryFailureKind.Transient,
                "429",
                TimeSpan.FromHours(1)));
        var startedAt = DateTime.UtcNow;

        await harness.Dispatcher.DispatchDueAsync();

        AssertTimestampNear(
            startedAt.AddMinutes(30),
            harness.Commands.Retries.Single().NextAttemptAt);
    }

    [TestMethod]
    public async Task DispatchDueAsync_TransientTypedFailureAtMaxAttemptsDeadLetters()
    {
        var harness = new DispatcherHarness();
        harness.Queries.Enqueue(Message(MonitorDeliveryKind.Email, attemptCount: 8));
        harness.Messages.Setup(service => service.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException(
                "SendGrid",
                DeliveryFailureKind.Transient,
                "503"));

        await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(
            () => harness.Dispatcher.DispatchDueAsync());

        Assert.IsEmpty(harness.Commands.Retries);
        Assert.AreEqual(
            "SendGrid email delivery failed (Transient, code 503).",
            harness.Commands.DeadLetters.Single().Error);
    }

    [TestMethod]
    public async Task DispatchDueAsync_AttemptEightDeadLettersContactWithFailureAuditAndFailsPass()
    {
        var harness = new DispatcherHarness();
        var message = Message(MonitorDeliveryKind.Email, attemptCount: 8, destination: "person@example.test");
        harness.Queries.Enqueue(message);
        harness.Messages.Setup(service => service.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("provider included person@example.test"));

        var error = await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(
            () => harness.Dispatcher.DispatchDueAsync());

        var deadLetter = harness.Commands.DeadLetters.Single();
        Assert.AreEqual("Delivery failed (IOException).", deadLetter.Error);
        Assert.AreEqual(DeliveryFixture.NotificationId, deadLetter.Audit!.NotificationId);
        Assert.AreEqual("person@example.test", deadLetter.Audit.Address);
        Assert.AreEqual(deadLetter.Error, deadLetter.Audit.Result);
        Assert.HasCount(1, error.Failures);
        Assert.IsTrue(harness.FailureSink.Failures.Single().Terminal);
    }

    [TestMethod]
    public async Task DispatchDueAsync_MalformedPayloadDeadLettersImmediatelyWithoutContactAudit()
    {
        var harness = new DispatcherHarness();
        var message = Message(MonitorDeliveryKind.Email, attemptCount: 1) with { Payload = "{ secret malformed" };
        harness.Queries.Enqueue(message);

        var error = await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(
            () => harness.Dispatcher.DispatchDueAsync());

        var deadLetter = harness.Commands.DeadLetters.Single();
        Assert.AreEqual("Delivery failed (InvalidDataException).", deadLetter.Error);
        Assert.IsNull(deadLetter.Audit);
        Assert.HasCount(1, error.Failures);
        Assert.IsEmpty(harness.Commands.Retries);
        harness.Messages.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DispatchDueAsync_ContinuesWithLaterRowsBeforeThrowingAggregateFailure()
    {
        var harness = new DispatcherHarness();
        var malformed = Message() with { Id = Guid.NewGuid(), Payload = "not-json" };
        var valid = Message(MonitorDeliveryKind.MqttDataInserted) with { Id = Guid.NewGuid() };
        harness.Queries.Enqueue(malformed);
        harness.Queries.Enqueue(valid);
        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var error = await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(
            () => harness.Dispatcher.DispatchDueAsync());

        Assert.HasCount(1, error.Failures);
        Assert.AreEqual(malformed.Id, harness.Commands.DeadLetters.Single().Id);
        Assert.AreEqual(valid.Id, harness.Commands.Completions.Single().Id);
        Assert.AreEqual(3, harness.Queries.Claims.Count);
    }

    [TestMethod]
    public async Task DispatchDueAsync_WhenCompletionLosesOwnership_LogsAndMakesNoSecondMutation()
    {
        var harness = new DispatcherHarness();
        var message = Message(MonitorDeliveryKind.MqttDataInserted);
        harness.Queries.Enqueue(message);
        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        harness.Commands.OutcomeResult = false;

        await harness.Dispatcher.DispatchDueAsync();

        Assert.HasCount(1, harness.Commands.Completions);
        Assert.IsEmpty(harness.Commands.Retries);
        Assert.IsEmpty(harness.Commands.DeadLetters);
        Assert.IsEmpty(harness.FailureSink.Failures);
        Assert.IsTrue(harness.Logger.Messages.Any(entry =>
            entry.Contains("ownership", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task DispatchDueAsync_WhenRetryLosesOwnership_MakesNoSecondMutationOrFailureReport()
    {
        var harness = new DispatcherHarness(ValidOptions() with
        {
            FailureMode = MonitorDeliveryFailureMode.AnyDeliveryFailure
        });
        harness.Queries.Enqueue(Message());
        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException());
        harness.Commands.OutcomeResult = false;

        await harness.Dispatcher.DispatchDueAsync();

        Assert.HasCount(1, harness.Commands.Retries);
        Assert.IsEmpty(harness.Commands.DeadLetters);
        Assert.IsEmpty(harness.FailureSink.Failures);
    }

    [TestMethod]
    public async Task DispatchDueAsync_CallerCancellationLeavesClaimedLeaseUntouched()
    {
        using var cancellationSource = new CancellationTokenSource();
        var harness = new DispatcherHarness();
        harness.Queries.Enqueue(Message());
        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string _, CancellationToken cancellationToken) =>
            {
                cancellationSource.Cancel();
                return Task.FromCanceled(cancellationToken);
            });

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => harness.Dispatcher.DispatchDueAsync(cancellationSource.Token));

        Assert.IsEmpty(harness.Commands.Outcomes);
        Assert.IsEmpty(harness.FailureSink.Failures);
        Assert.AreEqual(1, harness.Queries.Claims.Count);
    }

    [TestMethod]
    public async Task DispatchDueAsync_ProducerMismatchDeadLettersImmediately()
    {
        var harness = new DispatcherHarness();
        harness.Queries.Enqueue(Message(producer: MonitorDeliveryProducers.MyAtm));

        await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(
            () => harness.Dispatcher.DispatchDueAsync());

        Assert.AreEqual("Delivery failed (InvalidDataException).", harness.Commands.DeadLetters.Single().Error);
        Assert.IsEmpty(harness.Commands.Retries);
        harness.Mqtt.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DispatchDueAsync_UnsupportedVersionDeadLettersImmediately()
    {
        var harness = new DispatcherHarness();
        harness.Queries.Enqueue(Message() with { PayloadVersion = 2 });

        await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(
            () => harness.Dispatcher.DispatchDueAsync());

        Assert.AreEqual("Delivery failed (InvalidDataException).", harness.Commands.DeadLetters.Single().Error);
        Assert.IsEmpty(harness.Commands.Retries);
    }

    [TestMethod]
    public async Task DispatchDueAsync_UnknownKindDeadLettersImmediately()
    {
        var harness = new DispatcherHarness();
        harness.Queries.Enqueue(Message((MonitorDeliveryKind)99));

        await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(
            () => harness.Dispatcher.DispatchDueAsync());

        Assert.AreEqual("Delivery failed (InvalidDataException).", harness.Commands.DeadLetters.Single().Error);
        Assert.IsEmpty(harness.Commands.Retries);
    }

    [TestMethod]
    public async Task DispatchDueAsync_PersistedErrorRedactsExceptionMessageDestinationAndPayload()
    {
        var harness = new DispatcherHarness();
        var secret = "secret destination token";
        var message = Message(payload: DeliveryFixture.ValidPayload with { Field = secret }) with
        {
            Destination = secret
        };
        harness.Queries.Enqueue(message);
        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException(secret));

        await harness.Dispatcher.DispatchDueAsync();

        var persistedError = harness.Commands.Retries.Single().Error;
        Assert.AreEqual("Delivery failed (TimeoutException).", persistedError);
        Assert.DoesNotContain("secret", persistedError, StringComparison.OrdinalIgnoreCase);
        Assert.IsLessThanOrEqualTo(1024, persistedError.Length);
        Assert.IsFalse(harness.Logger.Messages.Any(entry =>
            entry.Contains(secret, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task DispatchDueAsync_DeadLetterOnlyModeRetriesWithoutFailingPass()
    {
        var harness = new DispatcherHarness();
        harness.Queries.Enqueue(Message());
        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());

        await harness.Dispatcher.DispatchDueAsync();

        Assert.HasCount(1, harness.Commands.Retries);
        Assert.HasCount(1, harness.FailureSink.Failures);
    }

    [TestMethod]
    public async Task DispatchDueAsync_AnyFailureMode_RetriesThenFailsPass()
    {
        var harness = new DispatcherHarness(ValidOptions() with
        {
            FailureMode = MonitorDeliveryFailureMode.AnyDeliveryFailure
        });
        harness.Queries.Enqueue(Message(attemptCount: 1));
        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("secret destination"));

        var error = await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(
            () => harness.Dispatcher.DispatchDueAsync());

        Assert.AreEqual("Delivery failed (TimeoutException).", harness.Commands.Retries.Single().Error);
        Assert.DoesNotContain("secret", harness.Commands.Retries.Single().Error);
        Assert.HasCount(1, error.Failures);
    }

    [TestMethod]
    public async Task DispatchDueAsync_FailureSinkRunsOnlyAfterFencedOutcomeAndCannotMaskIt()
    {
        var harness = new DispatcherHarness();
        var events = new List<string>();
        harness.Queries.Enqueue(Message());
        harness.Mqtt.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException());
        harness.Commands.OnOutcome = _ => events.Add("outcome");
        harness.FailureSink.OnFailure = _ =>
        {
            events.Add("sink");
            throw new InvalidOperationException("sink secret");
        };

        await harness.Dispatcher.DispatchDueAsync();

        CollectionAssert.AreEqual(new[] { "outcome", "sink" }, events);
        Assert.HasCount(1, harness.Commands.Retries);
        Assert.IsTrue(harness.Logger.Messages.Any(entry =>
            entry.Contains("failure sink", StringComparison.OrdinalIgnoreCase) &&
            !entry.Contains("sink secret", StringComparison.Ordinal)));
    }

    private static void AssertTimestampNear(DateTime expected, DateTime actual)
    {
        var delta = (actual - expected).Duration();
        Assert.IsLessThanOrEqualTo(TimingTolerance, delta, $"Expected {actual:O} within {TimingTolerance} of {expected:O}.");
    }

    private static MonitorDeliveryMessage Message(
        MonitorDeliveryKind kind = MonitorDeliveryKind.MqttAlert,
        string producer = MonitorDeliveryProducers.Svantek,
        int attemptCount = 1,
        string destination = "alerts@example.test",
        MonitorDeliveryPayloadV1? payload = null) =>
        DeliveryFixture.Message(kind, attemptCount: attemptCount) with
        {
            Producer = producer,
            Destination = destination,
            Payload = JsonSerializer.Serialize(payload ?? DeliveryFixture.ValidPayload)
        };

    private static MonitorDeliveryOptions ValidOptions() => new()
    {
        Producer = MonitorDeliveryProducers.Svantek,
        InsertTopic = "rvt/inserted",
        AlertTopic = "rvt/alerts",
        PortalBaseUrl = "https://portal.example.test/"
    };

    private sealed class DispatcherHarness
    {
        internal DispatcherHarness(MonitorDeliveryOptions? options = null)
        {
            Dispatcher = new MonitorDeliveryDispatcher(
                Queries,
                Commands,
                FailureSink,
                Mqtt.Object,
                Messages.Object,
                Logger,
                options ?? ValidOptions());
        }

        internal RecordingQueries Queries { get; } = new();
        internal RecordingCommands Commands { get; } = new();
        internal RecordingFailureSink FailureSink { get; } = new();
        internal Mock<IMqttClient> Mqtt { get; } = new(MockBehavior.Strict);
        internal Mock<INotificationDeliveryService> Messages { get; } = new(MockBehavior.Strict);
        internal TestLogger Logger { get; } = new();
        internal MonitorDeliveryDispatcher Dispatcher { get; }
    }

    private sealed class RecordingQueries : IMonitorDeliveryOutboxQueries
    {
        private readonly Queue<MonitorDeliveryMessage> messages = new();

        internal List<ClaimCall> Claims { get; } = [];
        internal Action? OnClaim { get; set; }
        internal int Remaining => messages.Count;

        internal void Enqueue(MonitorDeliveryMessage message) => messages.Enqueue(message);

        public Task<MonitorDeliveryMessage?> ClaimNextDueAsync(
            string producer,
            DateTime utcNow,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Claims.Add(new ClaimCall(producer, utcNow, leaseDuration, cancellationToken));
            OnClaim?.Invoke();
            return Task.FromResult(messages.TryDequeue(out var message) ? message : null);
        }
    }

    private sealed class RecordingCommands : IMonitorDeliveryOutboxCommands
    {
        internal List<CompletionCall> Completions { get; } = [];
        internal List<RetryCall> Retries { get; } = [];
        internal List<DeadLetterCall> DeadLetters { get; } = [];
        internal IEnumerable<object> Outcomes => Completions.Cast<object>().Concat(Retries).Concat(DeadLetters);
        internal bool OutcomeResult { get; set; } = true;
        internal Action<object>? OnOutcome { get; set; }

        public Task<bool> CompleteAsync(
            Guid id,
            Guid leaseId,
            DateTime completedAt,
            MonitorDeliveryAudit? audit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = new CompletionCall(id, leaseId, completedAt, audit, cancellationToken);
            Completions.Add(call);
            OnOutcome?.Invoke(call);
            return Task.FromResult(OutcomeResult);
        }

        public Task<bool> RetryAsync(
            Guid id,
            Guid leaseId,
            DateTime nextAttemptAt,
            string error,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = new RetryCall(id, leaseId, nextAttemptAt, error, cancellationToken);
            Retries.Add(call);
            OnOutcome?.Invoke(call);
            return Task.FromResult(OutcomeResult);
        }

        public Task<bool> DeadLetterAsync(
            Guid id,
            Guid leaseId,
            DateTime failedAt,
            string error,
            MonitorDeliveryAudit? audit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = new DeadLetterCall(id, leaseId, failedAt, error, audit, cancellationToken);
            DeadLetters.Add(call);
            OnOutcome?.Invoke(call);
            return Task.FromResult(OutcomeResult);
        }
    }

    private sealed class RecordingFailureSink : IMonitorDeliveryFailureSink
    {
        internal List<FailureCall> Failures { get; } = [];
        internal Action<FailureCall>? OnFailure { get; set; }

        public Task RecordFailureAsync(
            MonitorDeliveryMessage message,
            string error,
            bool terminal,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = new FailureCall(message, error, terminal, cancellationToken);
            Failures.Add(call);
            OnFailure?.Invoke(call);
            return Task.CompletedTask;
        }
    }

    private sealed class TestLogger : ILogger<MonitorDeliveryDispatcher>
    {
        internal List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private sealed record ClaimCall(
        string Producer,
        DateTime UtcNow,
        TimeSpan LeaseDuration,
        CancellationToken CancellationToken);

    private sealed record CompletionCall(
        Guid Id,
        Guid LeaseId,
        DateTime CompletedAt,
        MonitorDeliveryAudit? Audit,
        CancellationToken CancellationToken);

    private sealed record RetryCall(
        Guid Id,
        Guid LeaseId,
        DateTime NextAttemptAt,
        string Error,
        CancellationToken CancellationToken);

    private sealed record DeadLetterCall(
        Guid Id,
        Guid LeaseId,
        DateTime FailedAt,
        string Error,
        MonitorDeliveryAudit? Audit,
        CancellationToken CancellationToken);

    private sealed record FailureCall(
        MonitorDeliveryMessage Message,
        string Error,
        bool Terminal,
        CancellationToken CancellationToken);
}
