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
        private readonly IOmnidotsMonitorCommands _monitorCommands;
        private readonly IOmnidotsOperationalCommands _operationalCommands;
        private readonly bool _testLocal;

        public StoreMonitorsHandler(
            IOmnidotsVendorGateway gateway,
            IOmnidotsMonitorCommands monitorCommands,
            IOmnidotsOperationalCommands operationalCommands,
            bool testLocal)
        {
            _gateway = gateway;
            _monitorCommands = monitorCommands;
            _operationalCommands = operationalCommands;
            _testLocal = testLocal;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            MeasuringPointsResponse measuringPointsResponse = await _gateway.ListMeasuringPointsAsync(cancellationToken);
            List<VibrationMonitorDto> monitors = [];
            foreach (MeasuringPoint mp in measuringPointsResponse.MeasuringPoints!)
            {
                try
                {
                    VibrationMonitorDto dto = new(mp);
                    monitors.Add(dto);
                }
                catch (Exception e)
                {
                    RvtLogger.Logger.LogError(e, "StoreMonitors error with measuringPointId={Value1}", mp.Id);
                    _operationalCommands.HandleException(string.Format("StoreMonitor id={0}", mp.Id), e);
                }
            }

            _monitorCommands.WriteMonitorList(OmnidotsTestLocalMonitorFilter.ApplyCatalog(monitors, _testLocal));
        }
    }
}
