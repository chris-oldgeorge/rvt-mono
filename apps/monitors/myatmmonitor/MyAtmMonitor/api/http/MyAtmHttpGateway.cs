using System.Globalization;
using System.Text.Json;
using MyAtm.Api.Ports;
using MyAtm.Model;
using MyAtm.Model.Json;
using MyAtm.Model.Json.DeviceInfo;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

namespace MyAtm.Api.Http
{
    // Summary: Vendor HTTP gateway for the MyAtmosphere API - request building, calls, and response parsing.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the MyAtmApi partials (MyAtmApiMonitors, MyAtmApiDustLevels, MyAtmApiAccessoryInfo).
    //
    // Vendor context: the "RVT Case Study" AQ Network currently has no assigned devices, so the
    // per-customer measurements endpoint returns nothing for it. Device 18129 was once assigned
    // there, was moved to another AQ Network, and its data is still reachable via the per-device
    // endpoint (GET /api/customers/146/devices/18129/measurements). 18129 was added to the DB manually.
    public class MyAtmHttpGateway : IMyAtmVendorGateway
    {
        private readonly IHttpClient _httpClient;
        private readonly int _devicePageSize;
        private readonly int _measurementPageSize;
        private readonly int _accessoryPageSize;

        public MyAtmHttpGateway(
            IHttpClient httpClient,
            int devicePageSize,
            int measurementPageSize = 1000,
            int accessoryPageSize = 1000)
        {
            _httpClient = httpClient;
            _devicePageSize = devicePageSize;
            _measurementPageSize = measurementPageSize;
            _accessoryPageSize = accessoryPageSize;
        }

        public async Task<List<Model.Json.Customer.DustMonitor>> HttpGetMonitorsAsync(
            int customerId,
            int skip,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string json = await DoListMonitorsAsync(customerId, skip, cancellationToken);
                return JsonSerializer.Deserialize<List<Model.Json.Customer.DustMonitor>>(json)!;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("HttpGetMonitors", e);
            }
        }

