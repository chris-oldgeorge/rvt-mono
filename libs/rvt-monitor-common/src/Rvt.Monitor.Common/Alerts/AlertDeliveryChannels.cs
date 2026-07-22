namespace Rvt.Monitor.Common.Alerts;

[Flags]
public enum AlertDeliveryChannels
{
    None = 0,
    Mqtt = 1,
    Email = 2,
    Sms = 4
}
