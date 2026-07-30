// File summary: Builds unattached-monitor removal impact counts for read endpoints and transactional commands.
// Major updates:
// - 2026-07-30 pending Added the page-batched impact query so the unattached list issues four queries per page instead of four per row.
// - 2026-06-25 pending Extracted monitor removal impact calculation from the controller for CQRS command reuse.
// - 2026-06-25 pending Routed measurement impact counts through a provider view to avoid 14 sequential count round trips.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Application.Monitors;

// Function summary: Identifies one monitor whose removal impact is requested in a batched page query.
public sealed record MonitorRemovalImpactKey(Guid MonitorId, string SerialId);

public interface IMonitorRemovalImpactReader
{
    // Function summary: Counts domain and measurement rows that decide whether monitor removal archives or deletes.
    Task<MonitorRemovalImpactResponse> BuildAsync(Guid monitorId, string serialId, CancellationToken cancellationToken);

    // Function summary: Builds removal impacts for a whole page with one grouped query per count source.
    Task<IReadOnlyDictionary<Guid, MonitorRemovalImpactResponse>> BuildForPageAsync(
        IReadOnlyList<MonitorRemovalImpactKey> monitors,
        CancellationToken cancellationToken);
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
        int deploymentCount = await domainContext.Deployments.CountAsync(item => item.MonitorId == monitorId, cancellationToken);
        int notificationCount = await domainContext.Notifications.CountAsync(item => item.MonitorId == monitorId, cancellationToken);
        int alertRuleCount = await domainContext.RvtAlertRules.CountAsync(item => item.MonitorId == monitorId, cancellationToken);
        (int tableCount, int rowCount) = await CountMeasurementRowsAsync(serialId, cancellationToken);

        return new MonitorRemovalImpactResponse
        {
            DeploymentCount = deploymentCount,
            NotificationCount = notificationCount,
            AlertRuleCount = alertRuleCount,
            MeasurementTableCount = tableCount,
            MeasurementRowCount = rowCount
        };
    }

    // Function summary: Builds removal impacts for a whole page with one grouped query per count source.
    public async Task<IReadOnlyDictionary<Guid, MonitorRemovalImpactResponse>> BuildForPageAsync(
        IReadOnlyList<MonitorRemovalImpactKey> monitors,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, MonitorRemovalImpactResponse> results = new(monitors.Count);
        if (monitors.Count == 0)
        {
            return results;
        }

        List<Guid> monitorIds = [.. monitors.Select(monitor => monitor.MonitorId).Distinct()];
        Dictionary<Guid, int> deploymentCounts = await CountByMonitorAsync(
            domainContext.Deployments.Where(item => monitorIds.Contains(item.MonitorId)).Select(item => item.MonitorId),
            cancellationToken);
        Dictionary<Guid, int> notificationCounts = await CountByMonitorAsync(
            domainContext.Notifications.Where(item => monitorIds.Contains(item.MonitorId)).Select(item => item.MonitorId),
            cancellationToken);
        Dictionary<Guid, int> alertRuleCounts = await CountByMonitorAsync(
            domainContext.RvtAlertRules.Where(item => monitorIds.Contains(item.MonitorId)).Select(item => item.MonitorId),
            cancellationToken);
        Dictionary<string, (int TableCount, int RowCount)> measurementCounts =
            await CountMeasurementRowsForPageAsync(monitors, cancellationToken);

        foreach (MonitorRemovalImpactKey monitor in monitors)
        {
            (int tableCount, int rowCount) = measurementCounts.GetValueOrDefault(monitor.SerialId, (0, 0));
            results[monitor.MonitorId] = new MonitorRemovalImpactResponse
            {
                DeploymentCount = deploymentCounts.GetValueOrDefault(monitor.MonitorId),
                NotificationCount = notificationCounts.GetValueOrDefault(monitor.MonitorId),
                AlertRuleCount = alertRuleCounts.GetValueOrDefault(monitor.MonitorId),
                MeasurementTableCount = tableCount,
                MeasurementRowCount = rowCount
            };
        }

        return results;
    }

    // Function summary: Groups a filtered monitor-id projection into per-monitor counts with one query.
    private static async Task<Dictionary<Guid, int>> CountByMonitorAsync(
        IQueryable<Guid> monitorIds,
        CancellationToken cancellationToken)
    {
        return await monitorIds
            .GroupBy(monitorId => monitorId)
            .Select(group => new { MonitorId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.MonitorId, row => row.Count, cancellationToken);
    }

    // Function summary: Counts measurement rows for a page of serial ids with one IN query against the impact view.
    private async Task<Dictionary<string, (int TableCount, int RowCount)>> CountMeasurementRowsForPageAsync(
        IReadOnlyList<MonitorRemovalImpactKey> monitors,
        CancellationToken cancellationToken)
    {
        List<string> serialIds = [.. monitors
            .Select(monitor => monitor.SerialId)
            .Where(serialId => !string.IsNullOrWhiteSpace(serialId))
            .Distinct()];
        Dictionary<string, (int TableCount, int RowCount)> counts = new(serialIds.Count);
        if (serialIds.Count == 0)
        {
            return counts;
        }

        var impacts = await searchContext.MonitorMeasurementRemovalImpacts
            .AsNoTracking()
            .Where(item => serialIds.Contains(item.SerialId))
            .Select(item => new
            {
                item.SerialId,
                item.MeasurementTableCount,
                item.MeasurementRowCount
            })
            .ToListAsync(cancellationToken);
        foreach (var impact in impacts)
        {
            counts[impact.SerialId] = (ClampCount(impact.MeasurementTableCount), ClampCount(impact.MeasurementRowCount));
        }

        return counts;
    }

    // Function summary: Counts known serial-id keyed measurement rows for an unattached monitor.
    private async Task<(int TableCount, int RowCount)> CountMeasurementRowsAsync(string serialId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serialId))
        {
            return (0, 0);
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

    // Function summary: Converts SQL bigint counts to API DTO integer counts without overflowing.
    private static int ClampCount(long count)
    {
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }
}
