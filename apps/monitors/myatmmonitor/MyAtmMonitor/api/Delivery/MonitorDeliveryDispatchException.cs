// The namespace is retained from the shared-kernel folder this file moved out
// of, so its consumers keep compiling; IDE0130 would force a rename ripple.
#pragma warning disable IDE0130
namespace Rvt.Monitor.Common.Delivery;

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
