using System.Security.Cryptography;
using System.Text;
using Rvt.Monitor.Common.Delivery;

namespace Rvt.Monitor.CommonTests.Delivery;

[TestClass]
public sealed class MonitorDeliveryIdentityTests
{
    [TestMethod]
    public void CreateGuid_PreservesMyAtmSha256Identity()
    {
        var actual = MonitorDeliveryIdentity.CreateGuid("notification:fixture-key");
        var expected = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("notification:fixture-key"))[..16]);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void CreateGuid_IsDeterministic()
    {
        var first = MonitorDeliveryIdentity.CreateGuid("delivery:fixture-key");
        var second = MonitorDeliveryIdentity.CreateGuid("delivery:fixture-key");

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void CreateGuid_RejectsWhitespace()
    {
        Assert.ThrowsExactly<ArgumentException>(() => MonitorDeliveryIdentity.CreateGuid(" "));
    }
}
