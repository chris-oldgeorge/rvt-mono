using Microsoft.Extensions.Logging;
using MyAtm.Api.Db;
using MyAtm.Model.Dto;

namespace MyAtm.Api
{
    // Summary: Reads the MyAtm monitor list with the optional testlocal demo filter applied.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the MyAtmApi partials (MyAtmApiMonitors).
    public class MyAtmMonitorReader
    {
        private readonly IMyAtmMonitorQueries monitorQueries;
        private readonly IMyAtmOperationalCommands operationalCommands;
        private readonly bool testLocal;

        public MyAtmMonitorReader(
            IMyAtmMonitorQueries monitorQueries,
            IMyAtmOperationalCommands operationalCommands,
            bool testLocal)
        {
            this.monitorQueries = monitorQueries;
            this.operationalCommands = operationalCommands;
            this.testLocal = testLocal;
        }

        public List<DustMonitorDto>? ReadMonitors(int customerId, DateTime? dateTime = null)
        {
            try
            {
                return MyAtmTestLocalMonitorFilter.Apply(monitorQueries.ReadMonitorList(customerId, dateTime), testLocal);
            }
            catch (OperationCanceledException exception) when (exception.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                try
                {
                    Rvt.Monitor.Common.Diagnostics.RvtLogger.Logger.LogError(
                        exception,
                        "ReadMonitors failed for customerId={CustomerId} dateTime={DateTime}",
                        customerId,
                        dateTime);
                }
                catch
                {
                    // The original database failure remains authoritative when diagnostics are unavailable.
                }

                try
                {
                    operationalCommands.HandleException("ReadMonitors", exception);
                }
                catch (Exception operationalException)
                {
                    try
                    {
                        Rvt.Monitor.Common.Diagnostics.RvtLogger.Logger.LogError(
                            operationalException,
                            "ReadMonitors failed to record its database-query failure for customerId={CustomerId}",
                            customerId);
                    }
                    catch
                    {
                        // The original database failure remains authoritative when operational recording also fails.
                    }
                }

                throw;
            }
        }
    }
}
