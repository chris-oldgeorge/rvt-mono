using Microsoft.Extensions.Configuration;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.CommonTests.Scheduling;

[TestClass]
public sealed class MonitorInfrastructureOptionsTests
{
    [TestMethod]
    public void Bind_DefaultsToLocal()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = MonitorInfrastructureOptions.Bind(configuration);

        Assert.AreEqual(MonitorInfrastructure.Local, options.Infrastructure);
        Assert.IsTrue(options.AllowsQuartzScheduler);
    }

    [TestMethod]
    public void Bind_ReadsAzureInfrastructure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Infrastructure"] = "azure"
            })
            .Build();

        var options = MonitorInfrastructureOptions.Bind(configuration);

        Assert.AreEqual(MonitorInfrastructure.Azure, options.Infrastructure);
        Assert.IsFalse(options.AllowsQuartzScheduler);
    }

    [TestMethod]
    public void Bind_ReadsPrefixedAzureInfrastructure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT:Infrastructure"] = "azure"
            })
            .Build();

        var options = MonitorInfrastructureOptions.Bind(configuration);

        Assert.AreEqual(MonitorInfrastructure.Azure, options.Infrastructure);
        Assert.IsFalse(options.AllowsQuartzScheduler);
    }

    [TestMethod]
    public void Bind_ThrowsForUnknownInfrastructure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Infrastructure"] = "serverless"
            })
            .Build();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            MonitorInfrastructureOptions.Bind(configuration));

        Assert.Contains("serverless", exception.Message);
        Assert.Contains("local", exception.Message);
        Assert.Contains("azure", exception.Message);
    }
}
