using System.Text.RegularExpressions;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace MyAtmMonitorTests
{

    public sealed class TestUtil
    {
        public static string MEASUREMENT_SELECT = "?$select=avrg,timestamp,pm1,pm2_5,pm10,pm_total,weather_t,weather_p,weather_rh";

        public static string MeasurementPageRequestPattern(
            int customerId,
            string serialId,
            string routeSuffix = "",
            string select = "")
        {
            var path = $"/api/customers/{customerId}/devices/{serialId}/measurements{routeSuffix}{select}";
            var separator = string.IsNullOrEmpty(select) ? "?" : "&";
            return $"^{Regex.Escape(path + separator)}\\$filter=timestamp gt [^&]+&\\$orderby=timestamp asc&\\$top=1000$";
        }

        public static string AccessoryPageRequestPattern(int customerId, string serialId)
        {
            var path = $"/api/customers/{customerId}/devices/{serialId}/measurements/accessory?";
            return $"^{Regex.Escape(path)}\\$filter=timestamp gt [^&]+&\\$orderby=timestamp asc&\\$top=1000$";
        }

        public static MyAtmApi CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageClient,
                                                 bool testLocal = false)
        {
            httpClient = new Mock<IHttpClient>();
            dbClient = new Mock<IDBClient>();
            dbClient.Setup(client => client.CommitDustImportAsync(
                    It.IsAny<MyAtmDustImportCommit>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DustImportCommitResult(Array.Empty<MonitorDeliveryRequest>()));
            dbClient.Setup(client => client.ClaimNextDueAsync(
                    MonitorDeliveryProducers.MyAtm,
                    It.IsAny<DateTime>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MonitorDeliveryMessage?)null);
            dbClient.Setup(client => client.CommitAlertAsync(
                    It.IsAny<MyAtmAlertCommit>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));
            mqttClient = new Mock<IMqttClient>();
            messageClient = new Mock<IMessageService>();
            return new MyAtmApi(httpClient.Object, dbClient.Object, mqttClient.Object, messageClient.Object, testLocal);
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

        public static bool AreEqual(List<DustMonitorDto> expected, List<DustMonitorDto> actual)
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
                if (a.CustomerId != e.CustomerId ||
                    !a.SerialId!.Equals(e.SerialId) ||
                    !a.Model!.Equals(e.Model) ||
                    a.LocationId != e.LocationId ||
                    a.Latitude != e.Latitude ||
                    a.Longitude != e.Longitude ||
                    !a.Address!.Equals(e.Address) ||
                    !a.TimeZone!.Equals(e.TimeZone) ||
                    !a.CustomerDisplayName!.Equals(e.CustomerDisplayName) ||
                    !a.FirmwareVersion!.Equals(e.FirmwareVersion) ||
                    !"Palas GmbH".Equals(a.Manufacturer))
                {
                    return false;
                }
            }

            return true;
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

        //Had to create a new version of this as I couldn't think of a way to predict the notification time for the 8 houir Average.
        public static bool VerifyNotification8hourDto(NotificationDto dto, RvtAlertRuleDto rule, double alertLevel,
                                         DateTime notificationTime, int averagingPeriod, double limitOn)
        {

            //if (notificationTime != dto.NotificationTime)
            //{
            //    return false;
            //}

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
        public static bool VerifyDustDto(DustDto dto, string expectedSerialNumber, int addMinutes, DateTime expectedStartTime)
        {
            var expectedSampleTime = expectedStartTime.AddMinutes(addMinutes);
            if (!expectedSampleTime.Equals(dto.SampleTime))
            {
                return false;
            }
            return VerifyDustDto(dto, expectedSerialNumber);
        }

        public static bool VerifyDustDto(DustDto dto, string expectedSerialNumber)
        {
            if (!expectedSerialNumber.Equals(dto.SerialId))
            {
                return false;
            }
            return true;
        }

        public static bool VerifyContacts(List<RvtContactDto> actual, List<RvtContactDto> expected)
        {
            if (expected.Count != actual.Count)
            {
                return false;
            }

            for (int i = 0; i < actual.Count; i++)
            {
                var a = actual[i];
                var e = expected[i];
                if (
                    a.ContactMethod != e.ContactMethod ||
                    !a.EmailAddress!.Equals(e.EmailAddress))
                {
                    return false;
                }

                if (a.PhoneNumber == null)
                {
                    return e.PhoneNumber == null;
                }
                if (!a.PhoneNumber.Equals(e.PhoneNumber))
                {
                    return false;
                }
            }
            return true;
        }


        internal static bool VerifyMonitorList(List<DustMonitorDto> expected, List<DustMonitorDto> actual)
        {
            foreach (var a in actual)
            {
                var verified = false;
                foreach (var e in expected)
                {
                    if (a.SerialId.Equals(e.SerialId))
                    {
                        verified = VerifyMonitor(e, a);
                        if (!verified)
                        {
                            return false;
                        }
                        break;
                    }
                }
            }
            return true;
        }





        internal static bool VerifyMonitor(DustMonitorDto expected, DustMonitorDto actual)
        {
            return
               expected.CustomerId == actual.CustomerId &&
               expected.FirmwareVersion.Equals(actual.FirmwareVersion) &&
               expected.Id == actual.Id &&
               expected.Latitude == actual.Latitude &&
               expected.Longitude == actual.Longitude &&
               expected.Manufacturer.Equals(actual.Manufacturer) &&
               expected.Model.Equals(actual.Model) &&
               expected.Offline == actual.Offline &&
               expected.SerialId.Equals(actual.SerialId);

        }
    }
}
