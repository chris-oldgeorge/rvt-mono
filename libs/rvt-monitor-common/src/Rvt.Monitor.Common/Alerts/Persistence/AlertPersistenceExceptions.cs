namespace Rvt.Monitor.Common.Alerts.Persistence;

public sealed class AlertTransientPersistenceException(string message, Exception innerException) : Exception(message, innerException)
{
}

public sealed class AlertOccurrenceConflictException(Exception innerException) : Exception("The alert occurrence already exists.", innerException)
{
}
