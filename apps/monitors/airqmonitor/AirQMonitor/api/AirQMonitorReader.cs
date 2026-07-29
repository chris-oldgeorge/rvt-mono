using AirQ.Api.Db;
using AirQ.Model.Dto;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace AirQ.Api
{
    // Summary: Reads the AirQ monitor list shared by the use-case handlers.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitors).
    public class AirQMonitorReader
    {
        private readonly IAirQMonitorQueries monitorQueries;
        private readonly AirQTestLocalMonitorFilter testLocalFilter;

        public AirQMonitorReader(
            IAirQMonitorQueries monitorQueries,
            AirQTestLocalMonitorFilter testLocalFilter)
        {
            this.monitorQueries = monitorQueries;
            this.testLocalFilter = testLocalFilter;
        }

        public List<NoiseMonitorDto> ReadMonitors(DateTime? lastDataTime = null)
        {
            try
            {
                return testLocalFilter.Apply(monitorQueries.ReadMonitorList(lastDataTime));
            }
            catch (Exception e)
            {
                throw AdapterException.Of("ReadMonitors", e);
            }
        }
    }
}
