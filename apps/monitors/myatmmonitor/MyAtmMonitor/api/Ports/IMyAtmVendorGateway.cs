// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using MyAtm.Model;
using MyAtm.Model.Json;
using MyAtm.Model.Json.DeviceInfo;

namespace MyAtm.Api.Ports;

/// <summary>
/// Driven port for the MyAtmosphere vendor API.
/// </summary>
/// <remarks>
/// The use cases depend on this abstraction rather than on the concrete
/// <c>MyAtmHttpGateway</c> adapter, matching the AirQ and Omnidots ports, so
/// the import logic can be exercised and substituted without the vendor
/// transport. Every call is asynchronous and cancellable.
/// </remarks>
public interface IMyAtmVendorGateway
{
    Task<List<Model.Json.Customer.DustMonitor>> HttpGetMonitorsAsync(
        int customerId,
        int skip,
        CancellationToken cancellationToken = default);

    Task<DustMonitorInfo> HttpGetDeviceInfoAsync(
        int customerId,
        string serialNumber,
        CancellationToken cancellationToken = default);

    Task<List<T>> HttpGetDeviceMeasurementsAsync<T>(
        int customerId,
        string serialNumber,
        DateTime? lastDataTime,
        Period period,
        CancellationToken cancellationToken = default)
        where T : BaseDeviceMeasurement;

    Task<MyAtmMeasurementPage<T>> HttpGetDeviceMeasurementPageAsync<T>(
        int customerId,
        string serialNumber,
        DateTime cursor,
        Period period,
        CancellationToken cancellationToken = default)
        where T : BaseDeviceMeasurement;

    Task<List<AccessoryInfo>> HttpGetAccessoryInfosAsync(
        int customerId,
        string serialNumber,
        DateTime? lastDataTime,
        CancellationToken cancellationToken = default);

    Task<MyAtmMeasurementPage<AccessoryInfo>> HttpGetAccessoryInfoPageAsync(
        int customerId,
        string serialNumber,
        DateTime cursor,
        CancellationToken cancellationToken = default);
}
