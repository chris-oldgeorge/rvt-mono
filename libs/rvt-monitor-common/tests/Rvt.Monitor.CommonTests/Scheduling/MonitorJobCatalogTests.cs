using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.CommonTests.Scheduling;

/// <summary>
/// The catalog exists so a monitor's job names cannot be declared twice and
/// drift apart, which is what the dispatcher name set plus runner switch used
/// to allow.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class MonitorJobCatalogTests
{
    private static readonly string[] _expectedCatalogJobs = ["ClearOlderErrorMessages", "StoreMonitors"];
    private static readonly string[] _expectedStoreMonitorInvocation = ["StoreMonitors"];

    [TestMethod]
    public void JobNames_AreExactlyTheCatalogKeys()
    {
        MonitorJobCatalog<Recorder> catalog = new(
            "test monitor",
            new Dictionary<string, Func<Recorder, CancellationToken, Task>>(StringComparer.Ordinal)
            {
                ["StoreMonitors"] = (recorder, _) => recorder.RecordAsync("StoreMonitors"),
                ["ClearOlderErrorMessages"] = (recorder, _) => recorder.RecordAsync("ClearOlderErrorMessages")
            });

        CollectionAssert.AreEqual(
            _expectedCatalogJobs,
            catalog.JobNames.Order(StringComparer.Ordinal).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_InvokesTheMatchingJobAndReturnsZero()
    {
        Recorder recorder = new();
        MonitorJobCatalog<Recorder> catalog = CreateCatalog();

        int exitCode = await catalog.RunAsync("StoreMonitors", recorder, CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(_expectedStoreMonitorInvocation, recorder.Invoked.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_TrimsTheJobNameBeforeLookup()
    {
        Recorder recorder = new();
        MonitorJobCatalog<Recorder> catalog = CreateCatalog();

        int exitCode = await catalog.RunAsync("  StoreMonitors\t", recorder, CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(_expectedStoreMonitorInvocation, recorder.Invoked.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_IsCaseSensitive()
    {
        Recorder recorder = new();
        MonitorJobCatalog<Recorder> catalog = CreateCatalog();

        (int exitCode, _) = await CaptureErrorAsync(
            () => catalog.RunAsync("storemonitors", recorder, CancellationToken.None));

        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(recorder.Invoked);
    }

    [TestMethod]
    public async Task RunAsync_UnknownJob_ReturnsTwoAndNamesTheMonitor()
    {
        (int exitCode, string error) = await CaptureErrorAsync(
            () => CreateCatalog().RunAsync("NoSuchJob", new Recorder(), CancellationToken.None));

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual($"Unknown test monitor job 'NoSuchJob'.{Environment.NewLine}", error);
    }

    [TestMethod]
    public async Task RunAsync_PassesTheCallerToken_SoShutdownReachesTheJob()
    {
        using CancellationTokenSource cancellation = new();
        CancellationToken observed = default;
        MonitorJobCatalog<Recorder> catalog = new(
            "test monitor",
            new Dictionary<string, Func<Recorder, CancellationToken, Task>>(StringComparer.Ordinal)
            {
                ["StoreMonitors"] = (_, cancellationToken) =>
                {
                    observed = cancellationToken;
                    return Task.CompletedTask;
                }
            });

        await catalog.RunAsync("StoreMonitors", new Recorder(), cancellation.Token);

        Assert.AreEqual(cancellation.Token, observed);
    }

    [TestMethod]
    public async Task RunAsync_LetsJobFailuresSurface()
    {
        MonitorJobCatalog<Recorder> catalog = new(
            "test monitor",
            new Dictionary<string, Func<Recorder, CancellationToken, Task>>(StringComparer.Ordinal)
            {
                ["StoreMonitors"] = (_, _) => throw new InvalidOperationException("vendor failed")
            });

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => catalog.RunAsync("StoreMonitors", new Recorder(), CancellationToken.None));

        Assert.AreEqual("vendor failed", exception.Message);
    }

    private static async Task<(int ExitCode, string Error)> CaptureErrorAsync(Func<Task<int>> run)
    {
        using StringWriter error = new();
        TextWriter originalError = Console.Error;
        Console.SetError(error);

        try
        {
            return (await run(), error.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    private static MonitorJobCatalog<Recorder> CreateCatalog() => new(
        "test monitor",
        new Dictionary<string, Func<Recorder, CancellationToken, Task>>(StringComparer.Ordinal)
        {
            ["StoreMonitors"] = (recorder, _) => recorder.RecordAsync("StoreMonitors")
        });

    private sealed class Recorder
    {
        public List<string> Invoked { get; } = [];

        public Task RecordAsync(string jobName)
        {
            Invoked.Add(jobName);
            return Task.CompletedTask;
        }
    }
}
