using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Svantek.Api.Db;
using Svantek.Api.Ports;
using Svantek.Model.Dto;
using Svantek.Model.Http;

namespace Svantek.Api.UseCases
{
    // Summary: Imports the SvanNET project/station catalogue into the monitor list.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the SvantekApi partials (SvantekApiMonitors).
    public class StoreMonitorsHandler
    {
        private readonly ISvantekVendorGateway _gateway;
        private readonly ISvantekMonitorCommands _monitorCommands;
        private readonly ISvantekOperationalCommands _operationalCommands;
        private readonly bool _testLocal;

        public StoreMonitorsHandler(
            ISvantekVendorGateway gateway,
            ISvantekMonitorCommands monitorCommands,
            ISvantekOperationalCommands operationalCommands,
            bool testLocal)
        {
            _gateway = gateway;
            _monitorCommands = monitorCommands;
            _operationalCommands = operationalCommands;
            _testLocal = testLocal;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            RvtLogger.Logger.LogDebug("StoreMonitors reading projects API");
            List<Project> projects = await _gateway.GetProjectsAsync(cancellationToken).ConfigureAwait(false);
            RvtLogger.Logger.LogDebug("StoreMonitors reading stations API");
            List<Station> stations = await _gateway.GetStationsAsync(cancellationToken).ConfigureAwait(false);
            SvantekFailureCollector failures = new(_operationalCommands);

            foreach (Project project in projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string identifier = $"StoreMonitors project {project.id}";
                try
                {
                    List<NoiseMonitorDto> dtos = [];
                    foreach (ProjectStation projectStation in project.stations)
                    {
                        Station? station = stations.FirstOrDefault(x => x.serial.ToString() == projectStation.serial);
                        if (station == null)
                        {
                            if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
                            {
                                RvtLogger.Logger.LogDebug(
                                    "StoreMonitors reading, no serial set for {ProjectStation}",
                                    projectStation.name);
                            }

                            continue;
                        }

                        dtos.Add(new NoiseMonitorDto
                        {
                            Id = Guid.NewGuid(),
                            SerialId = projectStation.serial,
                            ProjectId = Convert.ToInt32(project.id),
                            PointId = Convert.ToInt32(projectStation.point_id),
                            ListedAtTime = DateTime.Now,
                            Model = station.type,
                            CustomerDisplayName = projectStation.short_name,
                            FirmwareVersion = station.meterfirmware.ToString(),
                            Active = station.active,
                            LastLogin = station.lastlogin,
                            LastLogout = station.lastlogout,
                            IsOnline = station.isonline,
                            LastStatusTimestamp = station.laststatustimestamp,
                            BatteryCharge = station.batterycharge,
                            BatteryTimeToEmpty = station.batterytimetoempty,
                            PowerSource = station.powersource,
                            IsBatteryCharging = station.isbatterycharging,
                            GsmSignalQuality = station.gsmsignalquality,
                            MeasurementState = station.measurementstate,
                        });
                    }

                    await _monitorCommands.WriteMonitorListAsync(
                        SvantekTestLocalMonitorFilter.Apply(dtos, _testLocal),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures.Capture(identifier, exception, cancellationToken);
                }
            }

            failures.ThrowIfAny("StoreMonitors");
        }
    }
}
