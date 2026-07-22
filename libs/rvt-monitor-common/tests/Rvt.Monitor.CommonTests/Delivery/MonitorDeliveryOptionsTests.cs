using Rvt.Monitor.Common.Delivery;

namespace Rvt.Monitor.CommonTests.Delivery;

[TestClass]
public sealed class MonitorDeliveryOptionsTests
{
    [TestMethod]
    public void Contracts_UseCanonicalProducerAndEnumValues()
    {
        Assert.AreEqual("MyAtm", MonitorDeliveryProducers.MyAtm);
        Assert.AreEqual("Svantek", MonitorDeliveryProducers.Svantek);
        Assert.IsTrue(MonitorDeliveryProducers.IsKnown("MyAtm"));
        Assert.IsTrue(MonitorDeliveryProducers.IsKnown("Svantek"));
        Assert.IsFalse(MonitorDeliveryProducers.IsKnown("myatm"));
        CollectionAssert.AreEqual(
            new[]
            {
                MonitorDeliveryKind.MqttDataInserted,
                MonitorDeliveryKind.MqttAlert,
                MonitorDeliveryKind.Email,
                MonitorDeliveryKind.Sms
            },
            Enum.GetValues<MonitorDeliveryKind>());
        CollectionAssert.AreEqual(
            new[]
            {
                MonitorDeliveryFailureMode.DeadLetterOnly,
                MonitorDeliveryFailureMode.AnyDeliveryFailure
            },
            Enum.GetValues<MonitorDeliveryFailureMode>());
    }

    [TestMethod]
    public void Defaults_PreserveSharedDeliveryPolicy()
    {
        var options = new MonitorDeliveryOptions();

        Assert.AreEqual(string.Empty, options.Producer);
        Assert.AreEqual(string.Empty, options.InsertTopic);
        Assert.AreEqual(string.Empty, options.AlertTopic);
        Assert.AreEqual(string.Empty, options.PortalBaseUrl);
        Assert.AreEqual(MonitorDeliveryFailureMode.DeadLetterOnly, options.FailureMode);
        Assert.AreEqual(50, options.BatchSize);
        Assert.AreEqual(TimeSpan.FromSeconds(30), options.DeliveryTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(120), options.LeaseDuration);
        Assert.AreEqual(TimeSpan.FromSeconds(30), options.InitialRetryDelay);
        Assert.AreEqual(TimeSpan.FromMinutes(30), options.RetryCap);
        Assert.AreEqual(8, options.MaxAttempts);
    }

    [TestMethod]
    public void Contracts_PreserveRequestAndAuditValues()
    {
        var createdAt = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var request = new MonitorDeliveryRequest(
            Guid.NewGuid(),
            MonitorDeliveryProducers.Svantek,
            DeliveryFixture.NotificationId,
            "notification:fixture-key",
            "delivery:fixture-key",
            MonitorDeliveryKind.Email,
            "alerts@example.test",
            1,
            "{}",
            createdAt);
        var audit = new MonitorDeliveryAudit(
            DeliveryFixture.NotificationId,
            "alerts@example.test",
            "Sent",
            createdAt);

        Assert.AreEqual(MonitorDeliveryProducers.Svantek, request.Producer);
        Assert.AreEqual("delivery:fixture-key", request.DeliveryKey);
        Assert.AreEqual(MonitorDeliveryKind.Email, request.Kind);
        Assert.AreEqual(DeliveryFixture.NotificationId, audit.NotificationId);
        Assert.AreEqual("Sent", audit.Result);
    }

    [TestMethod]
    public void Validate_AcceptsCompleteOptions()
    {
        ValidOptions().Validate();
    }

    [TestMethod]
    public void Validate_RejectsUnknownProducer()
    {
        var options = ValidOptions() with { Producer = "MyATM" };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    public void Validate_RejectsNonPositiveBatchSize()
    {
        var options = ValidOptions() with { BatchSize = 0 };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    public void Validate_RejectsMissingTopics()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            (ValidOptions() with { InsertTopic = " " }).Validate);
        Assert.ThrowsExactly<InvalidOperationException>(
            (ValidOptions() with { AlertTopic = string.Empty }).Validate);
    }

    [TestMethod]
    public void Validate_RejectsNonPositiveDurations()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            (ValidOptions() with { DeliveryTimeout = TimeSpan.Zero }).Validate);
        Assert.ThrowsExactly<InvalidOperationException>(
            (ValidOptions() with { LeaseDuration = TimeSpan.Zero }).Validate);
        Assert.ThrowsExactly<InvalidOperationException>(
            (ValidOptions() with { InitialRetryDelay = TimeSpan.Zero }).Validate);
        Assert.ThrowsExactly<InvalidOperationException>(
            (ValidOptions() with { RetryCap = TimeSpan.Zero }).Validate);
    }

    [TestMethod]
    public void Validate_RejectsRetryCapShorterThanInitialDelay()
    {
        var options = ValidOptions() with
        {
            InitialRetryDelay = TimeSpan.FromMinutes(2),
            RetryCap = TimeSpan.FromMinutes(1)
        };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    public void Validate_RejectsLeaseNotLongerThanDeliveryTimeout()
    {
        var options = ValidOptions() with
        {
            DeliveryTimeout = TimeSpan.FromSeconds(30),
            LeaseDuration = TimeSpan.FromSeconds(30)
        };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    public void Validate_RejectsNonPositiveMaxAttempts()
    {
        var options = ValidOptions() with { MaxAttempts = 0 };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    [DataRow("relative/path")]
    [DataRow("ftp://rvt.example.test/")]
    [DataRow("not a url")]
    public void Validate_RejectsPortalUrlThatIsNotAbsoluteHttpOrHttps(string portalBaseUrl)
    {
        var options = ValidOptions() with { PortalBaseUrl = portalBaseUrl };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    private static MonitorDeliveryOptions ValidOptions() => new()
    {
        Producer = MonitorDeliveryProducers.Svantek,
        InsertTopic = "monitors/inserted",
        AlertTopic = "monitors/alerts",
        PortalBaseUrl = "https://portal.example.test/"
    };
}
