using Rvt.Monitor.Common.Alerts.Persistence;

namespace Rvt.Monitor.Common.Alerts;

public interface IAlertIngressPort
{
    /// <summary>
    /// Durably records an alert signal and plans its deliveries.
    /// </summary>
    /// <remarks>
    /// Three failures are semantically distinct and callers should not treat
    /// them alike — only one of the six current call sites does.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The signal is malformed (blank or over-long text, a non-UTC event time,
    /// an unsupported alert type or channel bit, a non-finite level or limit,
    /// a negative averaging period or suppression window). This is a caller
    /// bug: retrying the same payload can never succeed.
    /// </exception>
    /// <exception cref="AlertUnknownMonitorException">
    /// The serial id is not in the fleet — typically a device webhook that
    /// arrived before the fleet import. Permanent for this payload, but the
    /// same serial may become valid after the next import.
    /// </exception>
    /// <exception cref="AlertTransientPersistenceException">
    /// A serialization failure or deadlock interrupted the commit. The signal
    /// is unchanged and the identical payload should be retried; ingress is
    /// idempotent on (source, source event key).
    /// </exception>
    Task<AlertIngressResult> AcceptAsync(
        AlertSignal signal,
        CancellationToken cancellationToken = default);
}
