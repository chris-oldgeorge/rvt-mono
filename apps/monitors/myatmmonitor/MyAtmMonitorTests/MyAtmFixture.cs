using System.Text.Json;
using MyAtm.Api;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using MyAtm.Model.Json.Customer;
using MyAtm.Model.Json.DeviceInfo;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;


using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace MyAtmMonitorTests
{

    public sealed class MyAtmFixture
    {
        public static string DevicesResponseJson()
        {
            return TestUtil.ReadTextFromFile("testdata/devices.json");
        }

        public static string DeviceInfoResponseJson(string serialNumber)
        {
            var json = TestUtil.ReadTextFromFile("testdata/device_info.json");
            var deviceInfo = JsonSerializer.Deserialize<DustMonitorInfo>(json);
            deviceInfo!.SerialNumber = serialNumber;
            return JsonSerializer.Serialize(deviceInfo);
        }

        public static string AccessoryResponseJson()
        {
            return TestUtil.ReadTextFromFile("testdata/accessory.json");
        }

        public static List<RvtContactDto> AlertContacts(TimeSpan? sendStartTime = null, TimeSpan? sendEndTime = null)
        {
            return new List<RvtContactDto>()
            {
                new RvtContactDto(ContactMethod.Email, "baz@bob.org", (string?)null,true,false, sendStartTime, sendEndTime)
            };
        }

        public static List<DustMonitorDto> CustomerDeviceDtos(DateTime? lastDataTime, bool singleItem = false)
        {
            var json = DevicesResponseJson();
            var devices = JsonSerializer.Deserialize<List<DustMonitor>>(json)!;
            var dtos = new List<DustMonitorDto>();
            foreach (var device in devices)
            {
                var deviceJson = DeviceInfoResponseJson(device.SerialNumber!);
                var deviceInfo = JsonSerializer.Deserialize<DustMonitorInfo>(deviceJson)!;
                var dto = new DustMonitorDto(deviceInfo)
                {
                    LastDataTime1Min = lastDataTime,
                    FleetNr = "Fnr" + device.SerialNumber!.ToString(), //Instead fo fleetNr using the serial
                };
                dtos.Add(dto);
                if (singleItem)
                {
                    break;
                }
            }
            return dtos;

        }

        public static string MeasurementsResponseJson(Period period)
        {

            switch (period)
            {
                case Period.Minutes1:
                    return TestUtil.ReadTextFromFile("testdata/measurements.json");
                case Period.Minutes15:
                case Period.Hours1:
                case Period.Hours8:
                case Period.Hours24:
                    return TestUtil.ReadTextFromFile("testdata/measurements_avg.json");
                default:
                    throw AdapterException.Of("MeasurementsResponseJson Unknown Period " + period);
            }
            ;
        }

        public static AvgDeviceMeasurement CreateAvgDeviceMeasurement(DateTime timestamp, double pm1, double pm2_5, double pm10)
        {

            return new AvgDeviceMeasurement
            {
                Avrg = 60,
                Timestamp = timestamp,
                Pm1 = new AvgVal
                {
                    Avg = pm1
                },
                Pm2_5 = new AvgVal
                {
                    Avg = pm2_5
                },
                Pm10 = new AvgVal
                {
                    Avg = pm10
                },
                PmTotal = new AvgVal
                {
                    Avg = pm1 + pm2_5 + pm10
                },
                Weather_t = new AvgVal
                {
                    Avg = 20.83439
                },
                Weather_p = new AvgVal
                {
                    Avg = 1020
                },
                Weather_rh = new AvgVal
                {
                    Avg = 59.61567
                }
            };
        }

        public static DeviceMeasurement CreateDeviceMeasurement(DateTime timestamp, double pm1, double pm2_5, double pm10)
        {

            return new DeviceMeasurement
            {
                Avrg = 60,
                Timestamp = timestamp,
                Pm1 = pm1,
                Pm2_5 = pm2_5,
                Pm10 = pm10,
                PmTotal = pm1 + pm2_5 + pm10,
                Weather_t = 20.83439,
                Weather_p = 1020,
                Weather_rh = 59.61567
            };
        }

        public static string MeasurementsResponseJson(int numMeasuements, DateTime startTime, double startLevel = 1.0, double levelInc = 0.5)
        {

            var measurements = new List<DeviceMeasurement>();
            for (var i = 0; i < numMeasuements; i++)
            {
                var level = startLevel + (levelInc * i);
                measurements.Insert(0,
                    CreateDeviceMeasurement(startTime.AddMinutes(i), level * 4, level * 2, level));
            }
            return JsonSerializer.Serialize(measurements);
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
    }
}
