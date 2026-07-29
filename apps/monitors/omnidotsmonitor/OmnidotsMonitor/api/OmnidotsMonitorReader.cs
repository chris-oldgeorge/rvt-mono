using Omnidots.Api.Db;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Diagnostics;

namespace Omnidots.Api
{
    // Summary: Reads the Omnidots monitor list with the optional testlocal demo filter applied.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiMonitors).
    public class OmnidotsMonitorReader
    {
        private readonly IOmnidotsMonitorQueries _monitorQueries;
        private readonly bool _testLocal;

        public OmnidotsMonitorReader(IOmnidotsMonitorQueries monitorQueries, bool testLocal)
        {
            _monitorQueries = monitorQueries;
            _testLocal = testLocal;
        }

        public List<VibrationMonitorDto> ReadMonitors(DateTime? lastDataTime = null)
        {
            try
            {
                return OmnidotsTestLocalMonitorFilter.Apply(_monitorQueries.ReadMonitorList(lastDataTime), _testLocal);
            }
            catch (Exception e)
            {
                throw AdapterException.Of("ReadMonitors", e);
            }
        }
    }
}
