// File summary: Builds shared monitor-detail summary metrics for admin, company, and installer workflows.
// Major updates:
// - 2026-06-29 pending Added invariant date parsing for monitor status timestamps.
// - 2026-06-26 pending Scoped latest reading/average data-source requests to effective ownership windows.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-09 pending Centralized legacy monitor metric semantics across monitor detail endpoints.
// - 2026-06-09 pending Shared latest metric query flow to reduce Sonar duplication.
// - 2026-06-10 pending Removed redundant async/await from metric summary pass-through helpers.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RVT.Entities.Querying;
using RvtPortal.Spa.Application.Monitors;
using System.Globalization;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Api;

public interface IMonitorDetailSummaryService
{
    // Function summary: Builds the latest reading summary, preferring live measurements and falling back to notification data.
    Task<MonitorMetricSummary?> BuildLatestReadingAsync(Deployment? deployment, Notification? fallbackNotification);

    // Function summary: Builds the latest average summary from live measurement data where supported.
    Task<MonitorMetricSummary?> BuildLatestAverageAsync(Deployment? deployment);

    // Function summary: Builds the latest vendor battery summary where supported.
    Task<MonitorMetricSummary?> BuildLatestBatteryAsync(MonitorEntity monitor);

    // Function summary: Builds deployment summary data for legacy monitor-detail parity.
    MonitorDeploymentSummary? BuildDeploymentSummary(Deployment? deployment);
}

public sealed class MonitorDetailSummaryService : IMonitorDetailSummaryService
{
    private readonly RVTSearchContext searchContext;
    private readonly IMonitorDataSource dataSource;

    // Function summary: Initializes this type with search and measurement data sources.
    public MonitorDetailSummaryService(RVTSearchContext searchContext, IMonitorDataSource dataSource)
    {
        this.searchContext = searchContext;
        this.dataSource = dataSource;
    }

    public async Task<MonitorMetricSummary?> BuildLatestReadingAsync(Deployment? deployment, Notification? fallbackNotification)
    {
        return await BuildLatestMeasurementAsync(deployment) ??
            BuildLatestReadingFromNotification(fallbackNotification, deployment?.Monitor.TypeOfMonitor ?? MonitorTypeEnum.Dust);
    }

    public Task<MonitorMetricSummary?> BuildLatestAverageAsync(Deployment? deployment)
    {
        return BuildLatestDeploymentMetricAsync(deployment, LatestAverageFilter, averageMetric: true);
    }

    public async Task<MonitorMetricSummary?> BuildLatestBatteryAsync(MonitorEntity monitor)
    {
        var omnidots = await searchContext.OmnidotsSensors
            .AsNoTracking()
            .Where(sensor => sensor.SerialId == monitor.SerialId)
            .OrderByDescending(sensor => sensor.Lastseen)
            .FirstOrDefaultAsync();
        if (omnidots != null)
        {
            return BuildMetric("Battery Charge", "batteryCharge", omnidots.BatteryCharge, "%", omnidots.Lastseen, "Omnidots sensor status");
        }

        var svantek = await searchContext.SvantekMonitorStatuses
            .AsNoTracking()
            .Where(status => status.SerialId == monitor.SerialId && status.Batterycharge.HasValue)
            .FirstOrDefaultAsync();
        return svantek?.Batterycharge == null
            ? null
            : BuildMetric("Battery Charge", "batteryCharge", svantek.Batterycharge, "%", ParseStatusTime(svantek.Laststatustimestamp), "Svantek monitor status");
    }

    public MonitorDeploymentSummary? BuildDeploymentSummary(Deployment? deployment)
    {
        if (deployment == null)
        {
            return null;
        }

        return new MonitorDeploymentSummary
        {
            DeploymentId = deployment.Id,
            ContractNumber = deployment.Contract?.ContractNumber,
            SiteName = deployment.Contract?.Site?.SiteName,
            CompanyName = deployment.Contract?.Company?.CompanyName,
            OnHireDate = deployment.StartDate,
            OffHireDate = deployment.EndDate,
            AddedDate = deployment.StartDate
        };
    }

    private Task<MonitorMetricSummary?> BuildLatestMeasurementAsync(Deployment? deployment)
    {
        return BuildLatestDeploymentMetricAsync(deployment, LatestReadingFilter, averageMetric: false);
    }

