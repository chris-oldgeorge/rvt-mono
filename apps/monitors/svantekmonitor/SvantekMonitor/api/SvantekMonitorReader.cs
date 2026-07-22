using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Svantek.Api.Db;
using SvantekMonitor.model.dto;

namespace Svantek.Api
{
    // Summary: Reads the Svantek monitor list with the optional testlocal demo filter applied.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the SvantekApi partials (SvantekApiMonitors).
    public class SvantekMonitorReader
    {
        private readonly ISvantekMonitorQueries monitorQueries;
        private readonly bool testLocal;

        public SvantekMonitorReader(ISvantekMonitorQueries monitorQueries, bool testLocal)
        {
            this.monitorQueries = monitorQueries;
            this.testLocal = testLocal;
        }

        public List<NoiseMonitorReadDto> ReadMonitors(DateTime? lastDataTime = null)
        {
            try
            {
                return SvantekTestLocalMonitorFilter.Apply(monitorQueries.ReadMonitorList(lastDataTime), testLocal);
            }
            catch (Exception e)
            {
                throw AdapterException.Of("ReadMonitors", e);
            }
        }

        public async Task<List<NoiseMonitorReadDto>> ReadMonitorsAsync(
            DateTime? lastDataTime = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var monitors = await monitorQueries
                    .ReadMonitorListAsync(lastDataTime, cancellationToken)
                    .ConfigureAwait(false);
                return SvantekTestLocalMonitorFilter.Apply(monitors, testLocal);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("ReadMonitors", e);
            }
        }
    }
}
