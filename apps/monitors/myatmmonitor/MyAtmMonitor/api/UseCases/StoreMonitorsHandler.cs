using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Dto;
using MyAtm.Model.Json.Customer;

namespace MyAtm.Api.UseCases;

// Imports the bounded MyAtmosphere customer device catalogue into the monitor list.
public sealed class StoreMonitorsHandler
{
    private readonly MyAtmHttpGateway gateway;
    private readonly IMyAtmMonitorCommands monitorCommands;
    private readonly IMyAtmOperationalCommands operationalCommands;
    private readonly bool testLocal;
    private readonly int devicePageSize;
    private readonly int maxDevicePagesPerRun;

    public StoreMonitorsHandler(
        MyAtmHttpGateway gateway,
        IMyAtmMonitorCommands monitorCommands,
        IMyAtmOperationalCommands operationalCommands,
        bool testLocal,
        int devicePageSize,
        int maxDevicePagesPerRun)
    {
        this.gateway = gateway;
        this.monitorCommands = monitorCommands;
        this.operationalCommands = operationalCommands;
        this.testLocal = testLocal;
        this.devicePageSize = devicePageSize;
        this.maxDevicePagesPerRun = maxDevicePagesPerRun;
    }

    public async Task RunAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var failures = new MyAtmFailureCollector(operationalCommands);
        var fullPageFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var skip = 0;

        for (var pageNumber = 1; pageNumber <= maxDevicePagesPerRun; pageNumber++)
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

            var isFullPage = devices.Count >= devicePageSize;
            if (isFullPage && !fullPageFingerprints.Add(Fingerprint(devices)))
            {
                failures.Capture(
                    $"StoreMonitors page={pageNumber}",
                    new InvalidOperationException("MyAtmosphere returned a repeated full catalogue page."),
                    cancellationToken);
                break;
            }

            var dtos = new List<DustMonitorDto>(devices.Count);
            foreach (var device in devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var serialId = device.SerialNumber;
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
                    var deviceInfo = await gateway.HttpGetDeviceInfoAsync(
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

            var filteredDtos = MyAtmTestLocalMonitorFilter.ApplyCatalog(dtos, testLocal);
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
