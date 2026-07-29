using AirQ.Api;
using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Model.Dto;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Rules;
namespace AirQMonitorTests
{

    public sealed class TestUtil
    {

        public static AirQApi CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                         out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient)
        {
            httpClient = new Mock<IHttpClient>();
            dbClient = new Mock<IDBClient>();
            mqttClient = new Mock<IMqttClient>();
            messageClient = new Mock<IAlertIngressPort>();
            messageClient
                .Setup(ingress => ingress.AcceptAsync(It.IsAny<AlertSignal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AlertIngressResult(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    AlertOccurrenceOutcome.Accepted,
                    IsDuplicate: false));
            return new AirQApi(httpClient.Object, dbClient.Object, mqttClient.Object, messageClient.Object);
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
                using StreamReader sr = new(fileName);
                string txt = sr.ReadToEnd();
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
            {
                return false;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                NoiseMonitorDto a = actual[i];
                NoiseMonitorDto e = expected[i];
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
    }
}
