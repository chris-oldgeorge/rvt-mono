namespace Rvt.Monitor.Common.Alerts.Persistence;

public sealed class AlertTransientPersistenceException(string message, Exception innerException) : Exception(message, innerException)
{
}

public sealed class AlertOccurrenceConflictException(Exception innerException) : Exception("The alert occurrence already exists.", innerException)
{
}

// Summary: A signal referenced a monitor serial that is not registered; this is a
// permanent (non-transient) outcome, so callers must not retry the same payload.
public sealed class AlertUnknownMonitorException(string serialId)
    : Exception($"No monitor is registered for serial id '{serialId}'.")
{
    public string SerialId { get; } = serialId;
}
