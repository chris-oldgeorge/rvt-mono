using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Rules;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Db.EntityFramework;
using Svantek.Api.Http;
namespace SvantekMonitorTests
{

    public sealed class TestUtil
    {
        public static void UseTestMonitorContextFactory(IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Singleton<IMonitorDbContextFactory<SvantekMonitorContext>>(
                new SvantekMonitorContextFactory(
                    "Host=localhost;Database=svantek-tests;Username=svantek-tests;Password=svantek-tests",
                    new MonitorDbOptions(
                        new Dictionary<string, string>()))));
        }

        public static SvantekApi CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                         out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient, bool testLocal = false)
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
            return new SvantekApi(httpClient.Object, dbClient.Object, messageClient.Object, "test-api-key", testLocal);
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
