using AirQ.Api.Db;
using AirQ.Model.Dto;
using Rvt.Monitor.Common.Diagnostics;

namespace AirQ.Api;

// Summary: Reads the AirQ monitor list shared by the use-case handlers.
// Major updates:
// - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitors).
public class AirQMonitorReader(
    IAirQMonitorQueries monitorQueries,
    AirQTestLocalMonitorFilter testLocalFilter)
{
    private readonly IAirQMonitorQueries _monitorQueries = monitorQueries;
    private readonly AirQTestLocalMonitorFilter _testLocalFilter = testLocalFilter;

    public List<NoiseMonitorDto> ReadMonitors(DateTime? lastDataTime = null)
    {
        try
        {
            return _testLocalFilter.Apply(_monitorQueries.ReadMonitorList(lastDataTime));
        }
        catch (Exception e)
        {
            throw AdapterException.Of("ReadMonitors", e);
        }
    }
}
