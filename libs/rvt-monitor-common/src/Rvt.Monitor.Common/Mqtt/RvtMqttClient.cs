using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Certificates;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;


namespace Rvt.Monitor.Common.Mqtt;


// Summary: Publishes RVT monitor events to Event Grid MQTT with client-certificate authentication.
// Major updates:
// - 2026-06-18 Warning remediation: switched to modern certificate loading, TLS options, and valid log templates.
// - 2026-07-12 DI composition: connects lazily on first publish instead of requiring an eager ConnectAsync at startup.
public class RvtMqttClient : IMqttClient, IDisposable
{
    private readonly MQTTnet.Client.IMqttClient _mqttClient;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly Lock _certificateLock = new();
    private readonly MqttOptions _options;
    private X509Certificate2? _clientCertificate;
    private X509Certificate2Collection? _clientCertificates;

    public RvtMqttClient(MqttOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _mqttClient = new MqttFactory().CreateMqttClient();

        Task _mqttClient_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            RvtLogger.Logger.LogInformation("MQTT Connected");
            return Task.CompletedTask;
        }
        ;
        Task _mqttClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            RvtLogger.Logger.LogInformation("MQTT Disconnected");
            return Task.CompletedTask;
        }
        ;

        _mqttClient.ConnectedAsync += _mqttClient_ConnectedAsync;
        _mqttClient.DisconnectedAsync += _mqttClient_DisconnectedAsync;

    }

    public async Task PublishAsync(
        string topic,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            RvtLogger.Logger.LogInformation("MQTT is disabled, not publishing.");
            return;
        }

        await EnsureConnectedAsync(cancellationToken);

        // QoS 1 so the broker acknowledges the publish. At QoS 0 the call
        // returned success for anything the socket accepted, which made the
        // outbox's retry and dead-letter machinery unreachable for MQTT.
        MqttClientPublishResult result = await _mqttClient.PublishStringAsync(
            topic,
            message,
            MqttQualityOfServiceLevel.AtLeastOnce,
            retain: false,
            cancellationToken);
        EnsurePublishAccepted(result.ReasonCode);
    }

    internal static void EnsurePublishAccepted(MqttClientPublishReasonCode reasonCode)
    {
        if (reasonCode != MqttClientPublishReasonCode.Success)
        {
            throw AdapterException.Of(
                "RVT MQTT publish was rejected by the broker with reason code ",
                reasonCode.ToString());
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient.IsConnected)
        {
            return;
        }

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (!_mqttClient.IsConnected && !await ConnectAsync(cancellationToken))
            {
                throw AdapterException.Of("RVT MQTT client connection failed ");
            }
        }
        catch (Exception exception) when (exception is not AdapterException)
        {
            RvtLogger.Logger.LogError(exception, "RVT MQTT client connection failed");
            throw;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Loads the client certificate once and keeps it for the client's
    /// lifetime. It used to be reloaded on every <see cref="ConnectAsync"/>
    /// and never disposed, so each reconnect leaked one PKCS12 key handle.
    /// </summary>
    private X509Certificate2Collection GetClientCertificates()
    {
        lock (_certificateLock)
        {
            if (_clientCertificates is not null)
            {
                return _clientCertificates;
            }

            if (!_options.HasClientCertificate)
            {
                throw new InvalidOperationException(
                    "MQTT is enabled but RVT__MQTT_CERTIFICATE_PATH and RVT__MQTT_PRIVATE_KEY_PATH are not configured.");
            }

            using X509Certificate2 pemCertificate = X509Certificate2.CreateFromPemFile(
                _options.CertificatePath,
                _options.PrivateKeyPath);
            _clientCertificate = X509CertificateLoader.LoadPkcs12(
                pemCertificate.Export(X509ContentType.Pkcs12),
                null);
            _clientCertificates = new X509Certificate2Collection(_clientCertificate);
            return _clientCertificates;
        }
    }

    /// <summary>
    /// Releases the underlying broker connection and the connect gate.
    /// Neither was disposed before, so the MQTTnet client's resources
    /// outlived the owner — enough to keep a host process alive.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _mqttClient.Dispose();
        _connectLock.Dispose();
        lock (_certificateLock)
        {
            _clientCertificate?.Dispose();
            _clientCertificate = null;
            _clientCertificates = null;
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {

        if (!_options.Enabled)
        {
            RvtLogger.Logger.LogInformation("MQTT is disabled, not connecting.");
            return true;
        }

        MqttClientConnectResult ack = await _mqttClient!.ConnectAsync(new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Hostname, _options.Port)
            .WithClientId(_options.ClientId)
            .WithCredentials(_options.Username, "")
            .WithTlsOptions(options =>
            {
                options.UseTls();
                options.WithClientCertificatesProvider(new DefaultMqttCertificatesProvider(GetClientCertificates()));
            })
            .Build(), cancellationToken);
        return ack.ResultCode == MqttClientConnectResultCode.Success;
    }
}
