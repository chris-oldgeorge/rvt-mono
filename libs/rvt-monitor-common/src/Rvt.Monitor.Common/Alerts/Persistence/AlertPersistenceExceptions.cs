namespace Rvt.Monitor.Common.Alerts.Persistence;

public sealed class AlertTransientPersistenceException : Exception
{
    public AlertTransientPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class AlertOccurrenceConflictException : Exception
{
    public AlertOccurrenceConflictException(Exception innerException)
        : base("The alert occurrence already exists.", innerException)
    {
    }
}
