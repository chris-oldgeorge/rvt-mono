using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.CommonTests.Scheduling;

[TestClass]
public sealed class MonitorQuartzJobTests
{
    [TestMethod]
    public async Task Execute_DispatchesConfiguredJobName()
    {
        var dispatcher = new CapturingDispatcher(0);
        var job = new MonitorQuartzJob(dispatcher, NullLogger<MonitorQuartzJob>.Instance);
        var context = CreateContext("StoreMonitors");

        await job.Execute(context);

        Assert.AreEqual("StoreMonitors", dispatcher.JobName);
    }

    [TestMethod]
    public async Task Execute_ThrowsWhenDispatcherReturnsFailure()
    {
        var dispatcher = new CapturingDispatcher(2);
        var job = new MonitorQuartzJob(dispatcher, NullLogger<MonitorQuartzJob>.Instance);
        var context = CreateContext("MissingJob");

        await Assert.ThrowsExactlyAsync<JobExecutionException>(() => job.Execute(context));
    }

    [TestMethod]
    public async Task Execute_ThrowsWhenJobNameIsMissing()
    {
        var dispatcher = new CapturingDispatcher(0);
        var job = new MonitorQuartzJob(dispatcher, NullLogger<MonitorQuartzJob>.Instance);
        var context = CreateContext(null);

        await Assert.ThrowsExactlyAsync<JobExecutionException>(() => job.Execute(context));
    }

    private sealed class CapturingDispatcher(int exitCode) : IMonitorJobDispatcher
    {
        public string? JobName { get; private set; }

        public IReadOnlySet<string> SupportedJobNames { get; } =
            new HashSet<string> { "StoreMonitors", "MissingJob" };

        public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
        {
            JobName = jobName;
            return Task.FromResult(exitCode);
        }
    }

    private static IJobExecutionContext CreateContext(string? jobName)
    {
        var context = new Mock<IJobExecutionContext>();
        var jobDataMap = new JobDataMap();
        if (jobName is not null)
        {
            jobDataMap["JobName"] = jobName;
        }

        context.SetupGet(current => current.MergedJobDataMap)
            .Returns(jobDataMap);
        context.SetupGet(current => current.CancellationToken)
            .Returns(CancellationToken.None);
        context.SetupGet(current => current.JobDetail.Key)
            .Returns(new JobKey("job", "TestMonitor"));
        return context.Object;
    }
}
