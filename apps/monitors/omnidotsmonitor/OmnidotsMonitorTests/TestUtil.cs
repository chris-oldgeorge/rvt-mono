using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Omnidots.Api;
using Omnidots.Api.Db;
using Omnidots.Api.Db.EntityFramework;
using Omnidots.Api.Http;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using AlertActivityTimeDto = Rvt.Monitor.Common.Notifications.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Notifications.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Notifications.RvtContactDto;
namespace OmnidotsAdapterTests
{

    public sealed class TestUtil
    {
        public static void UseTestMonitorContextFactory(IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Singleton<IMonitorDbContextFactory<OmnidotsMonitorContext>>(
                new OmnidotsMonitorContextFactory(
                    "Host=localhost;Database=omnidots-tests;Username=omnidots-tests;Password=omnidots-tests",
                    new MonitorDbOptions(
                        MonitorDatabaseProvider.PostgreSql,
                        new Dictionary<string, string>()))));
        }

        public static OmnidotsApi CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                         out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageClient,
                                         bool testLocal = false,
                                         OmnidotsTraceCollectionOptions? traceCollectionOptions = null)
        {
            return CreateApiAndMocks(
                out httpClient,
                out dbClient,
                out mqttClient,
                out messageClient,
                out _,
                out _,
                testLocal,
                traceCollectionOptions);
        }

        public static OmnidotsApi CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                         out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageClient,
                                         out Mock<IOmnidotsImportCursorQueries> cursorQueries,
                                         out Mock<IOmnidotsMeasurementImportCommands> importCommands,
                                         bool testLocal = false,
                                         OmnidotsTraceCollectionOptions? traceCollectionOptions = null)
        {
            httpClient = new Mock<IHttpClient>();
            dbClient = new Mock<IDBClient>();
            cursorQueries = dbClient.As<IOmnidotsImportCursorQueries>();
            importCommands = dbClient.As<IOmnidotsMeasurementImportCommands>();
            var traceQueries = dbClient.As<IOmnidotsTraceQueries>();
            mqttClient = new Mock<IMqttClient>();
            messageClient = new Mock<IMessageService>();
            return new OmnidotsApi(
                httpClient.Object,
                dbClient.Object,
                cursorQueries.Object,
                importCommands.Object,
                traceQueries.Object,
                mqttClient.Object,
                messageClient.Object,
                testLocal,
                new OmnidotsMonitoringOptions(),
                Mock.Of<IOmnidotsMonitoringNotifier>(),
                traceCollectionOptions ?? new OmnidotsTraceCollectionOptions
                {
                    AllowedSerialIds = ["23423"],
                    MaxMonitorsPerRun = int.MaxValue
                },
                TimeProvider.System);
        }


        public static void AssertDateTimeEqual(DateTime expected, DateTime actual)
        {
            Assert.AreEqual(actual.Year, expected.Year);
            Assert.AreEqual(actual.Month, expected.Month);
            Assert.AreEqual(actual.Day, expected.Day);
            Assert.AreEqual(actual.Hour, expected.Hour);
            Assert.AreEqual(actual.Minute, expected.Minute);
            Assert.AreEqual(actual.Second, expected.Second);

        }

        public static string ReadTextFromFile(string fileName)
        {
            try
            {
                using var sr = new StreamReader(fileName);
                var txt = sr.ReadToEnd();
                Console.WriteLine(txt);
                return txt;
            }
            catch (IOException e)
            {
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
                throw AdapterException.Of("Could not read file=" + fileName, e);
            }
        }

        private static async Task<string> ReadContent(MultipartFormDataContent content)
        {
            return await content.ReadAsStringAsync();
        }

        public static bool VerifyAuthenticateForm(HttpContent httpContent)
        {

            if (httpContent is MultipartFormDataContent)
            {
                var mfc = (MultipartFormDataContent)httpContent;
                var s = ReadContent(mfc).Result;

                if (!s.Contains(
                    string.Format("form-data; name=\"username\"\r\n\r\n{0}", RvtConfig.USER_ID)))
                {
                    return false;
                }

                if (!s.Contains(
                    string.Format("form-data; name=\"password\"\r\n\r\n{0}", RvtConfig.USER_AUTH)))
                {
                    return false;
                }
                return true;
            }

            return false;
        }

        private static bool VerifyConfigRequest(HttpContent httpContent)
        {
            // todo check request
            return true;
            //if (httpContent is MultipartFormDataContent)
            //{
            //    var mfc = (MultipartFormDataContent)httpContent;
            //    var s = ReadContent(mfc).Result;

            //    if (!s.Contains(
            //        string.Format("form-data; name=\"username\"\r\n\r\n{0}", RvtConfig.USER_ID)))
            //    {
            //        return false;
            //    }

            //    if (!s.Contains(
            //        string.Format("form-data; name=\"password\"\r\n\r\n{0}", RvtConfig.USER_AUTH)))
            //    {
            //        return false;
            //    }
            //    return true;
            //}

            //return false;
        }


        public static bool VerifyAlertRuleDto(RvtAlertRuleDto dto, string serialNumber, string field, bool triggered)
        {

            if (!serialNumber.Equals(dto.SerialId))
            {
                return false;
            }

            if (!field.Equals(dto.Field))
            {
                return false;
            }

            if (triggered != dto.IsActive)
            {
                return false;
            }
            return true;
        }

        public static bool VerifyNotificationDto(NotificationDto dto, RvtAlertRuleDto rule, double alertLevel,
                                                         DateTime notificationTime, int averagingPeriod, double limitOn)
        {

            if (notificationTime != dto.NotificationTime)
            {
                return false;
            }

            if (alertLevel != dto.Level)
            {
                return false;
            }

            if (averagingPeriod != dto.AveragingPeriod)
            {
                return false;
            }

            if (limitOn != dto.LimitOn)
            {
                return false;
            }

            if (dto.AlertType != rule.AlertType ||
                !dto.AlertField.Equals(rule.Field))
            {
                return false;
            }

            return true;
        }

        internal static bool VerifyMonitorList(List<VibrationMonitorDto> dtos, int expectedCount)
        {
            return dtos.Count == expectedCount;
        }

        public static bool AreEqual(VibrationMonitorStatusDto expected, VibrationMonitorStatusDto actual)
        {
            if (expected.VdvEnabled != actual.VdvEnabled ||
                expected.VdvX != actual.VdvX ||
                expected.VdvY != actual.VdvY ||
                expected.VdvZ != actual.VdvZ ||
                expected.VdvPeriod != actual.VdvPeriod)
            {
                return false;
            }

            if (!expected.SerialId.Equals(actual.SerialId) ||
                expected.MeasurementDuration != actual.MeasurementDuration ||
                expected.DataSaveLevel != actual.DataSaveLevel)
            {
                return false;
            }

            if (expected.TraceSaveLevel != actual.TraceSaveLevel ||
                expected.TracePreTrigger != actual.TracePreTrigger ||
                expected.TracePostTrigger != actual.TracePostTrigger ||
                expected.AlarmValue != actual.AlarmValue ||
                expected.FlatLevel != actual.FlatLevel ||
                expected.DisableLed != actual.DisableLed ||
                expected.LogFlushInterval != actual.LogFlushInterval ||
                expected.GuideLine != actual.GuideLine ||
                expected.BuildingLevel != actual.BuildingLevel ||
                expected.VectorEnabled != actual.VectorEnabled ||
                expected.AtopEnabled != actual.AtopEnabled ||
                expected.VtopEnabled != actual.VtopEnabled)
            {
                return false;
            }
            return true;
        }

        public static bool AreEqual(List<VibrationMonitorDto> expected, List<VibrationMonitorDto> actual)
        {
            if (expected.Count != actual.Count)
                return false;

            for (var i = 0; i < expected.Count; i++)
            {
                var a = actual[i];
                var e = expected[i];
                if (a.ListedAtTime < e.ListedAtTime.AddMinutes(-2) ||
                    a.ListedAtTime > e.ListedAtTime.AddMinutes(2))
                {
                    return false;
                }

                if (a.Address != e.Address)
                {
                    return false;
                }

                if (!a.SerialId!.Equals(e.SerialId) ||
                    !a.Model!.Equals(e.Model) ||
                    a.Latitude != e.Latitude ||
                    a.Longitude != e.Longitude ||

                    !a.TimeZone!.Equals(e.TimeZone) ||
                    !a.CustomerDisplayName!.Equals(e.CustomerDisplayName) ||
                    !a.FirmwareVersion!.Equals(e.FirmwareVersion) ||
                    !e.Manufacturer.Equals(a.Manufacturer))
                {
                    return false;
                }

                if (!AreEqual(e.MonitorStatus, a.MonitorStatus))
                {
                    return false;
                }

                if (!AreEqual(e.Sensor, a.Sensor))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool VerifyPeakRecordDtos(List<PeakRecordDto> dtos, int addMinutes, DateTime expectedStartTime,
                                               double fdom, double vtop, double vtopOverflow)
        {
            var dto = dtos[0];
            var expectedSampleTime = expectedStartTime.AddMinutes(addMinutes);
            if (!expectedSampleTime.Equals(dto.SampleTime))
            {
                return false;
            }
            if (dto.X!.Fdom != fdom ||
                dto.Y!.Fdom != fdom ||
                dto.X!.Fdom != fdom)
            {
                return false;
            }

            if (dto.X!.Vtop != vtop ||
                dto.Y!.Vtop != vtop ||
                dto.X!.Vtop != vtop)
            {
                return false;
            }

            if (dto.X!.VtopOverflow != vtopOverflow ||
               dto.Y!.VtopOverflow != vtopOverflow ||
               dto.X!.VtopOverflow != vtopOverflow)
            {
                return false;
            }
            return true;
        }

        public static bool VerifyPeakRecordTable(DataTable table, DateTime expectedSampleTime,
                                                double fdom, double vtop, double vtopOverflow)
        {
            if (table.Rows.Count != 1)
            {
                return false;
            }

            var row = table.Rows[0];
            return expectedSampleTime.Equals((DateTime)row["SampleTime"]) &&
                   fdom.Equals((double)row["XFdom"]) &&
                   fdom.Equals((double)row["YFdom"]) &&
                   fdom.Equals((double)row["ZFdom"]) &&
                   vtop.Equals((double)row["XVtop"]) &&
                   vtop.Equals((double)row["YVtop"]) &&
                   vtop.Equals((double)row["ZVtop"]) &&
                   vtopOverflow.Equals((double)row["XVtopOverflow"]) &&
                   vtopOverflow.Equals((double)row["YVtopOverflow"]) &&
                   vtopOverflow.Equals((double)row["ZVtopOverflow"]);
        }

        internal static bool AreEqual(SensorDto? actual, SensorDto? expected)
        {
            if (actual == null && expected == null)
            {
                return true;
            }

            if (!actual!.Name.Equals(expected!.Name))
            {
                return false;
            }

            if (actual.Lastseen != expected.Lastseen)
            {
                return false;
            }
            if (actual.BatteryCharge != expected.BatteryCharge)
            {
                return false;
            }
            if (actual.ConnectedUsing != expected.ConnectedUsing)
            {
                return false;
            }
            if (actual.Online != expected.Online)
            {
                return false;
            }
            return true;
        }

        internal static bool VerifyDateTime(DateTime expected, DateTime actual)
        {

            var e = expected.Ticks / TimeSpan.TicksPerSecond;
            var a = actual.Ticks / TimeSpan.TicksPerSecond;

            return e == a;
        }
    }
}
