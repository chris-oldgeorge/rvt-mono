using Rvt.Monitor.IntegrationTesting;

namespace TestConsumer;

[TestClass]
public sealed class PackageSmokeTests
{
    [TestMethod]
    public void IntegrationFixtureLoadsFromThePackage() =>
        Assert.AreEqual(
            "Rvt.Monitor.IntegrationTesting",
            typeof(PostgreSqlIntegrationDatabase).Assembly.GetName().Name);
}
