using Rvt.Monitor.Common.Alerts;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class AlertDeliveryIdentityTests
{
    private static readonly Guid OccurrenceId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");

    [TestMethod]
    public void Create_MatchesGoldenSha256Vector()
    {
        var key = AlertDeliveryIdentity.Create(
            OccurrenceId,
            "Email",
            "ops@example.test");

        Assert.AreEqual(
            "64B1D8E4AB9DDB8E6263C7E8519DE2DAC6807F7C649FACBC68B24006719FF987",
            key);
    }

    [TestMethod]
    public void Create_IsDeterministicAndSeparatesKindAndDestination()
    {
        var first = AlertDeliveryIdentity.Create(OccurrenceId, "MqttAlert", "alert");
        var replay = AlertDeliveryIdentity.Create(OccurrenceId, "MqttAlert", "alert");
        var otherKind = AlertDeliveryIdentity.Create(OccurrenceId, "Email", "alert");
        var otherDestination = AlertDeliveryIdentity.Create(OccurrenceId, "MqttAlert", "other");

        Assert.AreEqual(first, replay);
        Assert.AreEqual(64, first.Length);
        Assert.AreNotEqual(first, otherKind);
        Assert.AreNotEqual(first, otherDestination);
    }

    [TestMethod]
    public void CanonicalEmail_TrimsAndLowercasesInvariant()
    {
        Assert.AreEqual(
            "ops@example.test",
            AlertDeliveryIdentity.CanonicalEmail("  Ops@Example.Test  "));
    }

    [TestMethod]
    public void CanonicalSms_TrimsWithoutChangingText()
    {
        Assert.AreEqual(
            "+1 (555) 000-1111",
            AlertDeliveryIdentity.CanonicalSms("  +1 (555) 000-1111  "));
    }
}
