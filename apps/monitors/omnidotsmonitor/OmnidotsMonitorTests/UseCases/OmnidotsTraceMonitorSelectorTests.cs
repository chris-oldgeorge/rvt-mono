using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;

namespace OmnidotsAdapterTests.UseCases;

[TestClass]
public sealed class OmnidotsTraceMonitorSelectorTests
{
    [TestMethod]
    public void Select_DisabledCollection_ReturnsNoMonitors()
    {
        IReadOnlyList<VibrationMonitorDto> selected = OmnidotsTraceMonitorSelector.Select(
            OmnidotsFixture.MonitorsList(2),
            new Dictionary<string, DateTime>(),
            Options(enabled: false, maxMonitorsPerRun: 1),
            rotationSlot: 0);

        Assert.IsEmpty(selected);
    }

    private static readonly string[] _allowedSerialIds = ["2", "4"];
    private static readonly string[] _secondRotationSerialIds = ["3", "4"];

    [TestMethod]
    public void Select_AllowListFiltersFleet()
    {
        IReadOnlyList<VibrationMonitorDto> selected = OmnidotsTraceMonitorSelector.Select(
            OmnidotsFixture.MonitorsList(4),
            new Dictionary<string, DateTime>(),
            Options(allowedSerialIds: ["2", "4"], maxMonitorsPerRun: 4),
            rotationSlot: 0);

        CollectionAssert.AreEqual(_allowedSerialIds, selected.Select(monitor => monitor.SerialId).ToArray());
    }

    private static readonly string[] _limitedSerialIds = ["1", "2"];

    [TestMethod]
    public void Select_EmptyAllowListIncludesFleetAndAppliesLimit()
    {
        IReadOnlyList<VibrationMonitorDto> selected = OmnidotsTraceMonitorSelector.Select(
            OmnidotsFixture.MonitorsList(4),
            new Dictionary<string, DateTime>(),
            Options(maxMonitorsPerRun: 2),
            rotationSlot: 0);

        CollectionAssert.AreEqual(_limitedSerialIds, selected.Select(monitor => monitor.SerialId).ToArray());
    }

    private static readonly string[] _latestTraceOrder = ["3", "2", "1"];

    [TestMethod]
    public void Select_OrdersUnseenThenOldestLatestTrace()
    {
        IReadOnlyList<VibrationMonitorDto> selected = OmnidotsTraceMonitorSelector.Select(
            OmnidotsFixture.MonitorsList(3),
            new Dictionary<string, DateTime>
            {
                ["1"] = Utc(2026, 7, 12),
                ["2"] = Utc(2026, 7, 10)
            },
            Options(maxMonitorsPerRun: 3),
            rotationSlot: 0);

        CollectionAssert.AreEqual(_latestTraceOrder, selected.Select(monitor => monitor.SerialId).ToArray());
    }

    [TestMethod]
    public void Select_RotatesWithinEqualPriorityGroupWithoutMutatingFleet()
    {
        List<VibrationMonitorDto> monitors = OmnidotsFixture.MonitorsList(4);
        string[] originalOrder = [.. monitors.Select(monitor => monitor.SerialId)];

        IReadOnlyList<VibrationMonitorDto> first = OmnidotsTraceMonitorSelector.Select(
            monitors, new Dictionary<string, DateTime>(), Options(maxMonitorsPerRun: 2), rotationSlot: 0);
        IReadOnlyList<VibrationMonitorDto> second = OmnidotsTraceMonitorSelector.Select(
            monitors, new Dictionary<string, DateTime>(), Options(maxMonitorsPerRun: 2), rotationSlot: 1);

        CollectionAssert.AreEqual(_limitedSerialIds, first.Select(monitor => monitor.SerialId).ToArray());
        CollectionAssert.AreEqual(_secondRotationSerialIds, second.Select(monitor => monitor.SerialId).ToArray());
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
