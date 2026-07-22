using Omnidots.Api.Db;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace Omnidots.Api
{
    // Summary: Reads the Omnidots monitor list with the optional testlocal demo filter applied.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiMonitors).
    public class OmnidotsMonitorReader
    {
        private readonly IOmnidotsMonitorQueries monitorQueries;
        private readonly bool testLocal;

        public OmnidotsMonitorReader(IOmnidotsMonitorQueries monitorQueries, bool testLocal)
        {
            this.monitorQueries = monitorQueries;
            this.testLocal = testLocal;
        }

        public List<VibrationMonitorDto> ReadMonitors(DateTime? lastDataTime = null)
        {
            try
            {
                return OmnidotsTestLocalMonitorFilter.Apply(monitorQueries.ReadMonitorList(lastDataTime), testLocal);
            }
            catch (Exception e)
            {
                throw AdapterException.Of("ReadMonitors", e);
            }
        }
    }
}
