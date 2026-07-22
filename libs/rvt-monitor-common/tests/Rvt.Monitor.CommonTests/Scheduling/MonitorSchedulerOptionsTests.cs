using Microsoft.Extensions.Configuration;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.CommonTests.Scheduling;

[TestClass]
public sealed class MonitorSchedulerOptionsTests
{
    [TestMethod]
    public void Bind_ReturnsEnabledJobsOnly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MonitorScheduler:Enabled"] = "true",
                ["MonitorScheduler:TimeZoneId"] = "UTC",
                ["MonitorScheduler:Jobs:0:Name"] = "StoreMonitors",
                ["MonitorScheduler:Jobs:0:Cron"] = "0 2 * * * ?",
                ["MonitorScheduler:Jobs:0:Enabled"] = "true",
                ["MonitorScheduler:Jobs:0:Description"] = "Fetch and store monitor list hourly at :02",
                ["MonitorScheduler:Jobs:1:Name"] = "StoreNoiseLevels",
                ["MonitorScheduler:Jobs:1:Cron"] = "0 0/5 * * * ?",
                ["MonitorScheduler:Jobs:1:Enabled"] = "false",
                ["MonitorScheduler:Jobs:1:Description"] = "Store noise levels every five minutes"
            })
            .Build();

        var options = MonitorSchedulerOptions.Bind(configuration);

        Assert.IsTrue(options.Enabled);
        Assert.AreEqual("UTC", options.TimeZoneId);
        CollectionAssert.AreEqual(
            new[] { "StoreMonitors" },
            options.GetEnabledJobs().Select(job => job.Name).ToArray());
    }
}
