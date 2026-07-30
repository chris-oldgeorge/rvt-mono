// File summary: Pins the page-batched removal-impact query to the per-row BuildAsync results.
// Major updates:
// - 2026-07-30 pending Added with the batched unattached-monitors impact path (N+1 removal).

using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Monitors;
using RvtPortal.Spa.Tests.Support;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Tests;

public sealed class MonitorRemovalImpactReaderTests
{
    [Fact]
    // Function summary: Verifies the batched page query returns exactly what per-row BuildAsync returns for every monitor.
    public async Task BuildForPageAsync_MatchesPerRowBuildAsync()
    {
        using RVTDbContext domainContext = new(TestDbContexts.InMemory<RVTDbContext>());
        using RVTSearchContext searchContext = new(TestDbContexts.InMemory<RVTSearchContext>());

        MonitorEntity withEverything = TestData.Monitor();
        MonitorEntity withNothing = TestData.Monitor();
        MonitorEntity withMeasurementsOnly = TestData.Monitor();
        Guid contractId = Guid.NewGuid();

        domainContext.Deployments.AddRange(
            TestData.Deployment(contractId, withEverything.Id),
            TestData.Deployment(contractId, withEverything.Id));
        domainContext.Notifications.AddRange(
            TestData.Notification(withEverything.Id),
            TestData.Notification(withEverything.Id),
            TestData.Notification(withEverything.Id));
        domainContext.RvtAlertRules.Add(
            TestData.AlertLevel(withEverything.Id, withEverything.SerialId));
        await domainContext.SaveChangesAsync();

        searchContext.OmnidotsTracesIndices.AddRange(
            new OmnidotsTracesIndex
            {
                Id = Guid.NewGuid(),
                SerialId = withEverything.SerialId,
                StartTime = DateTime.UtcNow.AddHours(-2),
                EndTime = DateTime.UtcNow.AddHours(-1)
            },
            new OmnidotsTracesIndex
            {
                Id = Guid.NewGuid(),
                SerialId = withMeasurementsOnly.SerialId,
                StartTime = DateTime.UtcNow.AddHours(-4),
                EndTime = DateTime.UtcNow.AddHours(-3)
            });
        searchContext.OmnidotsMonitorStatuses.Add(new OmnidotsMonitorStatus
        {
            Id = Guid.NewGuid(),
            SerialId = withMeasurementsOnly.SerialId,
            BuildingLevel = "0"
        });
        await searchContext.SaveChangesAsync();

        MonitorRemovalImpactReader reader = new(domainContext, searchContext);
        List<MonitorRemovalImpactKey> page =
        [
            new(withEverything.Id, withEverything.SerialId),
            new(withNothing.Id, withNothing.SerialId),
            new(withMeasurementsOnly.Id, withMeasurementsOnly.SerialId)
        ];

        IReadOnlyDictionary<Guid, MonitorRemovalImpactResponse> batched =
            await reader.BuildForPageAsync(page, CancellationToken.None);

        Assert.Equal(page.Count, batched.Count);
        foreach (MonitorRemovalImpactKey key in page)
        {
            MonitorRemovalImpactResponse expected = await reader.BuildAsync(key.MonitorId, key.SerialId, CancellationToken.None);
            MonitorRemovalImpactResponse actual = batched[key.MonitorId];

            Assert.Equal(expected.DeploymentCount, actual.DeploymentCount);
            Assert.Equal(expected.NotificationCount, actual.NotificationCount);
            Assert.Equal(expected.AlertRuleCount, actual.AlertRuleCount);
            Assert.Equal(expected.MeasurementTableCount, actual.MeasurementTableCount);
            Assert.Equal(expected.MeasurementRowCount, actual.MeasurementRowCount);
            Assert.Equal(expected.HasRelatedData, actual.HasRelatedData);
        }

        // The monitor with related rows must actually have non-zero counts, or this parity check proves nothing.
        Assert.Equal(2, batched[withEverything.Id].DeploymentCount);
        Assert.Equal(3, batched[withEverything.Id].NotificationCount);
        Assert.Equal(1, batched[withEverything.Id].AlertRuleCount);
        Assert.True(batched[withMeasurementsOnly.Id].MeasurementRowCount > 0);
        Assert.False(batched[withNothing.Id].HasRelatedData);
    }

    [Fact]
    // Function summary: Verifies an empty page short-circuits without touching the database.
    public async Task BuildForPageAsync_WithEmptyPage_ReturnsEmpty()
    {
        using RVTDbContext domainContext = new(TestDbContexts.InMemory<RVTDbContext>());
        using RVTSearchContext searchContext = new(TestDbContexts.InMemory<RVTSearchContext>());
        MonitorRemovalImpactReader reader = new(domainContext, searchContext);

        IReadOnlyDictionary<Guid, MonitorRemovalImpactResponse> batched =
            await reader.BuildForPageAsync([], CancellationToken.None);

        Assert.Empty(batched);
    }
}
