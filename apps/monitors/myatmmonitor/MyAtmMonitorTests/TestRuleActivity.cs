using Microsoft.Extensions.Logging;

using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Rules;

using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace MyAtmMonitorTests
{

    [TestClass]
    public class TestRuleActivity
    {

        public TestRuleActivity()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestRuleActivity");
        }

        [TestMethod]
        public void TestAlertRule_Success()
        {
            var dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            var testObj = new AlertActivityTimeDto
            {
                Saturdays = false,
                Sundays = false,
                Weekdays = true,
                StartTime = dt.AddMinutes(-1).TimeOfDay,
                EndTime = dt.AddMinutes(1).TimeOfDay
            };
            Assert.IsTrue(testObj.IsActive(dt));
        }

        [TestMethod]
        public void TestAlertRuleBeforeTime_Success()
        {
            var dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            var testObj = new AlertActivityTimeDto
            {
                Saturdays = false,
                Sundays = false,
                Weekdays = true,
                StartTime = dt.AddMinutes(-2).TimeOfDay,
                EndTime = dt.AddMinutes(-1).TimeOfDay
            };
            Assert.IsTrue(testObj.IsActive(dt));
        }

        [TestMethod]
        public void TestAlertRuleAfterTime_Success()
        {
            var dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            var testObj = new AlertActivityTimeDto
            {
                Saturdays = false,
                Sundays = false,
                Weekdays = true,
                StartTime = dt.AddMinutes(1).TimeOfDay,
                EndTime = dt.AddMinutes(2).TimeOfDay
            };
            Assert.IsTrue(testObj.IsActive(dt));
        }

        [TestMethod]
        public void TestAlertRuleWeekdaysNullTime_Success()
        {
            var dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            var testObj = new AlertActivityTimeDto
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
            var dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            var testObj = new AlertActivityTimeDto
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
            var dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            var testObj = new AlertActivityTimeDto
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
            var dt = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
            var testObj = new AlertActivityTimeDto
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
            var dt = DateTime.Parse("Sun, 1 Oct 2023 07:22:16 GMT");
            var testObj = new AlertActivityTimeDto
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
            var dt = DateTime.Parse("Sat, 30 Sep 2023 07:22:16 GMT");
            var testObj = new AlertActivityTimeDto
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
