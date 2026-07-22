using Microsoft.Extensions.DependencyInjection;
using Rvt.Monitor.Common.Hosting;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.CommonTests.Hosting;

[TestClass]
[DoNotParallelize]
public sealed class MonitorHostTests
{
    [TestMethod]
    public async Task RunAsync_DelegatesConfiguredJobToMonitorJobRunner()
    {
        string? observedJobName = null;
        TestMarkerService? observedMarker = null;
        MonitorExecutionModeContext? observedExecutionMode = null;

        var exitCode = await MonitorHost.RunAsync<TestDispatcher>(
            ["--job", "StoreMonitors", "--hostBuilder:reloadConfigOnChange=false"],
            "TestMonitor",
            _ => "StoreMonitors",
            (jobName, services) =>
            {
                observedJobName = jobName;
                observedMarker = services.GetService<TestMarkerService>();
                observedExecutionMode = services.GetRequiredService<MonitorExecutionModeContext>();
                return Task.FromResult(7);
            },
            _ => Assert.Fail("API mapping should not run for one-shot jobs."),
            configureServices: services => services.AddSingleton<TestMarkerService>());

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual("StoreMonitors", observedJobName);
        Assert.IsNotNull(observedMarker);
        Assert.AreEqual(MonitorExecutionMode.OneShot, observedExecutionMode?.Mode);
    }

    [TestMethod]
    public async Task RunAsync_ReturnsOneAndWritesExceptionMessageWhenJobRunnerThrows()
    {
        using var error = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(error);

        try
        {
            var exitCode = await MonitorHost.RunAsync<TestDispatcher>(
                ["--job", "StoreMonitors", "--hostBuilder:reloadConfigOnChange=false"],
                "TestMonitor",
                _ => "StoreMonitors",
                (_, _) => throw new InvalidOperationException("job failed"),
                _ => Assert.Fail("API mapping should not run for one-shot jobs."));

            Assert.AreEqual(1, exitCode);
            Assert.Contains("job failed", error.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [TestMethod]
    public async Task RunAsync_ReturnsTwoAndWritesErrorWhenNoExecutionModeIsConfigured()
    {
        using var error = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(error);

        try
        {
            var exitCode = await MonitorHost.RunAsync<TestDispatcher>(
                ["--hostBuilder:reloadConfigOnChange=false"],
                "TestMonitor",
                _ => null,
                (_, _) => Task.FromResult(0),
                _ => Assert.Fail("API mapping should not run when API mode is disabled."));

            Assert.AreEqual(2, exitCode);
            Assert.AreEqual(
                "No monitor execution mode configured. Set MonitorApi:Enabled=true, MonitorScheduler:Enabled=true, or pass --job <name>." + Environment.NewLine,
                error.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    private sealed class TestMarkerService
    {
    }

    private sealed class TestDispatcher : IMonitorJobDispatcher
    {
        public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string> { "StoreMonitors" };

        public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }
}
