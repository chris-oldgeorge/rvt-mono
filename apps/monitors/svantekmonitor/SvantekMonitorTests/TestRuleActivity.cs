using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
namespace SvantekMonitorTests
{

    // Summary: Verifies alert activity day/time matching against local-time rule windows.
    // Major updates:
    // - 2026-06-18 Test stability: build expected windows from the same local time conversion used by rules.
    [TestClass]
    public class TestRuleActivity
    {


        public TestRuleActivity()
        {
            ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestRuleActivity");
        }


        [TestMethod]
        public void TestAlertRule_Success()
        {
            DateTime dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            TimeSpan localTime = DateTimeUtil.UtcToLocal(dt.TimeOfDay);
            AlertActivityTimeDto testObj = new()
            {
                Saturdays = false,
                Sundays = false,
                Weekdays = true,
                StartTime = localTime.Add(TimeSpan.FromMinutes(-1)),
                EndTime = localTime.Add(TimeSpan.FromMinutes(1))
            };
            Assert.IsTrue(testObj.IsActive(dt));
        }

        [TestMethod]
        public void TestAlertRuleBeforeTime_Success()
        {
            DateTime dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            TimeSpan localTime = DateTimeUtil.UtcToLocal(dt.TimeOfDay);
            AlertActivityTimeDto testObj = new()
            {
                Saturdays = false,
                Sundays = false,
                Weekdays = true,
                StartTime = localTime.Add(TimeSpan.FromMinutes(-2)),
                EndTime = localTime.Add(TimeSpan.FromMinutes(-1))
            };
            Assert.IsFalse(testObj.IsActive(dt));
        }

        [TestMethod]
        public void TestAlertRuleAfterTime_Success()
        {
            DateTime dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            TimeSpan localTime = DateTimeUtil.UtcToLocal(dt.TimeOfDay);
            AlertActivityTimeDto testObj = new()
            {
                Saturdays = false,
                Sundays = false,
                Weekdays = true,
                StartTime = localTime.Add(TimeSpan.FromMinutes(1)),
                EndTime = localTime.Add(TimeSpan.FromMinutes(2))
            };
            Assert.IsFalse(testObj.IsActive(dt));
        }

        [TestMethod]
        public void TestAlertRuleWeekdaysNullTime_Success()
        {
            DateTime dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            AlertActivityTimeDto testObj = new()
            {
                Saturdays = false,
                Sundays = false,
                Weekdays = true
            };
            Assert.IsTrue(testObj.IsActive(dt));
        }

        [TestMethod]
        public void TestAlertRuleWeekdaysNullEndTime_Success()
        {
            DateTime dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            AlertActivityTimeDto testObj = new()
            {
                Saturdays = false,
                Sundays = false,
                Weekdays = true,
                StartTime = dt.TimeOfDay
            };
            Assert.IsTrue(testObj.IsActive(dt));
        }

        [TestMethod]
        public void TestAlertRuleWeekdaysNullStartTime_Success()
        {
            DateTime dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            AlertActivityTimeDto testObj = new()
            {
                Saturdays = false,
                Sundays = false,
                Weekdays = true,
                EndTime = dt.TimeOfDay
            };
            Assert.IsTrue(testObj.IsActive(dt));
        }


        [TestMethod]
        public void TestAlertRuleNotWeekday_Success()
        {
            DateTime dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            AlertActivityTimeDto testObj = new()
            {
                Saturdays = true,
                Sundays = true,
                Weekdays = false,
                StartTime = dt.AddMinutes(-1).TimeOfDay,
                EndTime = dt.AddMinutes(1).TimeOfDay
            };
            Assert.IsFalse(testObj.IsActive(dt));
        }

        [TestMethod]
        public void TestAlertRuleNotSunday_Success()
        {
            DateTime dt = DateTime.Parse("Sun, 1 Oct 2023 07:22:16 GMT");
            AlertActivityTimeDto testObj = new()
            {
                Saturdays = true,
                Sundays = false,
                Weekdays = true,
                StartTime = dt.AddMinutes(-1).TimeOfDay,
                EndTime = dt.AddMinutes(1).TimeOfDay
            };
            Assert.IsFalse(testObj.IsActive(dt));
        }

        [TestMethod]
        public void TestAlertRuleNotSaturday_Success()
        {
            DateTime dt = DateTime.Parse("Sat, 30 Sep 2023 07:22:16 GMT");
            AlertActivityTimeDto testObj = new()
            {
                Saturdays = false,
                Sundays = true,
                Weekdays = true,
                StartTime = dt.AddMinutes(-1).TimeOfDay,
                EndTime = dt.AddMinutes(1).TimeOfDay
            };
            Assert.IsFalse(testObj.IsActive(dt));
        }

    }

}