        public async Task<DustMonitorInfo> HttpGetDeviceInfoAsync(
            int customerId,
            string serialNumber,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string json = await DoGetDeviceInfoAsync(customerId, serialNumber, cancellationToken);
                return JsonSerializer.Deserialize<Model.Json.DeviceInfo.DustMonitorInfo>(json)!;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("HttpGetCustomerDevices", e);
            }
        }

        public async Task<List<T>> HttpGetDeviceMeasurementsAsync<T>(
            int customerId,
            string serialNumber,
            DateTime? lastDataTime,
            Period period,
            CancellationToken cancellationToken = default) where T : BaseDeviceMeasurement
        {
            MyAtmMeasurementPage<T> page = await HttpGetDeviceMeasurementPageAsync<T>(
                customerId,
                serialNumber,
                lastDataTime ?? DateTimeUtil.JAN1_1970,
                period,
                cancellationToken);
            return [.. page.Measurements];
        }

        public async Task<MyAtmMeasurementPage<T>> HttpGetDeviceMeasurementPageAsync<T>(
            int customerId,
            string serialNumber,
            DateTime cursor,
            Period period,
            CancellationToken cancellationToken = default) where T : BaseDeviceMeasurement
        {
            string json = string.Empty;
            try
            {
                DateTime normalizedCursor = DateTimeUtil.AsUtc(cursor);
                json = await DoGetDeviceMeasurementsAsync(
                    customerId,
                    serialNumber,
                    period,
                    normalizedCursor,
                    _measurementPageSize,
                    cancellationToken);
                List<T> rawMeasurements = JsonSerializer.Deserialize<List<T>>(json)
                    ?? throw AdapterException.Of("HttpGetDeviceMeasurements returned null JSON array.");
                List<T> measurements = [.. rawMeasurements
                    .Select(measurement =>
                    {
                        measurement.Timestamp = DateTimeUtil.AsUtc(measurement.Timestamp);
                        return measurement;
                    })
                    .Where(measurement => measurement.Timestamp > normalizedCursor)
                    .GroupBy(measurement => measurement.Timestamp)
                    .Select(group => group.First())
                    .OrderBy(measurement => measurement.Timestamp)];
                DateTime? nextCursor = measurements.Count == 0 ? null : measurements[^1].Timestamp;
                return new MyAtmMeasurementPage<T>(measurements, nextCursor, rawMeasurements.Count >= _measurementPageSize);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("HttpGetDeviceMeasurements", e);
            }
        }

        public async Task<List<AccessoryInfo>> HttpGetAccessoryInfosAsync(
            int customerId,
            string serialNumber,
            DateTime? lastDataTime,
            CancellationToken cancellationToken = default)
        {
            try
            {
                MyAtmMeasurementPage<AccessoryInfo> page = await HttpGetAccessoryInfoPageAsync(
                    customerId,
                    serialNumber,
                    lastDataTime ?? DateTimeUtil.JAN1_1970,
                    cancellationToken);
                return [.. page.Measurements];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("HttpGetAccessoryInfos", e);
            }
        }

        public async Task<MyAtmMeasurementPage<AccessoryInfo>> HttpGetAccessoryInfoPageAsync(
            int customerId,
            string serialNumber,
            DateTime cursor,
            CancellationToken cancellationToken = default)
        {
            try
            {
                DateTime normalizedCursor = DateTimeUtil.AsUtc(cursor);
                string json = await DoGetDeviceAccessoryInfoAsync(customerId, serialNumber, normalizedCursor, _accessoryPageSize, cancellationToken);
                List<AccessoryInfo> rawAccessoryInfo = JsonSerializer.Deserialize<List<AccessoryInfo>>(json)
                    ?? throw AdapterException.Of("HttpGetAccessoryInfos returned null JSON array.");
                List<AccessoryInfo> accessoryInfo = [.. rawAccessoryInfo
                    .Select(info =>
                    {
                        info.Timestamp = DateTimeUtil.AsUtc(info.Timestamp);
                        return info;
                    })
                    .Where(info => info.Timestamp > normalizedCursor)
                    .GroupBy(info => info.Timestamp)
                    .Select(group => group.First())
                    .OrderBy(info => info.Timestamp)];
                DateTime? nextCursor = accessoryInfo.Count == 0 ? null : accessoryInfo[^1].Timestamp;
                return new MyAtmMeasurementPage<AccessoryInfo>(accessoryInfo, nextCursor, rawAccessoryInfo.Count >= _accessoryPageSize);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("HttpGetAccessoryInfos", e);
            }
        }

        #region ApiCalls

        private async Task<string> DoListMonitorsAsync(int customerId, int skip, CancellationToken cancellationToken)
        {
            return await _httpClient.GetAsync(string.Format("/api/customers/{0}/devices?$skip={1}&$top={2}", customerId, skip, _devicePageSize), cancellationToken);
        }

        private async Task<string> DoGetDeviceInfoAsync(int customerId, string serialId, CancellationToken cancellationToken)
        {
            return await _httpClient.GetAsync(string.Format("/api/customers/{0}/devices/{1}", customerId, serialId), cancellationToken);
        }

        private async Task<string> DoGetDeviceMeasurementsAsync(
            int customerId,
            string serialId,
            Period period,
            DateTime cursor,
            int pageSize,
            CancellationToken cancellationToken)
        {
            string basePath = string.Format("/api/customers/{0}/devices/{1}/measurements", customerId, serialId);
            string paging = string.Format(
                CultureInfo.InvariantCulture,
                "$filter=timestamp gt {0}&$orderby=timestamp asc&$top={1}",
                DateTimeUtil.AsUtc(cursor).ToString("O", CultureInfo.InvariantCulture),
                pageSize);

            // todo for avg values ?$select=avrg,timestamp&expand=pm1($select=avg)
            string path = period switch
            {
                Period.Minutes1 => string.Format("{0}?$select=avrg,timestamp,pm1,pm2_5,pm10,pm_total,weather_t,weather_p,weather_rh&{1}", basePath, paging),
                Period.Minutes15 => string.Format("{0}/15min?{1}", basePath, paging),
                Period.Hours1 => string.Format("{0}/hourly?{1}", basePath, paging),
                Period.Hours24 => string.Format("{0}/daily?{1}", basePath, paging),
                _ => throw AdapterException.Of("DoGetDeviceMeasurementsAsync Unknown Period " + period),
            };
            return await _httpClient.GetAsync(path, cancellationToken);
        }

        private async Task<string> DoGetDeviceAccessoryInfoAsync(
            int customerId,
            string serialNumber,
            DateTime cursor,
            int pageSize,
            CancellationToken cancellationToken)
        {
            return await _httpClient.GetAsync(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "/api/customers/{0}/devices/{1}/measurements/accessory?$filter=timestamp gt {2}&$orderby=timestamp asc&$top={3}",
                    customerId,
                    serialNumber,
                    DateTimeUtil.AsUtc(cursor).ToString("O", CultureInfo.InvariantCulture),
                    pageSize),
                cancellationToken);
        }

        #endregion // ApiCalls
    }
}
