using System.Text.Json;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.IntegrationTesting;
using Svantek.Model.Http;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
namespace SvantekMonitorTests
{

    public sealed class SvantekFixture
    {
        public static string SamplesResponseJson()
        {
            return MonitorTestUtil.ReadTextFromFile("testdata/latest_samples.json");
        }

        public static List<SampleResponse> SamplesResponseObjects(DateTime? sampleTimeUtc = null)
        {

            string json = SamplesResponseJson();
            List<SampleResponse>? samples = JsonSerializer.Deserialize<List<SampleResponse>>(json);

            if (sampleTimeUtc != null)
            {
                DateTime st = (DateTime)sampleTimeUtc!;
                foreach (SampleResponse sample in samples!)
                {
                    sample.Utc = st;
                    sample.Timestamp = st;
                }
            }
            return samples!;
        }

        public static List<NoiseMonitorReadDto> ReadMonitorDtos(DateTime? lastDataTime)
        {
            return
            [
                ReadMonitorDto("Device1", pointId: 1, lastDataTime: lastDataTime),
                ReadMonitorDto("Device2", pointId: 2, lastDataTime: lastDataTime),
                ReadMonitorDto("Device3", pointId: 3, lastDataTime: lastDataTime)
            ];
        }

        public static NoiseMonitorReadDto ReadMonitorDto(
            string serialId,
            int pointId = 1,
            DateTime? lastDataTime = null,
            int batteryCharge = 100,
            BatteryAlertType batteryStatus = BatteryAlertType.Off)
        {
            DateTime deployedStart = (lastDataTime ?? DateTime.UtcNow).AddDays(-1);
            return new NoiseMonitorReadDto(
                Guid.NewGuid(),
                "123",
                serialId,
                7,
                pointId,
                DateTime.UtcNow,
                lastDataTime,
                null,
                deployedStart,
                false,
                batteryStatus,
                batteryCharge);
        }

        public static AlertActivityTimeDto CreateActiveRuleActivity(DateTime? start, DateTime? end)
        {
            if (start != null)
            {
                start = ((DateTime)start).ToUniversalTime();
            }
            if (end != null)
            {
                end = ((DateTime)end).ToUniversalTime();
            }
            return new AlertActivityTimeDto
            {
                Weekdays = true,
                Sundays = true,
                Saturdays = true,
                StartTime = start != null ? ((DateTime)start!).TimeOfDay : null,
                EndTime = end != null ? ((DateTime)end!).TimeOfDay : null
            };

        }

        internal static List<RvtAlertRuleDto> OfflineRules()
        {
            List<RvtAlertRuleDto> rules =
            [
                new(ruleId: Guid.NewGuid(),
                          serialId: null,
                          field: "offline-rule",
                          limitOn: 0,
                          limitOff: 0,
                          averagingPeriod: 24 * 60 * 60,
                          ruleActivityTime: new AlertActivityTimeDto
                          {
                              Weekdays = true,
                              Saturdays = true,
                              Sundays = true,
                              StartTime = null,
                              EndTime = null
                          },
                        alertType: AlertType.Offline,
                        isActive: true,
                        isDeleted: false,
                        created: DateTime.UtcNow,
                        accessed: null)
            ];

            return rules;
        }
    }


}
