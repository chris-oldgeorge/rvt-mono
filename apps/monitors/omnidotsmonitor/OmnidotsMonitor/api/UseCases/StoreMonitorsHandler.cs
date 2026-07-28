using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Api.Ports;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Diagnostics;

namespace Omnidots.Api.UseCases
{
    // Summary: Imports the Omnidots measuring-point catalogue into the monitor list.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiMonitors).
    public class StoreMonitorsHandler
    {
        private readonly IOmnidotsVendorGateway _gateway;
        private readonly IOmnidotsMonitorCommands monitorCommands;
        private readonly IOmnidotsOperationalCommands operationalCommands;
        private readonly bool testLocal;

        public StoreMonitorsHandler(
            IOmnidotsVendorGateway gateway,
            IOmnidotsMonitorCommands monitorCommands,
            IOmnidotsOperationalCommands operationalCommands,
            bool testLocal)
        {
            _gateway = gateway;
            this.monitorCommands = monitorCommands;
            this.operationalCommands = operationalCommands;
            this.testLocal = testLocal;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            MeasuringPointsResponse measuringPointsResponse = await _gateway.ListMeasuringPointsAsync(cancellationToken);
            var monitors = new List<VibrationMonitorDto>();
            foreach (MeasuringPoint mp in measuringPointsResponse.MeasuringPoints!)
            {
                try
                {
                    var dto = new VibrationMonitorDto(mp);
                    monitors.Add(dto);
                }
                catch (Exception e)
                {
                    RvtLogger.Logger.LogError(e, "StoreMonitors error with measuringPointId={Value1}", mp.Id);
                    operationalCommands.HandleException(string.Format("StoreMonitor id={0}", mp.Id), e);
                }
            }

            monitorCommands.WriteMonitorList(OmnidotsTestLocalMonitorFilter.ApplyCatalog(monitors, testLocal));
        }
    }
}
