using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Rvt.Monitor.Common.Diagnostics;


/// <summary>
/// Process-wide logger used by the monitor code that predates constructor
/// injection.
/// </summary>
/// <remarks>
/// This is a service locator and remains one; new code should take an
/// <see cref="ILogger"/> dependency instead. What has changed is that
/// reading it can no longer take a process down: it previously threw when a
/// logging call ran before <see cref="CreateLogger"/>, so a diagnostic
/// statement — the thing reached for when something is already wrong —
/// could itself become the failure, and the message named the wrong
/// monitor. An unconfigured logger now degrades to
/// <see cref="NullLogger"/>.
/// </remarks>
public class RvtLogger
{
    private static volatile ILogger _current = NullLogger.Instance;

    private RvtLogger()
    {
    }

    public static void CreateLogger(ILoggerFactory loggerFactory, string categoryName)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        _current = loggerFactory.CreateLogger(categoryName);
    }

    /// <summary>
    /// True once a host has supplied a real logger. Composition roots can
    /// assert this; nothing should branch on it to decide whether to log.
    /// </summary>
    public static bool IsConfigured => !ReferenceEquals(_current, NullLogger.Instance);

    public static ILogger Logger => _current;

    /// <summary>
    /// Restores the unconfigured state. Intended for tests that need to
    /// observe behaviour before a host configures logging.
    /// </summary>
    internal static void Reset() => _current = NullLogger.Instance;
}
