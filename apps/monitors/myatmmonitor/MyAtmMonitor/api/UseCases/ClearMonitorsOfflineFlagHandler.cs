using MyAtm.Api.Db;
using MyAtm.Model.Dto;

namespace MyAtm.Api.UseCases;

// Summary: Clears the offline flag on every monitor of a customer.
// Major updates:
// - 2026-07-12 God-class split: extracted from the MyAtmApi partials (MyAtmApiMonitors).
public class ClearMonitorsOfflineFlagHandler
{
    private readonly MyAtmMonitorReader monitorReader;
    private readonly IMyAtmMonitorCommands monitorCommands;

    public ClearMonitorsOfflineFlagHandler(
        MyAtmMonitorReader monitorReader,
        IMyAtmMonitorCommands monitorCommands)
    {
        this.monitorReader = monitorReader;
        this.monitorCommands = monitorCommands;
    }

    public void Run(int customerId)
    {
        List<DustMonitorDto>? monitors = monitorReader.ReadMonitors(customerId);

        foreach (DustMonitorDto monitor in monitors!)
        {
            monitorCommands.SetMonitorOffline(monitor.Id, false);
        }
    }
}
