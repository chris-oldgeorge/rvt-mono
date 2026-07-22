using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Model.Dto;
using Svantek.Model.Http;

namespace Svantek.Api.UseCases
{
    // Summary: Imports the SvanNET project/station catalogue into the monitor list.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the SvantekApi partials (SvantekApiMonitors).
    public class StoreMonitorsHandler
    {
        private readonly SvantekHttpGateway gateway;
        private readonly ISvantekMonitorCommands monitorCommands;
        private readonly ISvantekOperationalCommands operationalCommands;
        private readonly bool testLocal;

        public StoreMonitorsHandler(
            SvantekHttpGateway gateway,
            ISvantekMonitorCommands monitorCommands,
            ISvantekOperationalCommands operationalCommands,
            bool testLocal)
        {
            this.gateway = gateway;
            this.monitorCommands = monitorCommands;
            this.operationalCommands = operationalCommands;
            this.testLocal = testLocal;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            RvtLogger.Logger.LogDebug("StoreMonitors reading projects API");
            var projects = await gateway.GetProjectsAsync(cancellationToken).ConfigureAwait(false);
            RvtLogger.Logger.LogDebug("StoreMonitors reading stations API");
            var stations = await gateway.GetStationsAsync(cancellationToken).ConfigureAwait(false);
            var failures = new SvantekFailureCollector(operationalCommands);

            foreach (var project in projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var identifier = $"StoreMonitors project {project.id}";
                try
                {
                    var dtos = new List<NoiseMonitorDto>();
                    foreach (var projectStation in project.stations)
                    {
                        var station = stations.FirstOrDefault(x => x.serial.ToString() == projectStation.serial);
                        if (station == null)
                        {
                            RvtLogger.Logger.LogDebug(
                                "StoreMonitors reading, no serial set for {ProjectStation}",
                                projectStation.name);
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

                    await monitorCommands.WriteMonitorListAsync(
                        SvantekTestLocalMonitorFilter.Apply(dtos, testLocal),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures.Capture(identifier, exception);
                }
            }

            failures.ThrowIfAny("StoreMonitors");
        }
    }
}
