using System.Security.Cryptography;
using System.Text;
using MyAtm.Delivery;

namespace MyAtmMonitorTests.Delivery;

[TestClass]
public sealed class MonitorDeliveryIdentityTests
{
    [TestMethod]
    public void CreateGuid_PreservesMyAtmSha256Identity()
    {
        Guid actual = MonitorDeliveryIdentity.CreateGuid("notification:fixture-key");
        Guid expected = new(SHA256.HashData(Encoding.UTF8.GetBytes("notification:fixture-key"))[..16]);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void CreateGuid_IsDeterministic()
    {
        Guid first = MonitorDeliveryIdentity.CreateGuid("delivery:fixture-key");
        Guid second = MonitorDeliveryIdentity.CreateGuid("delivery:fixture-key");

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void CreateGuid_RejectsWhitespace()
    {
        Assert.ThrowsExactly<ArgumentException>(() => MonitorDeliveryIdentity.CreateGuid(" "));
    }
}
