// File summary: Provides canned Omnidots API payloads and monitor DTO fixtures for unit and integration-style tests.
// Major updates:
// - 2026-06-18: Aligned fixture deploy dates and config secret payloads with current production timestamp/secret behavior.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using static Omnidots.Api.OmnidotsApi;
using AlertActivityTimeDto = Rvt.Monitor.Common.Notifications.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Notifications.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Notifications.RvtContactDto;
namespace OmnidotsAdapterTests
{

    public sealed class OmnidotsFixture
    {
        public static readonly int MINUTES_ELAPSED = 10;
        public static readonly DateTime SampleDeployDate = DateTime.Parse("2023-01-01T00:00:00Z").ToUniversalTime();

        public static string AuthenticateJson(string token)
        {
            return "{\"ok\": true, \"token\": \"" + token + "\"}";
        }

        public static string ErrorJson()
        {
            return "{\n    \"ok\": false,\n    \"message\": \"Some error message.\"\n}";
        }

        public static Task<string> AuthenticateTask(string token = "702811da14ff4225973c4054ed52bb9f")
        {
            return StringTask(AuthenticateJson(token));
        }


        public static Task<string> StringTask(string str)
        {
            return Task<string>.Factory.StartNew(() => str);
        }

        public static string MeasuringPointsJson()
        {
            return TestUtil.ReadTextFromFile("testdata/measuring_points.json");
        }

        public static SiteTimes AlwaysOpenSiteTimes()
        {
            return new SiteTimes
            {
                WeekdayStart = TimeSpan.Zero,
                WeekdayEnd = TimeSpan.FromDays(1),
                SaturdayStart = TimeSpan.Zero,
                SaturdayEnd = TimeSpan.FromDays(1),
                SundayStart = TimeSpan.Zero,
                SundayEnd = TimeSpan.FromDays(1)
            };
        }

        public static string PeakRecordsJson()
        {
            return TestUtil.ReadTextFromFile("testdata/peak_response.json");
        }

        public static string VeffRecordsJson()
        {
            return TestUtil.ReadTextFromFile("testdata/veff_response.json");
        }

        public static string VdvRecordsJson()
        {
            return TestUtil.ReadTextFromFile("testdata/vdv_response.json");
        }

        public static string TracesResponseJson()
        {
            return TestUtil.ReadTextFromFile("testdata/traces.json");
        }


        public static List<VibrationMonitorDto> MonitorsList(int numMonitors, DateTime? lastDataTime = null,
                                                             bool alwaysMakeSensor = false, int serialIdIn = 0,
                                                             int? batteryLevel = null,
                                                             BatteryAlertType batteryStatus = BatteryAlertType.Off,
                                                             string? timeZone = "foo/bar")
        {
            var monitors = new List<VibrationMonitorDto>();
            for (var i = 1; i <= numMonitors; i++)
            {
                var serialId = "" + (serialIdIn + i);
                var monitorId = Guid.NewGuid();

                var monitorStatus = new VibrationMonitorStatusDto(
                                        serialId: serialId,
                                        measurementDuration: i * 7,
                                        dataSaveLevel: i * 7.543,
                                        vdvEnabled: i % 2 == 0,
                                        vdvX: i + 1 % 2 == 0 ? "bar" : null,
                                        vdvY: i == 1 ? null : "baz",
                                        vdvZ: i == 2 ? null : "bobbobbob",
                                        vdvPeriod: i * 99,
                                        traceSaveLevel: i * 33.3,
                                        tracePreTrigger: i * 25.555,
                                        tracePostTrigger: i * 47.354,
                                        alarmValue: i * 0.8765,
                                        flatLevel: i == 1 ? null : i * 2.3,
                                        disableLed: i % 2 == 0,
                                        logFlushInterval: i * 13,
                                        guideLine: i % 2 == 0 ? null : "gl" + i,
                                        buildingLevel: "hsdghsgd" + i,
                                        vectorEnabled: i % 2 == 0,
                                        atopEnabled: i + 1 % 2 == 0,
                                        vtopEnabled: i % 2 == 0);

                SensorDto? sensor = null;
                if (numMonitors == 1 || i % 2 == 0 || alwaysMakeSensor)
                {

                    DateTime? dt = i == 1 ? DateTime.Parse("2023-11-14T11:19:00Z").ToUniversalTime() : null;
                    int? batteryCharge = batteryLevel != null ? batteryLevel : i == 1 ? i * 3 : null;
                    string? connectedUsing = i == 1 ? "foo" : null;
                    sensor = new SensorDto(serialId: serialId, name: "Sensor" + i, lastseen: dt, batteryCharge: batteryCharge,
                                           connectedUsing: connectedUsing, online: i % 2 == 0);
                }

                var m = new VibrationMonitorDto(
                                        id: monitorId,
                                        listedAtTime: DateTime.UtcNow,
                                        lastDataTime: null,
                                        serialId: serialId,
                                        model: "test-model" + i,
                                        firmwareVersion: "test-firmware" + i,
                                        manufacturer: "test-manufacturer" + i,
                                        fleetNr: "test-fleetNr" + i,
                                        latitude: i * 22.222f,
                                        longitude: i * 3.333f,
                                        address: null,
                                        timeZone: timeZone,
                                        customerDisplayName: "test-cn" + i,
                                        monitorStatus: monitorStatus,
                                        sensor: sensor,
                                        offline: false,
                                        batteryStatus: batteryStatus,
                                        lastSeen: null,
                                        deployDate: SampleDeployDate)
                {
                    LastDataTime = lastDataTime
                };
                monitors.Add(m);


            }
            return monitors;
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
            var rules = new List<RvtAlertRuleDto>
            {
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
            };

            return rules;
        }

