using AirQ.Api.Db;
using AirQ.Api.Ports;
using AirQ.Model.Dto;
using AirQ.Model.Http;

namespace AirQ.Api.UseCases
{
    // Summary: Imports the AirQ instrument catalogue and metadata into the monitor list.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitors).
    public class StoreMonitorsHandler
    {
        private readonly IAirQVendorGateway gateway;
        private readonly IAirQMonitorCommands monitorCommands;
        private readonly IAirQOperationalCommands operationalCommands;
        private readonly AirQTestLocalMonitorFilter testLocalFilter;

        public StoreMonitorsHandler(
            IAirQVendorGateway gateway,
            IAirQMonitorCommands monitorCommands,
            IAirQOperationalCommands operationalCommands,
            AirQTestLocalMonitorFilter testLocalFilter)
        {
            this.gateway = gateway;
            this.monitorCommands = monitorCommands;
            this.operationalCommands = operationalCommands;
            this.testLocalFilter = testLocalFilter;
        }

        public async Task RunAsync(string userId, string userAuth, CancellationToken cancellationToken = default)
        {
            List<InstrumentResponse> monitors;
            try
            {
                monitors = testLocalFilter.ApplyCatalog(await gateway.GetMonitorsAsync(userId, userAuth, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                operationalCommands.HandleException("StoreMonitors", e);
                throw;
            }

            var dtos = new List<NoiseMonitorDto>();
            var failures = new List<Exception>();
            foreach (var monitor in monitors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var metaData = await GetMetaDataAsync(userId: userId, userAuth: userAuth,
                                               model: monitor.Name!, serialId: monitor.InstrumentID!,
                                               cancellationToken: cancellationToken);
                    dtos.Add(new NoiseMonitorDto(monitor, metaData.FirstOrDefault() ?? new MetaDataResponse()));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    operationalCommands.HandleException($"StoreMonitors SerialId={monitor.InstrumentID}", e);
                    failures.Add(e);
                }
            }

            monitorCommands.WriteMonitorList(dtos);
            if (failures.Count > 0)
            {
                throw new AggregateException("One or more AirQ monitor catalogue imports failed.", failures);
            }
        }

        private async Task<List<MetaDataResponse>> GetMetaDataAsync(string userId, string userAuth, string model, string serialId, CancellationToken cancellationToken)
        {

            if ("iDB".Equals(model))
            {
                // iDB sevices do not report metadata
                return EmptyMetaData();


            }

            try
            {
                return await gateway.GetMetaDataAsync(userId, userAuth, serialId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                operationalCommands.HandleException("GetMetaData", e);
                return EmptyMetaData();
            }

        }

        private static List<MetaDataResponse> EmptyMetaData()
        {
            return new List<MetaDataResponse>
                {
                    new MetaDataResponse()
                };
        }
    }
}
