using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Hosting;

namespace Rvt.Monitor.CommonTests.Hosting;

[TestClass]
public sealed class MonitorJobTelemetryTests
{
    [TestMethod]
    public async Task ExecuteAsync_LogsJobStartAndCompletion()
    {
        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        var logger = loggerFactory.CreateLogger("test");

        var exitCode = await MonitorJobTelemetry.ExecuteAsync(
            "MyAtmMonitor",
            "StoreMonitors",
            "one-shot",
            logger,
            () => Task.FromResult(0));

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(loggerProvider.Messages.Any(message => message.Contains("Monitor job started", StringComparison.Ordinal)));
        Assert.IsTrue(loggerProvider.Messages.Any(message => message.Contains("Monitor job completed", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_LogsJobFailureWhenExitCodeIsNonZero()
    {
        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        var logger = loggerFactory.CreateLogger("test");

        var exitCode = await MonitorJobTelemetry.ExecuteAsync(
            "SvantekMonitor",
            "StoreNoiseLevels",
            "quartz",
            logger,
            () => Task.FromResult(2));

        Assert.AreEqual(2, exitCode);
        Assert.IsTrue(loggerProvider.Messages.Any(message => message.Contains("Monitor job failed", StringComparison.Ordinal)));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName)
        {
            return new CapturingLogger(Messages);
        }

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
