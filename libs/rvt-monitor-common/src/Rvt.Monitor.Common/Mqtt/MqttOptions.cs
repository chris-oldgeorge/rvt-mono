using Rvt.Monitor.Common.Configuration;

namespace Rvt.Monitor.Common.Mqtt;

/// <summary>
/// Connection settings for the RVT Event Grid MQTT broker.
/// </summary>
/// <remarks>
/// The client previously read these from static configuration at the point of
/// use, which hid the dependency, made the broker unreachable to configure per
/// host, and left the settings untestable without process-wide state. They are
/// supplied explicitly now; <see cref="FromRvtConfig"/> preserves the existing
/// environment contract for the composition roots.
/// </remarks>
public sealed record MqttOptions
{
    public bool Enabled { get; init; }

    public string Hostname { get; init; } = string.Empty;

    public int Port { get; init; } = 8883;

    public string ClientId { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string CertificatePath { get; init; } = string.Empty;

    public string PrivateKeyPath { get; init; } = string.Empty;

    /// <summary>
    /// True when the client certificate pair needed for broker authentication
    /// is configured.
    /// </summary>
    public bool HasClientCertificate =>
        !string.IsNullOrWhiteSpace(CertificatePath) && !string.IsNullOrWhiteSpace(PrivateKeyPath);

    public static MqttOptions FromRvtConfig() => new()
    {
        Enabled = RvtConfig.MQTT_ENABLED,
        Hostname = RvtConfig.MQTT_HOSTNAME,
        ClientId = RvtConfig.MQTT_CLIENT_ID,
        Username = RvtConfig.MQTT_USERNAME,
        CertificatePath = RvtConfig.MQTT_CERTIFICATE_PATH,
        PrivateKeyPath = RvtConfig.MQTT_PRIVATE_KEY_PATH,
    };
}
