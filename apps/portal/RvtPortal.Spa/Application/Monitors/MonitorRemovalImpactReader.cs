// File summary: Builds unattached-monitor removal impact counts for read endpoints and transactional commands.
// Major updates:
// - 2026-06-25 pending Extracted monitor removal impact calculation from the controller for CQRS command reuse.
// - 2026-06-25 pending Routed measurement impact counts through a provider view to avoid 14 sequential count round trips.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Application.Monitors;

public interface IMonitorRemovalImpactReader
{
    // Function summary: Counts domain and measurement rows that decide whether monitor removal archives or deletes.
    Task<MonitorRemovalImpactResponse> BuildAsync(Guid monitorId, string serialId, CancellationToken cancellationToken);
}

public sealed class MonitorRemovalImpactReader : IMonitorRemovalImpactReader
{
    private readonly RVTDbContext domainContext;
    private readonly RVTSearchContext searchContext;

    // Function summary: Initializes the removal impact reader with domain and search contexts.
    public MonitorRemovalImpactReader(RVTDbContext domainContext, RVTSearchContext searchContext)
    {
        this.domainContext = domainContext;
        this.searchContext = searchContext;
    }

    // Function summary: Counts monitor-related data that determines delete versus archive behavior.
    public async Task<MonitorRemovalImpactResponse> BuildAsync(Guid monitorId, string serialId, CancellationToken cancellationToken)
    {
        var deploymentCount = await domainContext.Deployments.CountAsync(item => item.MonitorId == monitorId, cancellationToken);
        var notificationCount = await domainContext.Notifications.CountAsync(item => item.MonitorId == monitorId, cancellationToken);
        var alertRuleCount = await domainContext.RvtAlertRules.CountAsync(item => item.MonitorId == monitorId, cancellationToken);
        var measurementCounts = await CountMeasurementRowsAsync(serialId, cancellationToken);

        return new MonitorRemovalImpactResponse
        {
            DeploymentCount = deploymentCount,
            NotificationCount = notificationCount,
            AlertRuleCount = alertRuleCount,
            MeasurementTableCount = measurementCounts.TableCount,
            MeasurementRowCount = measurementCounts.RowCount
        };
    }

    // Function summary: Counts known serial-id keyed measurement rows for an unattached monitor.
    private async Task<(int TableCount, int RowCount)> CountMeasurementRowsAsync(string serialId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serialId))
        {
            return (0, 0);
        }

        if (IsInMemoryProvider())
        {
            return await CountMeasurementRowsFromEntitySetsAsync(serialId, cancellationToken);
        }

        var impact = await searchContext.MonitorMeasurementRemovalImpacts
            .AsNoTracking()
            .Where(item => item.SerialId == serialId)
            .Select(item => new
            {
                item.MeasurementTableCount,
                item.MeasurementRowCount
            })
            .SingleOrDefaultAsync(cancellationToken);

        return impact is null
            ? (0, 0)
            : (ClampCount(impact.MeasurementTableCount), ClampCount(impact.MeasurementRowCount));
    }

    // Function summary: Counts measurement rows through entity sets when the EF test provider cannot query a physical view.
    private async Task<(int TableCount, int RowCount)> CountMeasurementRowsFromEntitySetsAsync(
        string serialId,
        CancellationToken cancellationToken)
    {
        var tableCount = 0;
        var rowCount = 0;

        async Task AddCountAsync(Task<int> countTask)
        {
            var count = await countTask;
            if (count > 0)
            {
                tableCount++;
                rowCount = checked(rowCount + count);
            }
        }

        await AddCountAsync(searchContext.MyAtmDustLevels.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.MyAtmDustLevel8hourAvgs.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.NoiseLevel15minAvgs.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.NoiseLevel1hourAvgs.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.NoiseLevel1dayAvgs.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.OmnidotsPeakLevels.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.OmnidotsPeakLevel1mins.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.OmnidotsPeakLevel15mins.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.OmnidotsPeakLevel20mins.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.OmnidotsPeakLevel5mins.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.OmnidotsPeakLevel1dayPeaks.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.OmnidotsTracesIndices.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.OmnidotsMonitorStatuses.CountAsync(item => item.SerialId == serialId, cancellationToken));
        await AddCountAsync(searchContext.SvantekMonitorStatuses.CountAsync(item => item.SerialId == serialId, cancellationToken));

        return (tableCount, rowCount);
    }

    // Function summary: Detects the EF InMemory provider that cannot query the physical SQL view.
    private bool IsInMemoryProvider()
    {
        return searchContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;
    }

    // Function summary: Converts SQL bigint counts to API DTO integer counts without overflowing.
    private static int ClampCount(long count)
    {
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }
}
