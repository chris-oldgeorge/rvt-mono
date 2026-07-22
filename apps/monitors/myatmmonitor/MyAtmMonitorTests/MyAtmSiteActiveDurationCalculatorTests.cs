using MyAtm.Api.UseCases;
using MyAtm.Model.Config;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmSiteActiveDurationCalculatorTests
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    [TestMethod]
    public void Between_AcrossMultipleSiteDays_AddsOnlyOperatingIntersections()
    {
        var schedule = WeekdaySchedule(TimeSpan.FromHours(8), TimeSpan.FromHours(18));

        var duration = MyAtmSiteActiveDurationCalculator.Between(
            schedule,
            LondonUtc(2026, 7, 13, 17),
            LondonUtc(2026, 7, 14, 10),
            London);

        Assert.AreEqual(TimeSpan.FromHours(3), duration);
    }

    [TestMethod]
    public void Between_ClosedWeekend_ReturnsZero()
    {
        var schedule = WeekdaySchedule(TimeSpan.FromHours(8), TimeSpan.FromHours(18));

        var duration = MyAtmSiteActiveDurationCalculator.Between(
            schedule,
            LondonUtc(2026, 7, 18, 8),
            LondonUtc(2026, 7, 19, 18),
            London);

        Assert.AreEqual(TimeSpan.Zero, duration);
    }

    [TestMethod]
    public void Between_OvernightSchedule_CountsBothSidesOfMidnight()
    {
        var schedule = WeekdaySchedule(TimeSpan.FromHours(20), TimeSpan.FromHours(4));

        var duration = MyAtmSiteActiveDurationCalculator.Between(
            schedule,
            LondonUtc(2026, 7, 13, 22),
            LondonUtc(2026, 7, 14, 2),
            London);

        Assert.AreEqual(TimeSpan.FromHours(4), duration);
    }

    [TestMethod]
    public void Between_SpringForwardAndFallBack_CountsElapsedUtcTime()
    {
        var schedule = SundaySchedule(TimeSpan.Zero, TimeSpan.FromHours(4));

        var spring = MyAtmSiteActiveDurationCalculator.Between(
            schedule,
            LondonUtc(2026, 3, 29, 0),
            LondonUtc(2026, 3, 29, 4),
            London);
        var fall = MyAtmSiteActiveDurationCalculator.Between(
            schedule,
            LondonUtc(2026, 10, 25, 0),
            LondonUtc(2026, 10, 25, 4),
            London);

        Assert.AreEqual(TimeSpan.FromHours(3), spring);
        Assert.AreEqual(TimeSpan.FromHours(5), fall);
    }

    [TestMethod]
    public void Between_InvalidOrAmbiguousBoundary_RejectsSchedule()
    {
        var schedule = SundaySchedule(TimeSpan.FromMinutes(90), TimeSpan.FromHours(4));

        Assert.ThrowsExactly<MyAtmSiteScheduleConfigurationException>(() =>
            MyAtmSiteActiveDurationCalculator.Between(
                schedule,
                LondonUtc(2026, 3, 29, 0),
                LondonUtc(2026, 3, 29, 4),
                London));
        Assert.ThrowsExactly<MyAtmSiteScheduleConfigurationException>(() =>
            MyAtmSiteActiveDurationCalculator.Between(
                schedule,
                LondonUtc(2026, 10, 25, 0),
                LondonUtc(2026, 10, 25, 4),
                London));
    }

    [TestMethod]
    public void Between_NonUtcInput_RejectsConversionGuessing()
    {
        var schedule = WeekdaySchedule(TimeSpan.Zero, TimeSpan.FromHours(24));
        var from = new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Unspecified);
        var to = new DateTime(2026, 7, 16, 9, 0, 0, DateTimeKind.Utc);

        Assert.ThrowsExactly<ArgumentException>(() =>
            MyAtmSiteActiveDurationCalculator.Between(schedule, from, to, TimeZoneInfo.Utc));
    }

    [TestMethod]
    public void Between_IncompleteDailySchedule_RejectsConfiguration()
    {
        var schedule = new MyAtmSiteSchedule { WeekdayStart = TimeSpan.FromHours(8) };

        Assert.ThrowsExactly<MyAtmSiteScheduleConfigurationException>(() =>
            MyAtmSiteActiveDurationCalculator.Between(
                schedule,
                new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 16, 9, 0, 0, DateTimeKind.Utc),
                TimeZoneInfo.Utc));
    }

    private static MyAtmSiteSchedule WeekdaySchedule(TimeSpan start, TimeSpan end) => new()
    {
        WeekdayStart = start,
        WeekdayEnd = end
    };

    private static MyAtmSiteSchedule SundaySchedule(TimeSpan start, TimeSpan end) => new()
    {
        SundayStart = start,
        SundayEnd = end
    };

    private static DateTime LondonUtc(int year, int month, int day, int hour) =>
        TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified),
            London);
}
