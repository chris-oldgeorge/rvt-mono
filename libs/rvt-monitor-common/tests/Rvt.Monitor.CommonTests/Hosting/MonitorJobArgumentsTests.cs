using Rvt.Monitor.Common.Hosting;

namespace Rvt.Monitor.CommonTests.Hosting;

/// <summary>
/// Pins the one-shot job-name contract that every monitor used to reimplement.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class MonitorJobArgumentsTests
{
    private string? _originalJobName;

    [TestInitialize]
    public void CaptureEnvironment()
    {
        _originalJobName = Environment.GetEnvironmentVariable(
            MonitorJobArguments.JobNameEnvironmentVariable);
        Environment.SetEnvironmentVariable(MonitorJobArguments.JobNameEnvironmentVariable, null);
    }

    [TestCleanup]
    public void RestoreEnvironment() =>
        Environment.SetEnvironmentVariable(
            MonitorJobArguments.JobNameEnvironmentVariable,
            _originalJobName);

    [TestMethod]
    public void GetJobName_ReadsTheValueAfterTheJobSwitch()
    {
        Assert.AreEqual(
            "StoreMonitors",
            MonitorJobArguments.GetJobName(["--job", "StoreMonitors", "--other", "value"]));
    }

    [TestMethod]
    public void GetJobName_ReturnsNullWhenNothingNamesAJob()
    {
        Assert.IsNull(MonitorJobArguments.GetJobName(["--hostBuilder:reloadConfigOnChange=false"]));
    }

    [TestMethod]
    public void GetJobName_FallsBackToTheEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable(
            MonitorJobArguments.JobNameEnvironmentVariable,
            "CheckForOfflineMonitors");

        Assert.AreEqual("CheckForOfflineMonitors", MonitorJobArguments.GetJobName([]));
    }

    [TestMethod]
    public void GetJobName_PrefersTheCommandLineOverTheEnvironment()
    {
        Environment.SetEnvironmentVariable(
            MonitorJobArguments.JobNameEnvironmentVariable,
            "CheckForOfflineMonitors");

        Assert.AreEqual("StoreMonitors", MonitorJobArguments.GetJobName(["--job", "StoreMonitors"]));
    }

    [TestMethod]
    public void GetJobName_TreatsATrailingJobSwitchAsUnset()
    {
        Environment.SetEnvironmentVariable(
            MonitorJobArguments.JobNameEnvironmentVariable,
            "CheckForOfflineMonitors");

        Assert.AreEqual("CheckForOfflineMonitors", MonitorJobArguments.GetJobName(["--job"]));
    }
}
