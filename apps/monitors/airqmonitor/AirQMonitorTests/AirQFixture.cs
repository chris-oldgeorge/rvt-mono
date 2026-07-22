using System.Text.Json;
using AirQ.Model.Dto;
using AirQ.Model.Http;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace AirQMonitorTests
{

    // Summary: Provides deterministic AirQ monitor, sample, rule, and contact fixtures.
    // Major updates:
    // - 2026-06-18 Test stability: added fixed sample timestamps and local activity windows for deterministic rule tests.
    public sealed class AirQFixture
    {
        public static readonly DateTime BeforeSampleData = DateTime.Parse("2023-09-18T11:00:00Z").ToUniversalTime();

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

        public static List<InstrumentResponse> ResponseObjects()
        {
            return new List<InstrumentResponse>(){
                new() {
                    InstrumentID = "E1012",
                    Name = "iDB",
                    Type = "E",
                    Location = "209732|R12823V|SE18 7DQ",
                    City = "Kent",
                    Country = "United Kingdom",
                    Latitude = "51.45157",
                    Longitude = "0.21820",
                    Ip = "10.219.229.213",
                    Port = 10002,
                    TimeZone =  "Europe/London",
                    Status = NoiseMonitorStatus.ACTIVE
                },
                new() {
                    InstrumentID = "SomeId",
                    Name = "SomeName",
                    Type = "SomeType",
                    Location = "SomeLocation",
                    City = "SomeCity",
                    Country = "SomeCountry",
                    Latitude = "12.34567",
                    Longitude = "67.54321",
                    Ip = "123.45.67.89",
                    Port = 9999,
                    TimeZone =  "Some/TimeZone",
                    Status = "SomeStatus"
                }};
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
                    sample.Utc = st;//.ToUniversalTime();
                    sample.Timestamp = st.ToLocalTime();
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
                start = DateTime.Today + DateTimeUtil.UtcToLocal(((DateTime)start).ToUniversalTime().TimeOfDay);
            }
            if (end != null)
            {
                end = DateTime.Today + DateTimeUtil.UtcToLocal(((DateTime)end).ToUniversalTime().TimeOfDay);
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


