using Microsoft.Extensions.Logging;
using MyAtm.Delivery;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace MyAtmMonitorTests.Delivery;

// Moved from CommonTests' SharedRuntimeCompatibilityTests with the planner
// (hexagonal convergence M2): pins the legacy notification and message
// selection the retired synchronous dispatcher produced.
[TestClass]
public sealed class RuleAlertDeliveryPlannerCompatibilityTests
{
    [TestInitialize]
    public void TestInitialize()
    {
        using ILoggerFactory factory = LoggerFactory.Create(_ => { });
        RvtLogger.CreateLogger(factory, nameof(RuleAlertDeliveryPlannerCompatibilityTests));
    }

    [TestMethod]
    [DataRow(AlertType.Alert)]
    [DataRow(AlertType.Caution)]
    [DataRow(AlertType.Offline)]
    [DataRow(AlertType.BatteryAlert)]
    [DataRow(AlertType.BatteryCaution)]
    public void DurablePlannerPreservesLegacyNotificationAndMessageSelection(AlertType alertType)
    {
        RuleNotificationRequest request = new(
            FleetNr: "SV-1",
            SerialId: "SV-157206",
            AlertTime: new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc),
            LimitOn: 70,
            AveragingPeriod: 900,
            Level: 75.5,
            alertType,
            Field: "LAeq",
            MonitorId: Guid.Parse("11111111-2222-3333-4444-555555555555"));
        List<RvtContactDto> contacts =
        [
            new(true, false, "alerts@example.test", null, null, null)
        ];
        RuleAlertDeliveryPlan plan = new RuleAlertDeliveryPlanner().Plan(
            request,
            contacts,
            MonitorDeliveryProducers.MyAtm,
            customerId: null,
            correlationKey: $"compatibility:{alertType}",
            createdAt: request.AlertTime);

        // Pins the notification shape the retired synchronous dispatcher
        // produced (deleted by legacy-retirement step 4 on 2026-07-29).
        Assert.AreEqual(request.AlertTime, plan.Notification.NotificationTime);
        Assert.AreEqual(request.LimitOn, plan.Notification.LimitOn);
        Assert.AreEqual(request.AveragingPeriod, plan.Notification.AveragingPeriod);
        Assert.AreEqual(request.Level, plan.Notification.Level);
        Assert.AreEqual(alertType, plan.Notification.AlertType);
        Assert.AreEqual(request.Field, plan.Notification.AlertField);
        Assert.AreEqual(request.MonitorId, plan.Notification.MonitorId);

        List<MonitorDeliveryRequest> emails = [.. plan.Deliveries.Where(delivery => delivery.Kind == MonitorDeliveryKind.Email)];
        Assert.HasCount(1, emails);
        MonitorDeliveryRequest email = emails[0];
        MonitorDeliveryPayloadV1 payload = MonitorDeliveryPayloadCodec.Decode(new MonitorDeliveryMessage(
            email.Id,
            email.Producer,
            email.NotificationId,
            email.CorrelationKey,
            email.DeliveryKey,
            email.Kind,
            email.Destination,
            email.PayloadVersion,
            email.Payload,
            AttemptCount: 1,
            LeaseId: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")));
        Assert.AreEqual(alertType, payload.AlertType);
    }
}
