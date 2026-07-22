using System.Text;
using Rvt.Monitor.Common.Alerts;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class AlertIdentityTests
{
    private const string SourceEventKey = "alarm/23423/Δόνηση/測定";
    private const string SourceKeyHashHex = "148C77FF1600CE858C262ACDF8B1351054CD8D6AA2EF9D7ABC5014D3FD8751B0";

    [TestMethod]
    public void SourceKeyHash_MatchesGoldenVectorForNonAsciiSourceEventKey()
    {
        var hash = AlertIdentity.CreateSourceKeyHash(SourceEventKey);

        Assert.AreEqual(SourceKeyHashHex, Convert.ToHexString(hash));
    }

    [TestMethod]
    public void NotificationId_MatchesGoldenVectorAndUsesRfc9562Version8Variant()
    {
        var sourceKeyHash = Convert.FromHexString(SourceKeyHashHex);
        var notificationId = AlertIdentity.CreateNotificationId("omnidots.webhook", sourceKeyHash);
        var bytes = notificationId.ToByteArray(bigEndian: true);

        Assert.AreEqual(Guid.Parse("fe0a093a-75ad-8966-baa9-ace828f4b739"), notificationId);
        Assert.AreEqual(8, bytes[6] >> 4);
        Assert.AreEqual(0b10, bytes[8] >> 6);
    }

    [TestMethod]
    public void SourceKeyHash_RejectsInvalidUtf16()
    {
        Assert.ThrowsExactly<EncoderFallbackException>(
            () => AlertIdentity.CreateSourceKeyHash("\ud800"));
    }
}
