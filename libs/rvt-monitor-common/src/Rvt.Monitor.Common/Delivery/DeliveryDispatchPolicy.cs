using Rvt.Communication.Abstractions;

namespace Rvt.Monitor.Common.Delivery;

// Summary: The shared terminal/error policy behind both outbox dispatchers.
// Major updates:
// - 2026-07-29 Legacy retirement step 6: extracted from
//   MonitorDeliveryDispatcher and DurableAlertDispatcher. This aligned the
//   error-truncation divergence — the alert dispatcher persisted unbounded
//   error text while the monitor dispatcher capped it at 1024 characters.

/// <summary>
/// Decides when a failed delivery is terminal and shapes the error text that
/// is persisted with the outcome. Persisted errors carry provider detail only
/// for <see cref="DeliveryException"/> (whose message is the safe, classified
/// contract); anything else is reduced to its exception type so raw provider
/// payloads cannot leak into the outbox.
/// </summary>
public static class DeliveryDispatchPolicy
{
    public const int MaximumErrorLength = 1024;

    public static bool IsTerminal(Exception exception, int attemptCount, int maxAttempts) =>
        exception is DeliveryException { FailureKind: not DeliveryFailureKind.Transient } ||
        attemptCount >= maxAttempts;

    public static string SafeError(Exception exception, string fallback)
    {
        string error = exception is DeliveryException
            ? exception.Message
            : fallback;
        return error.Length <= MaximumErrorLength ? error : error[..MaximumErrorLength];
    }
}
