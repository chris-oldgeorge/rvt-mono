using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rvt.Monitor.Common.Diagnostics;

namespace Rvt.Monitor.CommonTests.Diagnostics;

/// <summary>
/// Reading the shared logger before a host configured it used to throw, so a
/// diagnostic statement could itself become the failure — and the message named
/// a monitor that had nothing to do with the caller.
/// </summary>
/// <remarks>
/// This suite touches process-wide state, so it does not run in parallel and
/// restores a configured logger afterwards. Assertions are deliberately limited
/// to properties that hold even if another suite configures logging
/// concurrently.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class RvtLoggerTests
{
    [TestCleanup]
    public void RestoreConfiguredLogger() =>
        RvtLogger.CreateLogger(NullLoggerFactory.Instance, nameof(RvtLoggerTests));

    [TestMethod]
    public void Logger_BeforeConfiguration_DegradesInsteadOfThrowing()
    {
        RvtLogger.Reset();

        ILogger logger = RvtLogger.Logger;

        Assert.IsNotNull(logger);
        // The whole point: a logging call on an unconfigured logger is a no-op,
        // not an exception that takes the caller down.
        logger.LogInformation("Diagnostics must never take the process down.");
        logger.LogError(new InvalidOperationException("boom"), "Failure while unconfigured.");
    }

    [TestMethod]
    public void Logger_AfterConfiguration_ReturnsTheHostLogger()
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace));

        RvtLogger.CreateLogger(factory, "category");

        Assert.IsTrue(RvtLogger.IsConfigured);
        Assert.IsNotInstanceOfType<NullLogger>(RvtLogger.Logger);
    }

    [TestMethod]
    public void CreateLogger_WithoutAFactory_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => RvtLogger.CreateLogger(null!, "category"));
    }

    [TestMethod]
    public void CreateLogger_WithABlankCategory_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            RvtLogger.CreateLogger(NullLoggerFactory.Instance, "   "));
    }
}
