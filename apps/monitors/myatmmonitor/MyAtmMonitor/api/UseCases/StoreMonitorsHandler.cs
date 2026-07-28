using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Dto;
using MyAtm.Model.Json.Customer;
using MyAtm.Model.Json.DeviceInfo;

namespace MyAtm.Api.UseCases;

// Imports the bounded MyAtmosphere customer device catalogue into the monitor list.
public sealed class StoreMonitorsHandler(
    MyAtmHttpGateway gateway,
    IMyAtmMonitorCommands monitorCommands,
    IMyAtmOperationalCommands operationalCommands,
    bool testLocal,
    int devicePageSize,
    int maxDevicePagesPerRun)
{
    private readonly MyAtmHttpGateway gateway = gateway;
    private readonly IMyAtmMonitorCommands monitorCommands = monitorCommands;
    private readonly IMyAtmOperationalCommands operationalCommands = operationalCommands;
    private readonly bool testLocal = testLocal;
    private readonly int devicePageSize = devicePageSize;
    private readonly int maxDevicePagesPerRun = maxDevicePagesPerRun;

    public async Task RunAsync(int customerId, CancellationToken cancellationToken = default)
    {
        MyAtmFailureCollector failures = new(operationalCommands);
        HashSet<string> fullPageFingerprints = new(StringComparer.Ordinal);
        int skip = 0;

        for (int pageNumber = 1; pageNumber <= maxDevicePagesPerRun; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<DustMonitor> devices;
            try
            {
                devices = await gateway.HttpGetMonitorsAsync(customerId, skip, cancellationToken);
            }
            catch (Exception exception)
            {
                failures.Capture($"StoreMonitors page={pageNumber}", exception, cancellationToken);
                break;
            }

            bool isFullPage = devices.Count >= devicePageSize;
            if (isFullPage && !fullPageFingerprints.Add(Fingerprint(devices)))
            {
                failures.Capture(
                    $"StoreMonitors page={pageNumber}",
                    new InvalidOperationException("MyAtmosphere returned a repeated full catalogue page."),
                    cancellationToken);
                break;
            }

            List<DustMonitorDto> dtos = new(devices.Count);
            foreach (DustMonitor device in devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? serialId = device.SerialNumber;
                if (string.IsNullOrWhiteSpace(serialId))
                {
                    failures.Capture(
                        $"StoreMonitors page={pageNumber} serialId=missing",
                        new InvalidDataException("MyAtmosphere catalogue device has no serial number."),
                        cancellationToken);
                    continue;
                }

                try
                {
                    DustMonitorInfo deviceInfo = await gateway.HttpGetDeviceInfoAsync(
                        customerId,
                        serialId,
                        cancellationToken);
                    dtos.Add(new DustMonitorDto(deviceInfo));
                }
                catch (Exception exception)
                {
                    failures.Capture($"StoreMonitors serialId={serialId}", exception, cancellationToken);
                }
            }

            List<DustMonitorDto> filteredDtos = MyAtmTestLocalMonitorFilter.ApplyCatalog(dtos, testLocal);
            if (filteredDtos.Count > 0)
            {
                try
                {
                    monitorCommands.WriteMonitorList(filteredDtos);
                }
                catch (Exception exception)
                {
                    failures.Capture($"StoreMonitors page={pageNumber} persistence", exception, cancellationToken);
                }
            }

            if (!isFullPage)
            {
                break;
            }

            if (pageNumber == maxDevicePagesPerRun)
            {
                failures.Capture(
                    $"StoreMonitors page={pageNumber}",
                    new InvalidOperationException("MyAtmosphere catalogue page limit was reached before a final partial page."),
                    cancellationToken);
                break;
            }

            try
            {
                skip = checked(skip + devicePageSize);
            }
            catch (OverflowException exception)
            {
                failures.Capture($"StoreMonitors page={pageNumber}", exception, cancellationToken);
                break;
            }
        }

        failures.ThrowIfAny("StoreMonitors");
    }

    private static string Fingerprint(IEnumerable<DustMonitor> devices) =>
        string.Join(
            '\u001f',
            devices
                .Select(device => device.SerialNumber ?? string.Empty)
                .Order(StringComparer.Ordinal));
}
