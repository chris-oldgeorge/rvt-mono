using Omnidots.Api.UseCases;
using Omnidots.Model.Config;

namespace OmnidotsAdapterTests.UseCases;

[TestClass]
public sealed class SiteActiveDurationCalculatorTests
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    [TestMethod]
    public void Between_SameDayWithinSiteHours_ReturnsExactElapsedHour()
    {
        SiteTimes siteTimes = WeekdaySchedule(TimeSpan.FromHours(8), TimeSpan.FromHours(18));

        TimeSpan duration = SiteActiveDurationCalculator.Between(
            siteTimes,
            LondonUtc(2026, 7, 14, 9),
            LondonUtc(2026, 7, 14, 10),
            London);

        Assert.AreEqual(TimeSpan.FromHours(1), duration);
    }

    [TestMethod]
    public void Between_BeforeSiteOpens_ReturnsZero()
    {
        SiteTimes siteTimes = WeekdaySchedule(TimeSpan.FromHours(8), TimeSpan.FromHours(18));

        TimeSpan duration = SiteActiveDurationCalculator.Between(
            siteTimes,
            LondonUtc(2026, 7, 14, 5),
            LondonUtc(2026, 7, 14, 7),
            London);

        Assert.AreEqual(TimeSpan.Zero, duration);
    }

    [TestMethod]
    public void Between_AfterSiteCloses_ReturnsZero()
    {
        SiteTimes siteTimes = WeekdaySchedule(TimeSpan.FromHours(8), TimeSpan.FromHours(18));

        TimeSpan duration = SiteActiveDurationCalculator.Between(
            siteTimes,
            LondonUtc(2026, 7, 14, 19),
            LondonUtc(2026, 7, 14, 21),
            London);

        Assert.AreEqual(TimeSpan.Zero, duration);
    }

    [TestMethod]
    public void Between_AcrossMultipleSiteDays_AddsEachDailyIntersection()
    {
        SiteTimes siteTimes = WeekdaySchedule(TimeSpan.FromHours(8), TimeSpan.FromHours(18));

        TimeSpan duration = SiteActiveDurationCalculator.Between(
            siteTimes,
            LondonUtc(2026, 7, 13, 17),
            LondonUtc(2026, 7, 14, 10),
            London);

        Assert.AreEqual(TimeSpan.FromHours(3), duration);
    }

    [TestMethod]
    public void Between_ClosedWeekend_ReturnsZero()
    {
        SiteTimes siteTimes = WeekdaySchedule(TimeSpan.FromHours(8), TimeSpan.FromHours(18));

        TimeSpan duration = SiteActiveDurationCalculator.Between(
            siteTimes,
            LondonUtc(2026, 7, 18, 8),
            LondonUtc(2026, 7, 19, 18),
            London);

        Assert.AreEqual(TimeSpan.Zero, duration);
    }

    [TestMethod]
    public void Between_SpringForwardDay_CountsElapsedUtcTime()
    {
        SiteTimes siteTimes = SundaySchedule(TimeSpan.Zero, TimeSpan.FromHours(4));

        TimeSpan duration = SiteActiveDurationCalculator.Between(
            siteTimes,
            LondonUtc(2026, 3, 29, 0),
            LondonUtc(2026, 3, 29, 4),
            London);

        Assert.AreEqual(TimeSpan.FromHours(3), duration);
    }

    [TestMethod]
    public void Between_FallBackDay_CountsRepeatedElapsedHour()
    {
        SiteTimes siteTimes = SundaySchedule(TimeSpan.Zero, TimeSpan.FromHours(4));

        TimeSpan duration = SiteActiveDurationCalculator.Between(
            siteTimes,
            LondonUtc(2026, 10, 25, 0),
            LondonUtc(2026, 10, 25, 4),
            London);

        Assert.AreEqual(TimeSpan.FromHours(5), duration);
    }

    [TestMethod]
    public void Between_SpringForwardGapBoundary_RejectsScheduleSafely()
    {
        SiteTimes siteTimes = SundaySchedule(TimeSpan.FromMinutes(90), TimeSpan.FromHours(4));

        SiteScheduleConfigurationException exception = Assert.ThrowsExactly<SiteScheduleConfigurationException>(() =>
            SiteActiveDurationCalculator.Between(
                siteTimes,
                LondonUtc(2026, 3, 29, 0),
                LondonUtc(2026, 3, 29, 4),
                London));

        Assert.AreEqual(
            "Site schedule contains an invalid or ambiguous local time boundary.",
            exception.Message);
        Assert.IsNull(exception.InnerException);
    }

    [TestMethod]
    public void Between_FallBackOverlapBoundary_RejectsScheduleSafely()
    {
        SiteTimes siteTimes = SundaySchedule(TimeSpan.FromMinutes(90), TimeSpan.FromHours(4));

        SiteScheduleConfigurationException exception = Assert.ThrowsExactly<SiteScheduleConfigurationException>(() =>
            SiteActiveDurationCalculator.Between(
                siteTimes,
                LondonUtc(2026, 10, 25, 0),
                LondonUtc(2026, 10, 25, 4),
                London));

        Assert.AreEqual(
            "Site schedule contains an invalid or ambiguous local time boundary.",
            exception.Message);
        Assert.IsNull(exception.InnerException);
    }

    [TestMethod]
    public void Between_ExplicitMidnightToTwentyFourHoursFixture_IsAlwaysOpen()
    {
        TimeSpan duration = SiteActiveDurationCalculator.Between(
            OmnidotsFixture.AlwaysOpenSiteTimes(),
            new DateTime(2026, 7, 14, 12, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 15, 12, 30, 0, DateTimeKind.Utc),
            TimeZoneInfo.Utc);

        Assert.AreEqual(TimeSpan.FromHours(24), duration);
    }

    private static SiteTimes WeekdaySchedule(TimeSpan start, TimeSpan end) => new()
    {
        WeekdayStart = start,
        WeekdayEnd = end
    };

    private static SiteTimes SundaySchedule(TimeSpan start, TimeSpan end) => new()
    {
        SundayStart = start,
        SundayEnd = end
    };

    private static DateTime LondonUtc(int year, int month, int day, int hour) =>
        TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified),
            London);
}
