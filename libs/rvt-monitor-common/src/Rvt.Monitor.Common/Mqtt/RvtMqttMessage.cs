namespace Rvt.Monitor.Common.Mqtt
{

    public record RvtMqttMessage
    {
        public DateTime Timestamp { get; }
        public int? CustomerId { get; }
        public string SerialNumber { get; }
        public string Message { get; }

        public RvtMqttMessage(DateTime timestamp, string serialNumber, string message)
        {
            Timestamp = timestamp;
            SerialNumber = serialNumber;
            Message = message;
        }

        public RvtMqttMessage(DateTime timestamp, int customerId, string serialNumber, string message)
            : this(timestamp, serialNumber, message)
        {
            CustomerId = customerId;
        }
    }
}
