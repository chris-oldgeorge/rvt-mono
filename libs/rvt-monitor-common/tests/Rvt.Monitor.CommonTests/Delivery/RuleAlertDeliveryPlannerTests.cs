using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using RulesContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;

namespace Rvt.Monitor.CommonTests.Delivery;

[TestClass]
public sealed class RuleAlertDeliveryPlannerTests
{
    private static readonly DateTime AlertTime = new(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CreatedAt = new(2026, 7, 15, 10, 0, 5, DateTimeKind.Utc);
    private static readonly Guid MonitorId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [DataTestMethod]
    [DataRow(AlertType.Alert)]
    [DataRow(AlertType.Caution)]
    [DataRow(AlertType.Offline)]
    [DataRow(AlertType.BatteryAlert)]
    [DataRow(AlertType.BatteryCaution)]
    public void Plan_PreservesNotificationSemanticsAndCreatesDeterministicOrderedDeliveries(AlertType alertType)
    {
        var request = Request(alertType);
        var contacts = new List<RulesContactDto>
        {
            new(true, true, "first@example.test", "441111111111", TimeSpan.FromHours(9), TimeSpan.FromHours(11)),
            new(true, false, "second@example.test", null, null, null),
            new(true, true, "outside@example.test", "442222222222", TimeSpan.FromHours(11), TimeSpan.FromHours(12)),
            new(true, true, " ", " ", null, null)
        };
        var correlationKey = $"svantek:rule:monitor:rule:{alertType}:{AlertTime:O}";
        var planner = new RuleAlertDeliveryPlanner();

        var plan = planner.Plan(
            request,
            contacts,
            MonitorDeliveryProducers.Svantek,
            customerId: null,
            correlationKey,
            CreatedAt);
        var replay = planner.Plan(
            request,
            contacts,
            MonitorDeliveryProducers.Svantek,
            customerId: null,
            correlationKey,
            CreatedAt);

        var expectedNotificationId = MonitorDeliveryIdentity.CreateGuid($"notification:{correlationKey}");
        Assert.AreEqual(expectedNotificationId, plan.Notification.Id);
        Assert.AreEqual(request.AlertTime, plan.Notification.NotificationTime);
        Assert.AreEqual(request.LimitOn, plan.Notification.LimitOn);
        Assert.AreEqual(request.AveragingPeriod, plan.Notification.AveragingPeriod);
        Assert.AreEqual(request.Level, plan.Notification.Level);
        Assert.IsNull(plan.Notification.ClosedTime);
        Assert.IsNull(plan.Notification.ClosedByUser);
        Assert.AreEqual(request.AlertType, plan.Notification.AlertType);
        Assert.AreEqual(request.Field, plan.Notification.AlertField);
        Assert.AreEqual(request.MonitorId, plan.Notification.MonitorId);

        CollectionAssert.AreEqual(
            new[]
            {
                MonitorDeliveryKind.MqttAlert,
                MonitorDeliveryKind.Email,
                MonitorDeliveryKind.Email,
                MonitorDeliveryKind.Sms
            },
            plan.Deliveries.Select(delivery => delivery.Kind).ToArray());
        CollectionAssert.AreEqual(
            new[] { "alert", "first@example.test", "second@example.test", "441111111111" },
            plan.Deliveries.Select(delivery => delivery.Destination).ToArray());

        var expectedKeys = new[]
        {
            $"{correlationKey}:MqttAlert:alert",
            $"{correlationKey}:Email:first@example.test",
            $"{correlationKey}:Email:second@example.test",
            $"{correlationKey}:Sms:441111111111"
        };
        CollectionAssert.AreEqual(expectedKeys, plan.Deliveries.Select(delivery => delivery.DeliveryKey).ToArray());
        CollectionAssert.AreEqual(
            expectedKeys.Select(key => MonitorDeliveryIdentity.CreateGuid($"outbox:{key}")).ToArray(),
            plan.Deliveries.Select(delivery => delivery.Id).ToArray());

        Assert.HasCount(1, plan.Deliveries.Where(delivery => delivery.Kind == MonitorDeliveryKind.MqttAlert));
        Assert.IsTrue(plan.Deliveries.All(delivery => delivery.Producer == MonitorDeliveryProducers.Svantek));
        Assert.IsTrue(plan.Deliveries.All(delivery => delivery.NotificationId == expectedNotificationId));
        Assert.IsTrue(plan.Deliveries.All(delivery => delivery.CorrelationKey == correlationKey));
        Assert.IsTrue(plan.Deliveries.All(delivery => delivery.PayloadVersion == 1));
        Assert.IsTrue(plan.Deliveries.All(delivery => delivery.CreatedAt == CreatedAt));
        Assert.IsTrue(plan.Deliveries.All(delivery => delivery.Payload == plan.Deliveries[0].Payload));

        foreach (var delivery in plan.Deliveries)
        {
            var payload = Decode(delivery);
            Assert.AreEqual(expectedNotificationId, payload.NotificationId);
            Assert.AreEqual(AlertTime, payload.Timestamp);
            Assert.AreEqual("SV-157206", payload.SerialId);
            Assert.IsNull(payload.CustomerId);
            Assert.AreEqual("SV-1", payload.FleetNr);
            Assert.AreEqual(alertType, payload.AlertType);
            Assert.AreEqual("LAeq", payload.Field);
            Assert.AreEqual(75.5, payload.Level);
            Assert.IsNull(payload.PortalBaseUrl);
        }

        Assert.AreEqual(plan.Notification.Id, replay.Notification.Id);
        CollectionAssert.AreEqual(
            plan.Deliveries.Select(delivery => delivery.Id).ToArray(),
            replay.Deliveries.Select(delivery => delivery.Id).ToArray());
        CollectionAssert.AreEqual(
            plan.Deliveries.Select(delivery => delivery.DeliveryKey).ToArray(),
            replay.Deliveries.Select(delivery => delivery.DeliveryKey).ToArray());
    }

    [TestMethod]
    public void Plan_PreservesCustomerIdInEveryPayload()
    {
        var plan = new RuleAlertDeliveryPlanner().Plan(
            Request(AlertType.Alert),
            [],
            MonitorDeliveryProducers.MyAtm,
            customerId: 42,
            correlationKey: "myatm:fixture",
            CreatedAt);

        Assert.HasCount(1, plan.Deliveries);
        Assert.AreEqual(42, Decode(plan.Deliveries[0]).CustomerId);
    }

    [TestMethod]
    public void Plan_DeduplicatesEligibleEmailDestinationsUsingOrdinalComparison()
    {
        var contacts = new List<RulesContactDto>
        {
            new(true, false, "duplicate@example.test", null, null, null),
            new(true, false, "duplicate@example.test", null, null, null),
            new(true, false, "DUPLICATE@example.test", null, null, null)
        };

        var plan = new RuleAlertDeliveryPlanner().Plan(
            Request(AlertType.Alert),
            contacts,
            MonitorDeliveryProducers.Svantek,
            customerId: null,
            correlationKey: "svantek:duplicate-email",
            CreatedAt);

        CollectionAssert.AreEqual(
            new[] { "duplicate@example.test", "DUPLICATE@example.test" },
            plan.Deliveries
                .Where(delivery => delivery.Kind == MonitorDeliveryKind.Email)
                .Select(delivery => delivery.Destination)
                .ToArray());
    }

    [TestMethod]
    public void Plan_DeduplicatesEligibleSmsDestinationsAndPreservesFirstEligibleOrder()
    {
        var contacts = new List<RulesContactDto>
        {
            new(false, true, string.Empty, "442222222222", null, null),
            new(false, true, string.Empty, "441111111111", null, null),
            new(false, true, string.Empty, "442222222222", null, null),
            new(false, true, string.Empty, "441111111111", null, null)
        };

        var plan = new RuleAlertDeliveryPlanner().Plan(
            Request(AlertType.Alert),
            contacts,
            MonitorDeliveryProducers.Svantek,
            customerId: null,
            correlationKey: "svantek:duplicate-sms",
            CreatedAt);

        CollectionAssert.AreEqual(
            new[] { "442222222222", "441111111111" },
            plan.Deliveries
                .Where(delivery => delivery.Kind == MonitorDeliveryKind.Sms)
                .Select(delivery => delivery.Destination)
                .ToArray());
    }

    [TestMethod]
    public void Plan_RejectsUnknownProducer()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new RuleAlertDeliveryPlanner().Plan(
            Request(AlertType.Alert),
            [],
            producer: "Unknown",
            customerId: null,
            correlationKey: "svantek:fixture",
            CreatedAt));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void Plan_RejectsBlankCorrelationKey(string correlationKey)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new RuleAlertDeliveryPlanner().Plan(
            Request(AlertType.Alert),
            [],
            MonitorDeliveryProducers.Svantek,
            customerId: null,
            correlationKey,
            CreatedAt));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void Plan_RejectsBlankSerialId(string serialId)
    {
        var request = Request(AlertType.Alert) with { SerialId = serialId };

        Assert.ThrowsExactly<ArgumentException>(() => new RuleAlertDeliveryPlanner().Plan(
            request,
            [],
            MonitorDeliveryProducers.Svantek,
            customerId: null,
            correlationKey: "svantek:fixture",
            CreatedAt));
    }

    [DataTestMethod]
    [DataRow(DateTimeKind.Local)]
    [DataRow(DateTimeKind.Unspecified)]
    public void Plan_RejectsNonUtcAlertTime(DateTimeKind kind)
    {
        var request = Request(AlertType.Alert) with
        {
            AlertTime = DateTime.SpecifyKind(AlertTime, kind)
        };

        Assert.ThrowsExactly<ArgumentException>(() => new RuleAlertDeliveryPlanner().Plan(
            request,
            [],
            MonitorDeliveryProducers.Svantek,
            customerId: null,
            correlationKey: "svantek:fixture",
            CreatedAt));
    }

    [DataTestMethod]
    [DataRow(DateTimeKind.Local)]
    [DataRow(DateTimeKind.Unspecified)]
    public void Plan_RejectsNonUtcCreatedAt(DateTimeKind kind)
    {
        var createdAt = DateTime.SpecifyKind(CreatedAt, kind);

        Assert.ThrowsExactly<ArgumentException>(() => new RuleAlertDeliveryPlanner().Plan(
            Request(AlertType.Alert),
            [],
            MonitorDeliveryProducers.Svantek,
            customerId: null,
            correlationKey: "svantek:fixture",
            createdAt));
    }

    [TestMethod]
    public void Planner_HasNoInjectedTransportPersistenceOrDelegateDependencies()
    {
        var plannerType = typeof(RuleAlertDeliveryPlanner);

        var constructors = plannerType.GetConstructors();
        Assert.HasCount(1, constructors);
        var constructor = constructors[0];
        Assert.HasCount(0, constructor.GetParameters());
        Assert.HasCount(0, plannerType.GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic));
    }

    private static RuleNotificationRequest Request(AlertType alertType) => new(
        FleetNr: "SV-1",
        SerialId: "SV-157206",
        AlertTime,
        LimitOn: 70,
        AveragingPeriod: 900,
        Level: 75.5,
        alertType,
        Field: "LAeq",
        MonitorId);

    private static MonitorDeliveryPayloadV1 Decode(MonitorDeliveryRequest request) =>
        MonitorDeliveryPayloadCodec.Decode(new MonitorDeliveryMessage(
            request.Id,
            request.Producer,
            request.NotificationId,
            request.CorrelationKey,
            request.DeliveryKey,
            request.Kind,
            request.Destination,
            request.PayloadVersion,
            request.Payload,
            AttemptCount: 1,
            LeaseId: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")));
}
