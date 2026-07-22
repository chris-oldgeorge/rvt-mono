using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Svantek.Model.Dto;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace SvantekMonitorTests
{
    [TestClass]
    public class TestSiteInfo
    {
        public TestSiteInfo()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestSendInfo");
        }


        [DataRow("09:00:00", "17:00:00", "09:30:00", "12:30:00", "10:00:00", "11:00:00", true, true, true)]
        [DataRow(null, null, "19:30:00", "21:30:00", "11:00:11", "12:00:12", false, true, true)]
        [DataRow("09:00:00", "17:00:00", null, null, "10:00:00", "11:00:00", true, false, true)]
        [DataRow("09:00:00", "17:00:00", "10:23:00", "11:20:00", null, null, true, true, false)]
        [DataRow(null, null, null, null, null, null, false, false, false)]
        [DataTestMethod]
        public void TestSiteInfo_ShouldReportForDate_Success(string? start, string? end,
                                     string? satStart, string? satEnd,
                                     string? sunStart, string? sunEnd,
                                     bool weekday, bool saturday, bool sunday)
        {

            TimeSpan? startTime = start != null ? TimeSpan.Parse(start!) : null;
            TimeSpan? endTime = end != null ? TimeSpan.Parse(end!) : null;
            TimeSpan? satStartTime = satStart != null ? TimeSpan.Parse(satStart!) : null;
            TimeSpan? satEndTime = satEnd != null ? TimeSpan.Parse(satEnd!) : null;
            TimeSpan? sunStartTime = sunStart != null ? TimeSpan.Parse(sunStart!) : null;
            TimeSpan? sunEndTime = sunEnd != null ? TimeSpan.Parse(sunEnd!) : null;

            var testObj = new SiteInfoDto(siteId: Guid.NewGuid(),
                                          startTime: startTime,
                                          endTime: endTime,
                                          satStartTime: satStartTime,
                                          satEndTime: satEndTime,
                                          sunStartTime: sunStartTime,
                                          sunEndTime: sunEndTime);

            Assert.AreEqual(saturday, testObj.ShouldReportForDate(DateTime.Parse("Sat, 18 Aug 2018 00:22:16")));
            Assert.AreEqual(sunday, testObj.ShouldReportForDate(DateTime.Parse("Sun, 19 Aug 2018 00:22:16")));
            Assert.AreEqual(weekday, testObj.ShouldReportForDate(DateTime.Parse("Mon, 20 Aug 2018 00:22:16")));


        }


        [DataRow("Sat, 18 Aug 2018 00:00:00")]
        [DataRow("Sun, 19 Aug 2018 00:00:00")]
        [DataRow("Tue, 21 Aug 2018 00:00:00")]
        [DataTestMethod]
        public void TestSiteInfo_GetStartAndEndTimeForDate_Success(string dateStr)
        {

            var testObj = new SiteInfoDto(siteId: Guid.NewGuid(),
                                          startTime: TimeSpan.Parse("09:10:11"),
                                          endTime: TimeSpan.Parse("17:18:19"),
                                          satStartTime: TimeSpan.Parse("12:31:05"),
                                          satEndTime: TimeSpan.Parse("15:42:08"),
                                          sunStartTime: TimeSpan.Parse("10:11:12"),
                                          sunEndTime: TimeSpan.Parse("14:15:16"));

            var date = DateTime.Parse(dateStr);


            testObj.GetStartAndEndTimeForDate(date, out DateTime startTime, out DateTime endTime);


            Assert.AreEqual(date.Year, startTime.Year);
            Assert.AreEqual(date.Month, startTime.Month);
            Assert.AreEqual(date.Day, startTime.Day);

            Assert.AreEqual(date.Year, endTime.Year);
            Assert.AreEqual(date.Month, endTime.Month);
            Assert.AreEqual(date.Day, endTime.Day);

            switch (date.DayOfWeek)
            {
                case DayOfWeek.Saturday:
                    Assert.AreEqual(11, startTime.Hour);
                    Assert.AreEqual(31, startTime.Minute);
                    Assert.AreEqual(5, startTime.Second);

                    Assert.AreEqual(14, endTime.Hour);
                    Assert.AreEqual(42, endTime.Minute);
                    Assert.AreEqual(8, endTime.Second);
                    break;
                case DayOfWeek.Sunday:
                    Assert.AreEqual(9, startTime.Hour);
                    Assert.AreEqual(11, startTime.Minute);
                    Assert.AreEqual(12, startTime.Second);

                    Assert.AreEqual(13, endTime.Hour);
                    Assert.AreEqual(15, endTime.Minute);
                    Assert.AreEqual(16, endTime.Second);
                    break;
                case DayOfWeek.Monday:
                case DayOfWeek.Tuesday:
                case DayOfWeek.Wednesday:
                case DayOfWeek.Thursday:
                case DayOfWeek.Friday:
                    Assert.AreEqual(8, startTime.Hour);
                    Assert.AreEqual(10, startTime.Minute);
                    Assert.AreEqual(11, startTime.Second);

                    Assert.AreEqual(16, endTime.Hour);
                    Assert.AreEqual(18, endTime.Minute);
                    Assert.AreEqual(19, endTime.Second);
                    break;

            }


        }


    }
}
