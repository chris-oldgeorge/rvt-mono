
namespace Rvt.Monitor.Common.Notifications;

// needs to have the same values across all adapters
public enum AlertType
{
    Alert = 0,
    Caution = 1,
    Offline = 2,
    Ignore = 3,
    BatteryAlert = 4,
    BatteryCaution = 5
}
