using Microsoft.Extensions.DependencyInjection;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.IntegrationTesting;
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
            MonitorTestUtil.UseTestMonitorContextFactory(
                services,
                new SvantekMonitorContextFactory(
                    "Host=localhost;Database=svantek-tests;Username=svantek-tests;Password=svantek-tests",
                    new MonitorDbOptions(
                        new Dictionary<string, string>())));
        }

        public static SvantekApi CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                         out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient, bool testLocal = false)
        {
            httpClient = new Mock<IHttpClient>();
            dbClient = new Mock<IDBClient>();
            mqttClient = new Mock<IMqttClient>();
            messageClient = MonitorTestUtil.CreateAcceptingAlertIngress();
            return new SvantekApi(httpClient.Object, dbClient.Object, messageClient.Object, "test-api-key", testLocal);
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