        public static PeakRecords CreateDeviceMeasurement(DateTime timestamp, double fdom, double vtop, double vtopOverflow)
        {
            var peakRecords = JsonSerializer.Deserialize<PeakRecords>(PeakRecordsJson());

            foreach (var sample in peakRecords!.Samples!)
            {
                sample!.X!.Fdom = fdom;
                sample!.X!.Vtop = vtop;
                sample!.X!.VtopOverflow = vtopOverflow;

                sample!.Y!.Fdom = fdom;
                sample!.Y!.Vtop = vtop;
                sample!.Y!.VtopOverflow = vtopOverflow;

                sample!.Z!.Fdom = fdom;
                sample!.Z!.Vtop = vtop;
                sample!.Z!.VtopOverflow = vtopOverflow;

                sample.Timestamp = DateTimeUtil.GetMillis(timestamp);

            }
            return peakRecords;
        }

        public static List<RvtContactDto> AlertContacts(TimeSpan? sendStartTime = null, TimeSpan? sendEndTime = null)
        {
            return new List<RvtContactDto>()
            {
                new RvtContactDto(ContactMethod.Email, "baz@bob.org", (string?)null,true,false, sendStartTime, sendEndTime)
            };
        }

        public static List<NotificationDto> Notifications(Guid monitorId, DateTime notificationTime, AlertType alertType)
        {
            return new List<NotificationDto>
            {
                new NotificationDto(id: Guid.NewGuid(),
                                    notificationTime: notificationTime,
                                    limitOn: 10,
                                    averagingPeriod:0,
                                    level: 11.123,
                                    closedTime: null,
                                    closedByUser:null,
                                    alertType: alertType,
                                    alertField: "test",
                                    monitorId: monitorId)


            };
        }


        public static string AlertTypeJson(AlertType alertType)
        {
            switch (alertType)
            {
                case AlertType.Ignore:
                    return TestUtil.ReadTextFromFile("testdata/alarm_ignore.json");
                case AlertType.Alert:
                    return TestUtil.ReadTextFromFile("testdata/alarm_alert.json");
                case AlertType.Caution:
                    return TestUtil.ReadTextFromFile("testdata/alarm_caution.json");

                default:
                    throw new Exception("No json for alert type");
            }
        }

        public static string AlertTypeHash(AlertType alertType)
        {
            switch (alertType)
            {
                case AlertType.Ignore:
                case AlertType.Caution:
                case AlertType.Alert:
                    return GetHash(AlertTypeJson(alertType), RvtConfig.WEBHOOK_SECRET);
                //HE local test values dell.
                //case AlertType.Ignore:
                //    return "sha256=b12da2afdc5388e076c86fcec072d18d82098c9ef74015849a62b4bbf4303d9b";
                //case AlertType.Caution:
                //    return "sha256=129937183b46c41f4cd26eadac8ae4894cd70e53d2f20c195bf74d3f3704f21f";
                //case AlertType.Alert:
                //    return "sha256=a62c52352b8bed44c5bce2bc7cdbd06a5f9aca3fb2e73eeeacfc6356190aac18";
                //HE local test values mac.
                //case AlertType.Ignore:
                //    return "sha256=28767e6c5956281dfc01efec3732c8ecb7e9c048868deb5b15c98654050ecade";
                //case AlertType.Caution:
                //    return "sha256=34bde0d96cbbf4605a3a8af01594ba5f917bdb4a9f983c5667f2cccb60ebdd0e";
                //case AlertType.Alert:
                //    return "sha256=af90524266dd590514d0bbe51486db20d50d4c55caee282bfd774f43250d54b3";
                //case AlertType.Ignore:
                //    return "sha256=b12da2afdc5388e076c86fcec072d18d82098c9ef74015849a62b4bbf4303d9b";
                //case AlertType.Caution:
                //    return "sha256=129937183b46c41f4cd26eadac8ae4894cd70e53d2f20c195bf74d3f3704f21f";
                //case AlertType.Alert:
                //    return "sha256=a62c52352b8bed44c5bce2bc7cdbd06a5f9aca3fb2e73eeeacfc6356190aac18";
                default:
                    throw new Exception("No hash for alert type");
            }
        }

        private static string GetHash(string text, string key)
        {
            var textBytes = Encoding.UTF8.GetBytes(text);
            var keyBytes = Encoding.UTF8.GetBytes(key);
            using var hash = new HMACSHA256(keyBytes);
            return string.Format("sha256={0}", BitConverter.ToString(hash.ComputeHash(textBytes)).Replace("-", "").ToLower());
        }
    }
}