    // Function summary: Builds the latest metric from deployment data using the requested filter semantics.
    private async Task<MonitorMetricSummary?> BuildLatestDeploymentMetricAsync(
        Deployment? deployment,
        Func<MonitorTypeEnum, string> filter,
        bool averageMetric)
    {
        if (deployment?.Monitor == null)
        {
            return null;
        }

        try
        {
            var monitorType = deployment.Monitor.TypeOfMonitor;
            var ownershipWindow = MonitorOwnershipWindowResolver.ForDeployment(deployment);
            var data = await dataSource.GetDeploymentDataAsync(new DeploymentDataQuery(
                DeploymentId: deployment.Id,
                TraceId: null,
                FilterOption: filter(monitorType),
                FromDate: ownershipWindow.Start,
                ToDate: ownershipWindow.End ?? DateTime.UtcNow.AddDays(1),
                GraphData: false,
                Page: 1,
                PageSize: 1,
                Sort: "SampleTime",
                SortDir: OrderByDirectionEnum.Descending));

            if (data.DustLevels?.Value.FirstOrDefault() is { } dust)
            {
                return BuildMetric(
                    averageMetric ? "Latest 15 Min Average" : "Latest Reading",
                    "pm10",
                    dust.Pm10,
                    "ug/m3",
                    dust.SampleTime,
                    averageMetric ? "Dust PM10 15 minute average" : "Dust PM10 live reading");
            }

            if (data.NoiseLevels?.Value.FirstOrDefault() is { } noise)
            {
                return BuildMetric(
                    averageMetric ? "Latest 15 Min Average" : "Latest Reading",
                    "LAeq",
                    noise.Laeq,
                    "dB",
                    noise.SampleTime,
                    averageMetric ? "Noise LAeq 15 minute average" : "Noise LAeq live reading");
            }

            if (data.VibrationLevels?.Value.FirstOrDefault() is { } vibration)
            {
                var (field, value) = MaxAxis(vibration.Xvtop, vibration.Yvtop, vibration.Zvtop);
                return BuildMetric(
                    averageMetric ? "Latest Peak" : "Latest Reading",
                    field,
                    value,
                    "mm/s",
                    vibration.SampleTime,
                    "Vibration highest peak axis");
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static MonitorMetricSummary? BuildLatestReadingFromNotification(Notification? notification, MonitorTypeEnum monitorType)
    {
        if (notification == null)
        {
            return null;
        }

        return new MonitorMetricSummary
        {
            Label = notification.AlertType == AlertTypeEnum.Alert ? "Latest Breach" : "Latest Reading",
            Field = notification.AlertField,
            Value = notification.Level,
            Unit = UnitForMonitorType(monitorType),
            SampleTime = notification.NotificationTime,
            Detail = $"{notification.AlertType} notification fallback"
        };
    }

    private static string LatestAverageFilter(MonitorTypeEnum monitorType)
    {
        return monitorType switch
        {
            MonitorTypeEnum.Dust => "900",
            MonitorTypeEnum.Noise => "900",
            MonitorTypeEnum.Vibration => "time",
            _ => ""
        };
    }

    private static string LatestReadingFilter(MonitorTypeEnum monitorType)
    {
        return monitorType switch
        {
            MonitorTypeEnum.Dust => "60",
            MonitorTypeEnum.Noise => "900",
            MonitorTypeEnum.Vibration => "time",
            _ => ""
        };
    }

    private static MonitorMetricSummary BuildMetric(string label, string field, double? value, string unit, DateTime? sampleTime, string? detail)
    {
        return new MonitorMetricSummary
        {
            Label = label,
            Field = field,
            Value = value,
            Unit = unit,
            SampleTime = sampleTime,
            Detail = detail
        };
    }

    private static (string Field, double? Value) MaxAxis(double? x, double? y, double? z)
    {
        return new[] { ("Xvtop", x), ("Yvtop", y), ("Zvtop", z) }
            .Where(axis => axis.Item2.HasValue)
            .OrderByDescending(axis => axis.Item2)
            .FirstOrDefault();
    }

    private static DateTime? ParseStatusTime(string? value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
    }

    private static string UnitForMonitorType(MonitorTypeEnum monitorType)
    {
        return monitorType switch
        {
            MonitorTypeEnum.Dust => "ug/m3",
            MonitorTypeEnum.Noise => "dB",
            MonitorTypeEnum.Vibration => "mm/s",
            _ => ""
        };
    }
}
