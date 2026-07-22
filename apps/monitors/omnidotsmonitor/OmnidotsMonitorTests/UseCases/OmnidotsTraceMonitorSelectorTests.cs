using Omnidots.Api.UseCases;
using Omnidots.Model.Config;

namespace OmnidotsAdapterTests.UseCases;

[TestClass]
public sealed class OmnidotsTraceMonitorSelectorTests
{
    [TestMethod]
    public void Select_DisabledCollection_ReturnsNoMonitors()
    {
        var selected = OmnidotsTraceMonitorSelector.Select(
            OmnidotsFixture.MonitorsList(2),
            new Dictionary<string, DateTime>(),
            Options(enabled: false, maxMonitorsPerRun: 1),
            rotationSlot: 0);

        Assert.AreEqual(0, selected.Count);
    }

    [TestMethod]
    public void Select_AllowListFiltersFleet()
    {
        var selected = OmnidotsTraceMonitorSelector.Select(
            OmnidotsFixture.MonitorsList(4),
            new Dictionary<string, DateTime>(),
            Options(allowedSerialIds: ["2", "4"], maxMonitorsPerRun: 4),
            rotationSlot: 0);

        CollectionAssert.AreEqual(new[] { "2", "4" }, selected.Select(monitor => monitor.SerialId).ToArray());
    }

    [TestMethod]
    public void Select_EmptyAllowListIncludesFleetAndAppliesLimit()
    {
        var selected = OmnidotsTraceMonitorSelector.Select(
            OmnidotsFixture.MonitorsList(4),
            new Dictionary<string, DateTime>(),
            Options(maxMonitorsPerRun: 2),
            rotationSlot: 0);

        CollectionAssert.AreEqual(new[] { "1", "2" }, selected.Select(monitor => monitor.SerialId).ToArray());
    }

    [TestMethod]
    public void Select_OrdersUnseenThenOldestLatestTrace()
    {
        var selected = OmnidotsTraceMonitorSelector.Select(
            OmnidotsFixture.MonitorsList(3),
            new Dictionary<string, DateTime>
            {
                ["1"] = Utc(2026, 7, 12),
                ["2"] = Utc(2026, 7, 10)
            },
            Options(maxMonitorsPerRun: 3),
            rotationSlot: 0);

        CollectionAssert.AreEqual(new[] { "3", "2", "1" }, selected.Select(monitor => monitor.SerialId).ToArray());
    }

    [TestMethod]
    public void Select_RotatesWithinEqualPriorityGroupWithoutMutatingFleet()
    {
        var monitors = OmnidotsFixture.MonitorsList(4);
        var originalOrder = monitors.Select(monitor => monitor.SerialId).ToArray();

        var first = OmnidotsTraceMonitorSelector.Select(
            monitors, new Dictionary<string, DateTime>(), Options(maxMonitorsPerRun: 2), rotationSlot: 0);
        var second = OmnidotsTraceMonitorSelector.Select(
            monitors, new Dictionary<string, DateTime>(), Options(maxMonitorsPerRun: 2), rotationSlot: 1);

        CollectionAssert.AreEqual(new[] { "1", "2" }, first.Select(monitor => monitor.SerialId).ToArray());
        CollectionAssert.AreEqual(new[] { "3", "4" }, second.Select(monitor => monitor.SerialId).ToArray());
        CollectionAssert.AreEqual(originalOrder, monitors.Select(monitor => monitor.SerialId).ToArray());
    }

    private static OmnidotsTraceCollectionOptions Options(
        bool enabled = true,
        string[]? allowedSerialIds = null,
        int maxMonitorsPerRun = 1) => new()
        {
            Enabled = enabled,
            AllowedSerialIds = allowedSerialIds ?? [],
            MaxMonitorsPerRun = maxMonitorsPerRun
        };

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
