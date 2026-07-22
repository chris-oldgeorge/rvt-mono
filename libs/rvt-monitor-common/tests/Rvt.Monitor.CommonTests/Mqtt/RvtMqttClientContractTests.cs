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
        var method = typeof(IMqttClient).GetMethod(methodName);
        Assert.IsNotNull(method);

        var parameters = method.GetParameters();
        Assert.IsGreaterThan(0, parameters.Length);
        var cancellationToken = parameters[^1];
        Assert.AreEqual(typeof(CancellationToken), cancellationToken.ParameterType);
        Assert.IsTrue(cancellationToken.IsOptional);
    }
}
