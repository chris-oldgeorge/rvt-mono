using System.Text.Json;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Model.Dto;
using Svantek.Model.Http;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace SvantekMonitorTests
{

    public sealed class SvantekFixture
    {
        public static string InstrumentsResponseJson()
        {
            return TestUtil.ReadTextFromFile("testdata/devices.json"); ;
        }

        public static string MetaDataResponseJson()
        {
            return TestUtil.ReadTextFromFile("testdata/latest_metadata.json"); ;
        }

        public static string TooManyRequestsJson()
        {
            return @"[{""Response"": ""Too many requests!""}]";
        }

        public static string SamplesResponseJson()
        {
            return TestUtil.ReadTextFromFile("testdata/latest_samples.json");
        }

        public static List<SampleResponse> SamplesResponseObjects(DateTime? sampleTimeUtc = null)
        {

            var json = SamplesResponseJson();
            var samples = JsonSerializer.Deserialize<List<SampleResponse>>(json);

            if (sampleTimeUtc != null)
            {
                var st = (DateTime)sampleTimeUtc!;
                foreach (var sample in samples!)
                {
                    sample.Utc = st;
                    sample.Timestamp = st;
                }
            }
            return samples!;
        }

        public static string DateSamplesResponseJson()
        {
            return TestUtil.ReadTextFromFile("testdata/date_samples.json");
        }

        public static List<NoiseMonitorDto> MonitorDtos(DateTime? lastDataTime, string activityStatus, int errorCount = 0)
        {
            var caiibrationDate = DateTime.UtcNow.AddDays(-7);
            var filterChangeDate = DateTime.UtcNow;
            return new List<NoiseMonitorDto>(){
                new NoiseMonitorDto (
                      id: Guid.NewGuid(),
                      listedAtTime: DateTime.UtcNow,
                      lastDataTime: lastDataTime,
                      serialId: "Device1",
                      model: "TestName",
                      firmwareVersion: "Unknown",
                      manufacturer: "Turnkey",
                      fleetNr:"123",
                      latitude: 51.2500f,
                      longitude: 0.75000f,
                      address: "209732|R12823V|SE18 1DQ, Kent, United Kingdom",
                      timeZone: "Europe/London",
                      customerDisplayName: "E",
                      offline: false,
                      monitorStatus: new NoiseMonitorStatus(DateTime.UtcNow, status:activityStatus, errorCount:errorCount,
                                                            batteryVoltage: "1.11 volts", calibrationDate:caiibrationDate,
                                                            filterChangeDate:filterChangeDate, pumpHours:"1 hours"))
                , new NoiseMonitorDto (
                      id: Guid.NewGuid(),
                      listedAtTime: DateTime.UtcNow,
                      lastDataTime: lastDataTime,
                      serialId: "Device2",
                      model: "TestName",
                      firmwareVersion: "Unknown",
                      manufacturer: "Turnkey",
                      fleetNr:"123",
                      latitude: 51.2500f,
                      longitude: 0.75000f,
                      address: "209732|R12823V|SE18 2DQ, Kent, United Kingdom",
                      timeZone: "Europe/London",
                      customerDisplayName: "E",
                      offline: false,
                      monitorStatus: new NoiseMonitorStatus(DateTime.UtcNow, status:activityStatus, errorCount:errorCount,
                                                            batteryVoltage: "2.22 volts", calibrationDate:caiibrationDate,
                                                            filterChangeDate:filterChangeDate, pumpHours:"2 hours"))
               , new NoiseMonitorDto (
                      id: Guid.NewGuid(),
                      listedAtTime: DateTime.UtcNow,
                      lastDataTime: lastDataTime,
                      serialId: "Device3",
                      model: "TestName",
                      firmwareVersion: "Unknown",
                      manufacturer: "Turnkey",
                      fleetNr:"123",
                      latitude: 51.2500f,
                      longitude: 0.75000f,
                      address: "209732|R12823V|SE18 3DQ, Kent, United Kingdom",
                      timeZone: "Europe/London",
                      customerDisplayName: "E",
                      offline: false,
                      monitorStatus: new NoiseMonitorStatus(DateTime.UtcNow, status:activityStatus, errorCount:errorCount,
                                                            batteryVoltage: "3.33 volts" , calibrationDate:caiibrationDate,
                                                            filterChangeDate:filterChangeDate, pumpHours:"3 hours"))
            };
        }

        public static List<NoiseMonitorDto> SingleActiveMonitorDto(string serialId, DateTime? lastDataTime)
        {
            return new List<NoiseMonitorDto>(){
                new NoiseMonitorDto (
                      id: Guid.NewGuid(),
                      listedAtTime: DateTime.UtcNow,
                      lastDataTime: lastDataTime,
                      serialId: serialId,
                      model: "E",
                      firmwareVersion: "Unknown",
                      manufacturer: "Turnkey",
                      fleetNr:"123",
                      latitude: 51.2500f,
                      longitude: 0.75000f,
                      address: "209732|R12823V|SE18 1DQ, Kent, United Kingdom",
                      timeZone: "Europe/London",
                      customerDisplayName: "iDB",
                      offline: false,
                      monitorStatus: new NoiseMonitorStatus(DateTime.UtcNow, status:"Active", errorCount:0,
                                                            batteryVoltage:"1.23 Volts", calibrationDate:DateTime.UtcNow,
                                                            filterChangeDate:DateTime.UtcNow, pumpHours:"1 hours"))

            };
        }

        public static SampleResponse CreateSampleResponse(DateTime timestamp, string serialId, double value)
        {

            var val = string.Format("{0}", value);
            return new SampleResponse
            {
                Utc = timestamp.ToUniversalTime(),
                Timestamp = timestamp,
                InstrumentID = serialId,
                Location = "Initial Configuration",
                GpsCoordinates = "51.2500, 0.75000",
                Data = new List<SampleData>
                {
                    new SampleData
                    {
                        Unit = "dB",
                        Name = "LAeq(T)",
                        Value = val
                    },
                    new SampleData
                    {
                        Unit = "dB",
                        Name = "LAmax(T)",
                        Value = val
                    },
                    new SampleData
                    {
                        Unit = "dB",
                        Name = "LA90(T)",
                        Value = val
                    },
                    new SampleData
                    {
                        Unit = "dB",
                        Name = "LA10(T)",
                        Value = val
                    },
                    new SampleData
                    {
                        Unit = "dB",
                        Name = "LCeq(T)",
                        Value = val
                    },
                    new SampleData
                    {
                        Unit = "dB",
                        Name = "LCmax(T)",
                        Value = val
                    },
                    new SampleData
                    {
                        Unit = "dB",
                        Name = "LC90(T)",
                        Value = val
                    },
                    new SampleData
                    {
                        Unit = "dB",
                        Name = "LC10(T)",
                        Value = val
                    }

                }
            };
        }

        public static List<RvtContactDto> AlertContacts(TimeSpan? sendStartTime = null, TimeSpan? sendEndTime = null)
        {
            return new List<RvtContactDto>()
            {
                new RvtContactDto( contactMethod: ContactMethod.Email,
                                   emailAddress: "baz@bob.org",
                                   phoneNumber: (string?)null,
                                   email:true,
                                   sms:false,
                                   sendStartTime: sendStartTime,
                                   sendEndTime: sendEndTime)
            };
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

        internal static List<RvtAlertRuleDto> NotifyRules(string serialId, string field,
                                                          double limitOn)
        {
            var rules = new List<RvtAlertRuleDto>
            {
                new(ruleId: Guid.NewGuid(),
                          serialId: serialId,
                          field: field,
                          limitOn: limitOn,
                          limitOff: limitOn -1,
                          averagingPeriod: 0,
                          ruleActivityTime: new AlertActivityTimeDto
                          {
                              Weekdays = true,
                              Saturdays = true,
                              Sundays = true,
                              StartTime = null,
                              EndTime = null
                          },
                        alertType: AlertType.Caution,
                        isActive: true,
                        isDeleted: false,
                        created: DateTime.UtcNow,
                        accessed: null)
            };

            return rules;
        }
    }
}


