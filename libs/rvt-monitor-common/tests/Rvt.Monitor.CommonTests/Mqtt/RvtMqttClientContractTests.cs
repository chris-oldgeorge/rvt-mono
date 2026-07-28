using System.Reflection;
using Rvt.Monitor.Common.Mqtt;

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
