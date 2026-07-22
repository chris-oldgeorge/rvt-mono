using Moq;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Model.Dto;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace SvantekMonitorTests
{

    public sealed class TestUtil
    {

        public static SvantekApi CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                         out Mock<IMqttClient> mqttClient, out Mock<IMessageService> emailClient, bool testLocal = false)
        {
            httpClient = new Mock<IHttpClient>();
            dbClient = new Mock<IDBClient>();
            mqttClient = new Mock<IMqttClient>();
            emailClient = new Mock<IMessageService>();
            return new SvantekApi(httpClient.Object, dbClient.Object, mqttClient.Object, emailClient.Object, "test-api-key", testLocal);
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


        public static bool AreEqual(List<NoiseMonitorDto> expected, List<NoiseMonitorDto> actual)
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
                if (!a.SerialId!.Equals(e.SerialId) ||
                    !a.Model!.Equals(e.Model) ||
                    a.Latitude != e.Latitude ||
                    a.Longitude != e.Longitude ||
                    !a.Address!.Equals(e.Address) ||
                    !a.TimeZone!.Equals(e.TimeZone) ||
                    !a.CustomerDisplayName!.Equals(e.CustomerDisplayName) ||
                    !a.FirmwareVersion!.Equals(e.FirmwareVersion) ||
                    !"Turnkey".Equals(a.Manufacturer))
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
    }

}
