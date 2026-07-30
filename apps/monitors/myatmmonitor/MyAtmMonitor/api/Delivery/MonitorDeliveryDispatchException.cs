namespace MyAtm.Delivery;

public sealed class MonitorDeliveryDispatchException : Exception
{
    public MonitorDeliveryDispatchException(IReadOnlyList<Exception> failures)
        : base("One or more monitor deliveries failed.")
    {
        ArgumentNullException.ThrowIfNull(failures);
        Failures = [.. failures];
    }

    public IReadOnlyList<Exception> Failures { get; }
}
