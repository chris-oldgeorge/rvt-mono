using Rvt.Monitor.Common.Alerts;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class AlertSendWindowTests
{
    private static readonly DateTime _day = new(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void TryCreate_WithNoConfiguredBound_ReturnsNoWindowSoTheContactIsStillReached()
    {
        Assert.IsNull(AlertSendWindow.TryCreate(null, null));
        Assert.IsNull(AlertSendWindow.TryCreate(new TimeSpan(9, 0, 0), null));
        Assert.IsNull(AlertSendWindow.TryCreate(null, new TimeSpan(17, 0, 0)));
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(24, 0)]
    [DataRow(0, 24)]
    public void TryCreate_WithABoundOutsideASingleDay_ReturnsNoWindow(int startHours, int endHours)
    {
        Assert.IsNull(AlertSendWindow.TryCreate(
            TimeSpan.FromHours(startHours),
            TimeSpan.FromHours(endHours)));
    }

    [TestMethod]
    [DataRow(8, 59, false)]
    [DataRow(9, 0, true)]
    [DataRow(9, 30, true)]
    [DataRow(17, 0, true)]
    [DataRow(17, 1, false)]
    [DataRow(3, 0, false)]
    public void IsOpenAt_SameDayWindow_IsInclusiveOfBothBounds(int hour, int minute, bool expected)
    {
        AlertSendWindow window = new(new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0));

        Assert.AreEqual(expected, window.IsOpenAt(_day.AddHours(hour).AddMinutes(minute)));
    }

    [TestMethod]
    [DataRow(22, 0, true)]
    [DataRow(23, 59, true)]
    [DataRow(0, 30, true)]
    [DataRow(6, 0, true)]
    [DataRow(6, 1, false)]
    [DataRow(14, 0, false)]
    public void IsOpenAt_MidnightSpanningWindow_WrapsAcrossTheDayBoundary(int hour, int minute, bool expected)
    {
        AlertSendWindow window = new(new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0));

        Assert.AreEqual(expected, window.IsOpenAt(_day.AddHours(hour).AddMinutes(minute)));
    }

    [TestMethod]
    public void NextOpeningAfter_BeforeTodaysOpening_IsTodaysOpening()
    {
        AlertSendWindow window = new(new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0));

        Assert.AreEqual(_day.AddHours(9), window.NextOpeningAfter(_day.AddHours(3)));
    }

    [TestMethod]
    public void NextOpeningAfter_AfterTodaysClose_IsTomorrowsOpening()
    {
        AlertSendWindow window = new(new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0));

        Assert.AreEqual(_day.AddDays(1).AddHours(9), window.NextOpeningAfter(_day.AddHours(20)));
    }

    [TestMethod]
    public void NextOpeningAfter_MidnightSpanningWindow_IsTodaysOpening()
    {
        AlertSendWindow window = new(new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0));

        Assert.AreEqual(_day.AddHours(22), window.NextOpeningAfter(_day.AddHours(14)));
    }

    [TestMethod]
    public void NextOpeningAfter_IsAlwaysStrictlyInTheFutureAndAtMostADayAway()
    {
        // This is what keeps a deferred outbox row from being reclaimed in a
        // tight loop: every minute of every window shape must move the row
        // forward, never to now or to the past.
        foreach (int startHour in Enumerable.Range(0, 24))
        {
            foreach (int endHour in Enumerable.Range(0, 24))
            {
                AlertSendWindow window = new(
                    TimeSpan.FromHours(startHour),
                    TimeSpan.FromHours(endHour));
                foreach (int minuteOfDay in Enumerable.Range(0, 24 * 60))
                {
                    DateTime utcNow = _day.AddMinutes(minuteOfDay);
                    if (window.IsOpenAt(utcNow))
                    {
                        continue;
                    }

                    DateTime opening = window.NextOpeningAfter(utcNow);
                    Assert.IsGreaterThan(utcNow, opening);
                    Assert.IsLessThanOrEqualTo(utcNow.AddDays(1), opening);
                }
            }
        }
    }
}
