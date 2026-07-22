namespace Rvt.Monitor.Common.Delivery;

public sealed class MonitorDeliveryDispatchException : Exception
{
    public MonitorDeliveryDispatchException(IReadOnlyList<Exception> failures)
        : base("One or more monitor deliveries failed.")
    {
        ArgumentNullException.ThrowIfNull(failures);
        Failures = failures.ToArray();
    }

    public IReadOnlyList<Exception> Failures { get; }
}
