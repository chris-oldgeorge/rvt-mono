namespace Rvt.Monitor.Common.Mqtt
{
    public interface IMqttClient
    {
        public Task PublishAsync(string topic, string message, CancellationToken cancellationToken = default);

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    }
}
