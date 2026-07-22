using MyAtm.Api.Db;
using Rvt.Monitor.Common.Delivery;

namespace MyAtm.Api;

// Summary: Records only terminal shared-delivery failures in the MyAtm operational error log.
public sealed class MyAtmDeliveryFailureSink : IMonitorDeliveryFailureSink
{
    private readonly IMyAtmOperationalCommands operationalCommands;

    public MyAtmDeliveryFailureSink(IMyAtmOperationalCommands operationalCommands)
    {
        this.operationalCommands = operationalCommands ?? throw new ArgumentNullException(nameof(operationalCommands));
    }

    public Task RecordFailureAsync(
        MonitorDeliveryMessage message,
        string error,
        bool terminal,
        CancellationToken cancellationToken = default)
    {
        if (!terminal)
        {
            return Task.CompletedTask;
        }

        operationalCommands.HandleException(
            "Outbox delivery dead-lettered",
            new InvalidOperationException(error));
        return Task.CompletedTask;
    }
}
