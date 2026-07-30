using System.Reflection;
using MQTTnet.Client;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using IMqttClient = Rvt.Monitor.Common.Mqtt.IMqttClient;

namespace Rvt.Monitor.CommonTests.Mqtt;

[TestClass]
public sealed class RvtMqttClientContractTests
{
    [TestMethod]
    public void PublishAsync_EndsWithAnOptionalCancellationToken()
    {
        AssertEndsWithOptionalCancellationToken(nameof(IMqttClient.PublishAsync));
    }

    [TestMethod]
    public void ConnectAsync_EndsWithAnOptionalCancellationToken()
    {
        AssertEndsWithOptionalCancellationToken(nameof(IMqttClient.ConnectAsync));
    }

    [TestMethod]
    public void EnsurePublishAccepted_Success_DoesNotThrow()
    {
        RvtMqttClient.EnsurePublishAccepted(MqttClientPublishReasonCode.Success);
    }

    [TestMethod]
    [DataRow(MqttClientPublishReasonCode.NoMatchingSubscribers)]
    [DataRow(MqttClientPublishReasonCode.UnspecifiedError)]
    [DataRow(MqttClientPublishReasonCode.NotAuthorized)]
    [DataRow(MqttClientPublishReasonCode.QuotaExceeded)]
    [DataRow(MqttClientPublishReasonCode.TopicNameInvalid)]
    public void EnsurePublishAccepted_NonSuccessReasonCode_Throws(MqttClientPublishReasonCode reasonCode)
    {
        AdapterException exception = Assert.ThrowsExactly<AdapterException>(
            () => RvtMqttClient.EnsurePublishAccepted(reasonCode));

        Assert.Contains(reasonCode.ToString(), exception.Message, StringComparison.Ordinal);
    }

    private static void AssertEndsWithOptionalCancellationToken(string methodName)
    {
        MethodInfo? method = typeof(IMqttClient).GetMethod(methodName);
        Assert.IsNotNull(method);

        ParameterInfo[] parameters = method.GetParameters();
        Assert.IsGreaterThan(0, parameters.Length);
        ParameterInfo cancellationToken = parameters[^1];
        Assert.AreEqual(typeof(CancellationToken), cancellationToken.ParameterType);
        Assert.IsTrue(cancellationToken.IsOptional);
    }
}
