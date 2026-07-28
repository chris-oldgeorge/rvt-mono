using Microsoft.Extensions.Configuration;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.CommonTests.Scheduling;

[TestClass]
public sealed class MonitorInfrastructureOptionsTests
{
    [TestMethod]
    public void Bind_DefaultsToLocal()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().Build();

        MonitorInfrastructureOptions options = MonitorInfrastructureOptions.Bind(configuration);

        Assert.AreEqual(MonitorInfrastructure.Local, options.Infrastructure);
        Assert.IsTrue(options.AllowsQuartzScheduler);
    }

    [TestMethod]
    public void Bind_ReadsAzureInfrastructure()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Infrastructure"] = "azure"
            })
            .Build();

        MonitorInfrastructureOptions options = MonitorInfrastructureOptions.Bind(configuration);

        Assert.AreEqual(MonitorInfrastructure.Azure, options.Infrastructure);
        Assert.IsFalse(options.AllowsQuartzScheduler);
    }

    [TestMethod]
    public void Bind_ReadsPrefixedAzureInfrastructure()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT:Infrastructure"] = "azure"
            })
            .Build();

        MonitorInfrastructureOptions options = MonitorInfrastructureOptions.Bind(configuration);

        Assert.AreEqual(MonitorInfrastructure.Azure, options.Infrastructure);
        Assert.IsFalse(options.AllowsQuartzScheduler);
    }

    [TestMethod]
    public void Bind_ThrowsForUnknownInfrastructure()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Infrastructure"] = "serverless"
            })
            .Build();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            MonitorInfrastructureOptions.Bind(configuration));

        Assert.Contains("serverless", exception.Message);
        Assert.Contains("local", exception.Message);
        Assert.Contains("azure", exception.Message);
    }
}
