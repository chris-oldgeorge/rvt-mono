using Rvt.Monitor.Common.Mqtt;

namespace Rvt.Monitor.CommonTests.Mqtt;

/// <summary>
/// The MQTT client used to read its broker settings from static configuration
/// at the point of use, which hid the dependency and left the connection
/// untestable without process-wide state. Settings are supplied explicitly now.
/// </summary>
[TestClass]
public sealed class MqttOptionsTests
{
    [TestMethod]
    public void HasClientCertificate_RequiresBothHalvesOfThePair()
    {
        Assert.IsTrue(new MqttOptions
        {
            CertificatePath = "/certs/client.pem",
            PrivateKeyPath = "/certs/client.key",
        }.HasClientCertificate);
    }

    [TestMethod]
    [DataRow("", "")]
    [DataRow("/certs/client.pem", "")]
    [DataRow("", "/certs/client.key")]
    [DataRow("   ", "   ")]
    public void HasClientCertificate_IsFalseWhenEitherHalfIsMissing(
        string certificatePath,
        string privateKeyPath)
    {
        Assert.IsFalse(new MqttOptions
        {
            CertificatePath = certificatePath,
            PrivateKeyPath = privateKeyPath,
        }.HasClientCertificate);
    }

    [TestMethod]
    public void Defaults_AreDisabledAndUseTheBrokerTlsPort()
    {
        var options = new MqttOptions();

        Assert.IsFalse(options.Enabled);
        Assert.AreEqual(8883, options.Port);
    }

    [TestMethod]
    public void FromRvtConfig_ProducesOptionsWithoutThrowing()
    {
        // Preserves the historical environment contract for composition roots.
        var options = MqttOptions.FromRvtConfig();

        Assert.IsNotNull(options);
        Assert.AreEqual(8883, options.Port);
    }

    [TestMethod]
    public void Client_AcceptsExplicitOptionsWithoutTouchingStaticConfiguration()
    {
        var options = new MqttOptions { Enabled = false, Hostname = "broker.example.test" };

        using var client = new RvtMqttClient(options);

        Assert.IsNotNull(client);
    }

    [TestMethod]
    public async Task PublishAsync_WhenDisabled_IsANoOp()
    {
        using var client = new RvtMqttClient(new MqttOptions { Enabled = false });

        await client.PublishAsync("rvt/noise/inserted", "{}");
    }

    [TestMethod]
    public async Task ConnectAsync_WhenDisabled_ReportsSuccessWithoutConnecting()
    {
        using var client = new RvtMqttClient(new MqttOptions { Enabled = false });

        Assert.IsTrue(await client.ConnectAsync());
    }

    [TestMethod]
    public void Client_WithoutOptions_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new RvtMqttClient(null!));
    }
}
