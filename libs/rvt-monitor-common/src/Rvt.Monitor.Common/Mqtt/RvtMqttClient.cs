using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Certificates;
using MQTTnet.Client;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;


namespace Rvt.Monitor.Common.Mqtt
{

    // Summary: Publishes RVT monitor events to Event Grid MQTT with client-certificate authentication.
    // Major updates:
    // - 2026-06-18 Warning remediation: switched to modern certificate loading, TLS options, and valid log templates.
    // - 2026-07-12 DI composition: connects lazily on first publish instead of requiring an eager ConnectAsync at startup.
    public class RvtMqttClient : IMqttClient
    {
        private readonly MQTTnet.Client.IMqttClient mqttClient;
        private readonly SemaphoreSlim connectLock = new(1, 1);

        public RvtMqttClient()
        {
            mqttClient = new MqttFactory().CreateMqttClient();

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

            mqttClient.ConnectedAsync += _mqttClient_ConnectedAsync;
            mqttClient.DisconnectedAsync += _mqttClient_DisconnectedAsync;

        }

        public async Task PublishAsync(
            string topic,
            string message,
            CancellationToken cancellationToken = default)
        {
            if (!RvtConfig.MQTT_ENABLED)
            {
                RvtLogger.Logger.LogInformation("MQTT is disabled, not publishing.");
                return;
            }

            await EnsureConnectedAsync(cancellationToken);
            await mqttClient.PublishStringAsync(topic, message, cancellationToken: cancellationToken);
        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (mqttClient.IsConnected)
            {
                return;
            }

            await connectLock.WaitAsync(cancellationToken);
            try
            {
                if (!mqttClient.IsConnected && !await ConnectAsync(cancellationToken))
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
                connectLock.Release();
            }
        }

        private static X509Certificate2 GetCert()
        {
            if (string.IsNullOrWhiteSpace(RvtConfig.MQTT_CERTIFICATE_PATH)
                || string.IsNullOrWhiteSpace(RvtConfig.MQTT_PRIVATE_KEY_PATH))
            {
                throw new InvalidOperationException(
                    "MQTT is enabled but RVT__MQTT_CERTIFICATE_PATH and RVT__MQTT_PRIVATE_KEY_PATH are not configured.");
            }

            using var pemCertificate = X509Certificate2.CreateFromPemFile(
                RvtConfig.MQTT_CERTIFICATE_PATH,
                RvtConfig.MQTT_PRIVATE_KEY_PATH);
            return X509CertificateLoader.LoadPkcs12(pemCertificate.Export(X509ContentType.Pkcs12), null);
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {

            if (!RvtConfig.MQTT_ENABLED)
            {
                RvtLogger.Logger.LogInformation("MQTT is disabled, not connecting.");
                return true;
            }

            var ack = await mqttClient!.ConnectAsync(new MqttClientOptionsBuilder()
                .WithTcpServer(RvtConfig.MQTT_HOSTNAME, 8883)
                .WithClientId(RvtConfig.MQTT_CLIENT_ID)
                .WithCredentials(RvtConfig.MQTT_USERNAME, "")
                .WithTlsOptions(options =>
                {
                    options.UseTls();
                    options.WithClientCertificatesProvider(new DefaultMqttCertificatesProvider(new X509Certificate2Collection(GetCert())));
                })
                .Build(), cancellationToken);
            return ack.ResultCode == MqttClientConnectResultCode.Success;
        }
    }
}
