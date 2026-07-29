using Microsoft.Extensions.Logging;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

namespace MyAtm.Api.UseCases;

// Summary: Fetches and stores accessory info readings for every monitor of a customer.
// Major updates:
// - 2026-07-12 God-class split: extracted from the MyAtmApi partials (MyAtmApiAccessoryInfo).
public class StoreAccessoryInfoHandler(
    MyAtmHttpGateway gateway,
    MyAtmMonitorReader monitorReader,
    IMyAtmAccessoryCommands accessoryCommands,
    IMyAtmMeasurementQueries measurementQueries,
    IMyAtmOperationalCommands operationalCommands,
    int maxPagesPerMonitorPerRun)
{
    private readonly MyAtmHttpGateway _gateway = gateway;
    private readonly MyAtmMonitorReader _monitorReader = monitorReader;
    private readonly IMyAtmAccessoryCommands _accessoryCommands = accessoryCommands;
    private readonly IMyAtmMeasurementQueries _measurementQueries = measurementQueries;
    private readonly IMyAtmOperationalCommands _operationalCommands = operationalCommands;
    private readonly int _maxPagesPerMonitorPerRun = maxPagesPerMonitorPerRun;

    public async Task RunAsync(int customerId, CancellationToken cancellationToken = default)
    {
        List<DustMonitorDto>? customerDtos = _monitorReader.ReadMonitors(customerId);
        if (customerDtos == null)
        {
            return;
        }

        MyAtmFailureCollector failures = new(_operationalCommands);
        foreach (DustMonitorDto customerDto in customerDtos)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                DateTime cursor = DateTimeUtil.AsUtc(
                    _measurementQueries.ReadLatestAccessoryTimestamp(customerDto.SerialId) ?? MyAtmApi.JAN1_1970);
                for (int pageNumber = 0; pageNumber < _maxPagesPerMonitorPerRun; pageNumber++)
                {
                    MyAtmMeasurementPage<AccessoryInfo> page = await _gateway.HttpGetAccessoryInfoPageAsync(
                        customerId,
                        customerDto.SerialId,
                        cursor,
                        cancellationToken);
                    List<AccessoryInfoDto> dtos = [.. page.Measurements
                        .Select(accessoryInfo => new AccessoryInfoDto(customerDto.SerialId, accessoryInfo))
                        .GroupBy(dto => DateTimeUtil.AsUtc(dto.SampleTime))
                        .Select(group => group.First())
                        .OrderBy(dto => dto.SampleTime)];

                    if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                    {
                        RvtLogger.Logger.LogInformation(
                        "StoreAccessoryInfo page={PageNumber} number of dtos to insert={Count} serialId={SerialId} cursor={Cursor}",
                        pageNumber + 1,
                        dtos.Count,
                        customerDto.SerialId,
                        cursor);
                    }

                    if (dtos.Count > 0)
                    {
                        await _accessoryCommands.InsertAccessoryPageAsync(dtos, cancellationToken);
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
