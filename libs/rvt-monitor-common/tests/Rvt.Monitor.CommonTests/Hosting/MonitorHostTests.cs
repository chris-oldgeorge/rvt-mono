using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Monitor.Common.Hosting;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.CommonTests.Hosting;

[TestClass]
[DoNotParallelize]
public sealed class MonitorHostTests
{
    private static readonly IReadOnlySet<string> _supportedJobNames =
        new HashSet<string>(StringComparer.Ordinal) { "StoreMonitors" };

    [TestMethod]
    public async Task RunAsync_DelegatesConfiguredJobToMonitorJobRunner()
    {
        string? observedJobName = null;
        TestMarkerService? observedMarker = null;
        MonitorExecutionModeContext? observedExecutionMode = null;

        int exitCode = await MonitorHost.RunAsync<TestDispatcher>(
            ["--job", "StoreMonitors", "--hostBuilder:reloadConfigOnChange=false"],
            "TestMonitor",
            _supportedJobNames,
            (jobName, services, _) =>
            {
                observedJobName = jobName;
                observedMarker = services.GetService<TestMarkerService>();
                observedExecutionMode = services.GetRequiredService<MonitorExecutionModeContext>();
                return Task.FromResult(7);
            },
            _ => Assert.Fail("API mapping should not run for one-shot jobs."),
            configureServices: (services, _) => services.AddSingleton<TestMarkerService>());

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual("StoreMonitors", observedJobName);
        Assert.IsNotNull(observedMarker);
        Assert.AreEqual(MonitorExecutionMode.OneShot, observedExecutionMode?.Mode);
    }

    [TestMethod]
    public async Task RunAsync_ReturnsOneAndWritesExceptionMessageWhenJobRunnerThrows()
    {
        using StringWriter error = new();
        TextWriter originalError = Console.Error;
        Console.SetError(error);

        try
        {
            int exitCode = await MonitorHost.RunAsync<TestDispatcher>(
                ["--job", "StoreMonitors", "--hostBuilder:reloadConfigOnChange=false"],
                "TestMonitor",
                _supportedJobNames,
                (_, _, _) => throw new InvalidOperationException("job failed"),
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
        using StringWriter error = new();
        TextWriter originalError = Console.Error;
        Console.SetError(error);

        // The host now reads the job name from the shared argument parser, which
        // falls back to this variable. Clear it so an ambient value cannot turn
        // "no execution mode" into a one-shot run.
        string? originalJobName = Environment.GetEnvironmentVariable(
            MonitorJobArguments.JobNameEnvironmentVariable);
        Environment.SetEnvironmentVariable(MonitorJobArguments.JobNameEnvironmentVariable, null);

        try
        {
            int exitCode = await MonitorHost.RunAsync<TestDispatcher>(
                ["--hostBuilder:reloadConfigOnChange=false"],
                "TestMonitor",
                _supportedJobNames,
                (_, _, _) => Task.FromResult(0),
                _ => Assert.Fail("API mapping should not run when API mode is disabled."));

            Assert.AreEqual(2, exitCode);
            Assert.AreEqual(
                "No monitor execution mode configured. Set MonitorApi:Enabled=true, MonitorScheduler:Enabled=true, or pass --job <name>." + Environment.NewLine,
                error.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            Environment.SetEnvironmentVariable(
                MonitorJobArguments.JobNameEnvironmentVariable,
                originalJobName);
        }
    }

    [TestMethod]
    public async Task RunAsync_PassesTheHostShutdownTokenToTheOneShotJob()
    {
        CancellationToken observedToken = default;
        IHostApplicationLifetime? observedLifetime = null;

        int exitCode = await MonitorHost.RunAsync<TestDispatcher>(
            ["--job", "StoreMonitors", "--hostBuilder:reloadConfigOnChange=false"],
            "TestMonitor",
            _supportedJobNames,
            (_, services, cancellationToken) =>
            {
                observedToken = cancellationToken;
                observedLifetime = services.GetRequiredService<IHostApplicationLifetime>();
                return Task.FromResult(0);
            },
            _ => Assert.Fail("API mapping should not run for one-shot jobs."));

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(
            observedToken.CanBeCanceled,
            "A one-shot job must receive a live shutdown token, not CancellationToken.None.");
        Assert.AreEqual(observedLifetime?.ApplicationStopping, observedToken);
    }

    [TestMethod]
    public async Task RunAsync_ReportsShutdownCancellationAsFailureWithoutTheRawCancellationMessage()
    {
        using StringWriter error = new();
        TextWriter originalError = Console.Error;
        Console.SetError(error);

        try
        {
            int exitCode = await MonitorHost.RunAsync<TestDispatcher>(
                ["--job", "StoreMonitors", "--hostBuilder:reloadConfigOnChange=false"],
                "TestMonitor",
                _supportedJobNames,
                (_, services, cancellationToken) =>
                {
                    services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(0);
                },
                _ => Assert.Fail("API mapping should not run for one-shot jobs."));

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
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
