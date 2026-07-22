using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyAtm.Api.Db;
using MyAtm.Model.Json;
using MyAtm.Model.Json.DeviceInfo;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

namespace MyAtm.Api.Http
{
    // Summary: Vendor HTTP gateway for the MyAtmosphere API - request building, calls, and response parsing.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the MyAtmApi partials (MyAtmApiMonitors, MyAtmApiDustLevels, MyAtmApiAccessoryInfo).
    public class MyAtmHttpGateway
    {
        private readonly IHttpClient httpClient;
        private readonly int devicePageSize;
        private readonly int measurementPageSize;
        private readonly int accessoryPageSize;

        public MyAtmHttpGateway(
            IHttpClient httpClient,
            int devicePageSize,
            int measurementPageSize = 1000,
            int accessoryPageSize = 1000)
        {
            this.httpClient = httpClient;
            this.devicePageSize = devicePageSize;
            this.measurementPageSize = measurementPageSize;
            this.accessoryPageSize = accessoryPageSize;
        }

        public async Task<List<Model.Json.Customer.DustMonitor>> HttpGetMonitorsAsync(
            int customerId,
            int skip,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var json = await DoListMonitorsAsync(customerId, skip, cancellationToken);
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
                var json = await DoGetDeviceInfoAsync(customerId, serialNumber, cancellationToken);
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
            var page = await HttpGetDeviceMeasurementPageAsync<T>(
                customerId,
                serialNumber,
                lastDataTime ?? MyAtmApi.JAN1_1970,
                period,
                cancellationToken);
            return page.Measurements.ToList();
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
                var normalizedCursor = DateTimeUtil.AsUtc(cursor);
                json = await DoGetDeviceMeasurementsAsync(
                    customerId,
                    serialNumber,
                    period,
                    normalizedCursor,
                    measurementPageSize,
                    cancellationToken);
                var rawMeasurements = JsonSerializer.Deserialize<List<T>>(json)
                    ?? throw AdapterException.Of("HttpGetDeviceMeasurements returned null JSON array.");
                var measurements = rawMeasurements
                    .Select(measurement =>
                    {
                        measurement.Timestamp = DateTimeUtil.AsUtc(measurement.Timestamp);
                        return measurement;
                    })
                    .Where(measurement => measurement.Timestamp > normalizedCursor)
                    .GroupBy(measurement => measurement.Timestamp)
                    .Select(group => group.First())
                    .OrderBy(measurement => measurement.Timestamp)
                    .ToList();
                DateTime? nextCursor = measurements.Count == 0 ? null : measurements[^1].Timestamp;
                return new MyAtmMeasurementPage<T>(measurements, nextCursor, rawMeasurements.Count >= measurementPageSize);
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
                var page = await HttpGetAccessoryInfoPageAsync(
                    customerId,
                    serialNumber,
                    lastDataTime ?? MyAtmApi.JAN1_1970,
                    cancellationToken);
                return page.Measurements.ToList();
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
                var normalizedCursor = DateTimeUtil.AsUtc(cursor);
                var json = await DoGetDeviceAccessoryInfoAsync(customerId, serialNumber, normalizedCursor, accessoryPageSize, cancellationToken);
                var rawAccessoryInfo = JsonSerializer.Deserialize<List<AccessoryInfo>>(json)
                    ?? throw AdapterException.Of("HttpGetAccessoryInfos returned null JSON array.");
                var accessoryInfo = rawAccessoryInfo
                    .Select(info =>
                    {
                        info.Timestamp = DateTimeUtil.AsUtc(info.Timestamp);
                        return info;
                    })
                    .Where(info => info.Timestamp > normalizedCursor)
                    .GroupBy(info => info.Timestamp)
                    .Select(group => group.First())
                    .OrderBy(info => info.Timestamp)
                    .ToList();
                DateTime? nextCursor = accessoryInfo.Count == 0 ? null : accessoryInfo[^1].Timestamp;
                return new MyAtmMeasurementPage<AccessoryInfo>(accessoryInfo, nextCursor, rawAccessoryInfo.Count >= accessoryPageSize);
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
            return await httpClient.GetAsync(string.Format("/api/customers/{0}/devices?$skip={1}&$top={2}", customerId, skip, devicePageSize), cancellationToken);
        }

        private async Task<string> DoGetDeviceInfoAsync(int customerId, string serialId, CancellationToken cancellationToken)
        {
            return await httpClient.GetAsync(string.Format("/api/customers/{0}/devices/{1}", customerId, serialId), cancellationToken);
        }

        private async Task<string> DoGetDeviceMeasurementsAsync(
            int customerId,
            string serialId,
            Period period,
            DateTime cursor,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var basePath = string.Format("/api/customers/{0}/devices/{1}/measurements", customerId, serialId);
            string path;
            var paging = string.Format(
                CultureInfo.InvariantCulture,
                "$filter=timestamp gt {0}&$orderby=timestamp asc&$top={1}",
                DateTimeUtil.AsUtc(cursor).ToString("O", CultureInfo.InvariantCulture),
                pageSize);

            // todo for avg values ?$select=avrg,timestamp&expand=pm1($select=avg)
            switch (period)
            {
                case Period.Minutes1:
                    path = string.Format("{0}?$select=avrg,timestamp,pm1,pm2_5,pm10,pm_total,weather_t,weather_p,weather_rh&{1}", basePath, paging);
                    break;
                case Period.Minutes15:
                    path = string.Format("{0}/15min?{1}", basePath, paging);
                    break;
                case Period.Hours1:
                    path = string.Format("{0}/hourly?{1}", basePath, paging);
                    break;
                case Period.Hours24:
                    path = string.Format("{0}/daily?{1}", basePath, paging);
                    break;
                default:
                    throw AdapterException.Of("DoGetDeviceMeasurementsAsync Unknown Period " + period);
            }
            return await httpClient.GetAsync(path, cancellationToken);
        }

        private async Task<string> DoGetDeviceAccessoryInfoAsync(
            int customerId,
            string serialNumber,
            DateTime cursor,
            int pageSize,
            CancellationToken cancellationToken)
        {
            return await httpClient.GetAsync(
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

public sealed record MyAtmMeasurementPage<T>(
    IReadOnlyList<T> Measurements,
    DateTime? NextCursor,
    bool HasMore);
