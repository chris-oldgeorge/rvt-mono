using Microsoft.Extensions.Logging;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

namespace MyAtm.Api.UseCases
{
    // Summary: Fetches and stores accessory info readings for every monitor of a customer.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the MyAtmApi partials (MyAtmApiAccessoryInfo).
    public class StoreAccessoryInfoHandler
    {
        private readonly MyAtmHttpGateway gateway;
        private readonly MyAtmMonitorReader monitorReader;
        private readonly IMyAtmAccessoryCommands accessoryCommands;
        private readonly IMyAtmMeasurementQueries measurementQueries;
        private readonly IMyAtmOperationalCommands operationalCommands;
        private readonly int maxPagesPerMonitorPerRun;

        public StoreAccessoryInfoHandler(
            MyAtmHttpGateway gateway,
            MyAtmMonitorReader monitorReader,
            IMyAtmAccessoryCommands accessoryCommands,
            IMyAtmMeasurementQueries measurementQueries,
            IMyAtmOperationalCommands operationalCommands,
            int maxPagesPerMonitorPerRun)
        {
            this.gateway = gateway;
            this.monitorReader = monitorReader;
            this.accessoryCommands = accessoryCommands;
            this.measurementQueries = measurementQueries;
            this.operationalCommands = operationalCommands;
            this.maxPagesPerMonitorPerRun = maxPagesPerMonitorPerRun;
        }

        public async Task RunAsync(int customerId, CancellationToken cancellationToken = default)
        {
            var customerDtos = monitorReader.ReadMonitors(customerId);
            if (customerDtos == null)
            {
                return;
            }

            var failures = new MyAtmFailureCollector(operationalCommands);
            foreach (var customerDto in customerDtos)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var cursor = DateTimeUtil.AsUtc(
                        measurementQueries.ReadLatestAccessoryTimestamp(customerDto.SerialId) ?? MyAtmApi.JAN1_1970);
                    for (var pageNumber = 0; pageNumber < maxPagesPerMonitorPerRun; pageNumber++)
                    {
                        var page = await gateway.HttpGetAccessoryInfoPageAsync(
                            customerId,
                            customerDto.SerialId,
                            cursor,
                            cancellationToken);
                        var dtos = page.Measurements
                            .Select(accessoryInfo => new AccessoryInfoDto(customerDto.SerialId, accessoryInfo))
                            .GroupBy(dto => DateTimeUtil.AsUtc(dto.SampleTime))
                            .Select(group => group.First())
                            .OrderBy(dto => dto.SampleTime)
                            .ToList();

                        RvtLogger.Logger.LogInformation(
                            "StoreAccessoryInfo page={PageNumber} number of dtos to insert={Count} serialId={SerialId} cursor={Cursor}",
                            pageNumber + 1,
                            dtos.Count,
                            customerDto.SerialId,
                            cursor);

                        if (dtos.Count > 0)
                        {
                            await accessoryCommands.InsertAccessoryPageAsync(dtos, cancellationToken);
                        }

                        if (!page.HasMore || !page.NextCursor.HasValue || page.NextCursor <= cursor)
                        {
                            break;
                        }

                        cursor = DateTimeUtil.AsUtc(page.NextCursor.Value);
                    }
                }
                catch (Exception exception)
                {
                    TryLogFailure(exception, customerDto.SerialId);
                    failures.Capture(
                        $"StoreAccessoryInfo SerialId={customerDto.SerialId}",
                        exception,
                        cancellationToken);
                }
            }

            failures.ThrowIfAny("StoreAccessoryInfo");
        }

        private static void TryLogFailure(Exception exception, string serialId)
        {
            try
            {
                RvtLogger.Logger.LogError(
                    exception,
                    "StoreAccessoryInfo failed for serialId={SerialId}",
                    serialId);
            }
            catch
            {
                // Operational recording and the final aggregate remain authoritative.
            }
        }
    }
}
