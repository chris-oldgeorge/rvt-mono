using Svantek.Api;
using Svantek.Model.Config;

namespace SvantekMonitorTests;

[TestClass]
public sealed class NoiseRequestWindowCalculatorTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime UtcMin = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    private static readonly DateTime UtcMax = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

    [TestMethod]
    public void Calculate_CapsFirstImportAtSevenDaysAndEmitsContiguousBoundedWindows()
    {
        var calculator = CreateCalculator();

        var windows = calculator.Calculate(
            deploymentStart: UtcNow.AddDays(-30),
            watermark: null,
            lastStatusTimestamp: null,
            utcNow: UtcNow);

        Assert.HasCount(14, windows);
        Assert.AreEqual(UtcNow.AddDays(-7), windows[0].Start);
        Assert.AreEqual(UtcNow, windows[^1].End);
        Assert.IsTrue(windows.All(window => window.End - window.Start <= TimeSpan.FromHours(12)));
        Assert.IsTrue(windows.All(window =>
            window.Start.Kind == DateTimeKind.Utc && window.End.Kind == DateTimeKind.Utc));
        for (var index = 1; index < windows.Count; index++)
        {
            Assert.AreEqual(windows[index - 1].End, windows[index].Start);
        }
    }

    [TestMethod]
    public void Calculate_StartsAtWatermarkMinusOverlapWhenAfterDeployment()
    {
        var calculator = CreateCalculator();
        var watermark = UtcNow.AddHours(-2);

        var windows = calculator.Calculate(
            deploymentStart: UtcNow.AddDays(-1),
            watermark: watermark,
            lastStatusTimestamp: null,
            utcNow: UtcNow);

        Assert.AreEqual(watermark.AddMinutes(-5), windows[0].Start);
    }

    [TestMethod]
    public void Calculate_DoesNotOverlapBeforeDeployment()
    {
        var calculator = CreateCalculator();
        var deployment = UtcNow.AddMinutes(-2);

        var windows = calculator.Calculate(
            deploymentStart: deployment,
            watermark: UtcNow,
            lastStatusTimestamp: null,
            utcNow: UtcNow);

        Assert.HasCount(1, windows);
        Assert.AreEqual(deployment, windows[0].Start);
    }

    [TestMethod]
    public void Calculate_EndsOneHourAfterLastStatusTimestamp()
    {
        var calculator = CreateCalculator();

        var windows = calculator.Calculate(
            deploymentStart: UtcNow.AddHours(-4),
            watermark: UtcNow.AddHours(-3),
            lastStatusTimestamp: UtcNow.AddHours(-2),
            utcNow: UtcNow);

        Assert.AreEqual(UtcNow.AddHours(-1), windows[^1].End);
    }

    [TestMethod]
    public void Calculate_ClampsFutureStatusEndToInjectedUtcNow()
    {
        var calculator = CreateCalculator();

        var windows = calculator.Calculate(
            deploymentStart: UtcNow.AddHours(-2),
            watermark: UtcNow.AddHours(-1),
            lastStatusTimestamp: UtcNow.AddHours(2),
            utcNow: UtcNow);

        Assert.AreEqual(UtcNow, windows[^1].End);
    }

    [TestMethod]
    public void Calculate_ReturnsEmptyForFutureInterval()
    {
        var calculator = CreateCalculator();

        var windows = calculator.Calculate(
            deploymentStart: UtcNow.AddMinutes(1),
            watermark: null,
            lastStatusTimestamp: null,
            utcNow: UtcNow);

        Assert.IsEmpty(windows);
    }

    [TestMethod]
    public void Calculate_ReturnsEmptyForDegenerateInterval()
    {
        var calculator = CreateCalculator();

        var windows = calculator.Calculate(
            deploymentStart: UtcNow,
            watermark: UtcNow,
            lastStatusTimestamp: null,
            utcNow: UtcNow);

        Assert.IsEmpty(windows);
    }

    [TestMethod]
    public void Calculate_EmitsExactTwelveHourSlices()
    {
        var calculator = CreateCalculator();
        var deployment = UtcNow.AddHours(-24);

        var windows = calculator.Calculate(
            deploymentStart: deployment,
            watermark: deployment,
            lastStatusTimestamp: null,
            utcNow: UtcNow);

        Assert.HasCount(2, windows);
        Assert.AreEqual(new NoiseRequestWindow(deployment, deployment.AddHours(12)), windows[0]);
        Assert.AreEqual(new NoiseRequestWindow(deployment.AddHours(12), UtcNow), windows[1]);
    }

    [TestMethod]
    public void Calculate_EmitsShortFinalSlice()
    {
        var calculator = CreateCalculator();
        var deployment = UtcNow.AddHours(-25);

        var windows = calculator.Calculate(
            deploymentStart: deployment,
            watermark: deployment,
            lastStatusTimestamp: null,
            utcNow: UtcNow);

        Assert.HasCount(3, windows);
        Assert.AreEqual(TimeSpan.FromHours(12), windows[0].End - windows[0].Start);
        Assert.AreEqual(TimeSpan.FromHours(12), windows[1].End - windows[1].Start);
        Assert.AreEqual(TimeSpan.FromHours(1), windows[2].End - windows[2].Start);
        Assert.AreEqual(UtcNow, windows[2].End);
    }

    [TestMethod]
    public void Calculate_SaturatesWatermarkOverlapAtMinimumUtcDateTime()
    {
        var calculator = CreateCalculator();
        var now = UtcMin.AddHours(1);

        var windows = calculator.Calculate(
            deploymentStart: UtcMin,
            watermark: UtcMin,
            lastStatusTimestamp: null,
            utcNow: now);

        Assert.HasCount(1, windows);
        Assert.AreEqual(new NoiseRequestWindow(UtcMin, now), windows[0]);
    }

    [TestMethod]
    public void Calculate_SaturatesInitialBackfillAtMinimumUtcDateTime()
    {
        var calculator = CreateCalculator();
        var now = UtcMin.AddHours(1);

        var windows = calculator.Calculate(
            deploymentStart: UtcMin,
            watermark: null,
            lastStatusTimestamp: null,
            utcNow: now);

        Assert.HasCount(1, windows);
        Assert.AreEqual(new NoiseRequestWindow(UtcMin, now), windows[0]);
    }

    [TestMethod]
    public void Calculate_SaturatesStatusExtensionAtMaximumUtcDateTime()
    {
        var calculator = CreateCalculator();
        var deployment = UtcMax.AddHours(-1);

        var windows = calculator.Calculate(
            deploymentStart: deployment,
            watermark: deployment.AddMinutes(5),
            lastStatusTimestamp: UtcMax,
            utcNow: UtcMax);

        Assert.HasCount(1, windows);
        Assert.AreEqual(new NoiseRequestWindow(deployment, UtcMax), windows[0]);
    }

    [TestMethod]
    public void Calculate_SaturatesSliceAdvancementAtMaximumUtcDateTime()
    {
        var calculator = CreateCalculator();
        var deployment = UtcMax.AddHours(-1);

        var windows = calculator.Calculate(
            deploymentStart: deployment,
            watermark: deployment.AddMinutes(5),
            lastStatusTimestamp: null,
            utcNow: UtcMax);

        Assert.HasCount(1, windows);
        Assert.AreEqual(new NoiseRequestWindow(deployment, UtcMax), windows[0]);
    }

    [TestMethod]
    public void Calculate_NormalizesMixedDateTimeKindsBeforeComparingAndCalculating()
    {
        var calculator = CreateCalculator();
        var localNow = UtcNow.ToLocalTime();
        var localWatermark = UtcNow.AddHours(-3).ToLocalTime();
        var unspecifiedStatus = DateTime.SpecifyKind(UtcNow.AddHours(-1), DateTimeKind.Unspecified);

        var windows = calculator.Calculate(
            deploymentStart: UtcNow.AddHours(-4),
            watermark: localWatermark,
            lastStatusTimestamp: unspecifiedStatus,
            utcNow: localNow);

        Assert.AreEqual(UtcNow.AddHours(-3).AddMinutes(-5), windows[0].Start);
        Assert.AreEqual(UtcNow, windows[^1].End);
        Assert.IsTrue(windows.All(window =>
            window.Start.Kind == DateTimeKind.Utc && window.End.Kind == DateTimeKind.Utc));
    }

    [TestMethod]
    public void Calculate_TreatsUnspecifiedInputsAsUtcAndEmitsUtcBoundaries()
    {
        var calculator = CreateCalculator();
        var unspecifiedNow = DateTime.SpecifyKind(UtcNow, DateTimeKind.Unspecified);
        var unspecifiedDeployment = DateTime.SpecifyKind(UtcNow.AddHours(-2), DateTimeKind.Unspecified);
        var unspecifiedWatermark = DateTime.SpecifyKind(UtcNow.AddHours(-1), DateTimeKind.Unspecified);

        var windows = calculator.Calculate(
            deploymentStart: unspecifiedDeployment,
            watermark: unspecifiedWatermark,
            lastStatusTimestamp: null,
            utcNow: unspecifiedNow);

        Assert.HasCount(1, windows);
        Assert.AreEqual(UtcNow.AddHours(-1).AddMinutes(-5), windows[0].Start);
        Assert.AreEqual(UtcNow, windows[0].End);
        Assert.AreEqual(DateTimeKind.Utc, windows[0].Start.Kind);
        Assert.AreEqual(DateTimeKind.Utc, windows[0].End.Kind);
    }

    private static NoiseRequestWindowCalculator CreateCalculator() =>
        new(new SvantekImportOptions());
}
