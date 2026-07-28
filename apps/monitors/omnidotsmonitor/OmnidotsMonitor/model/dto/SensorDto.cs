using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

namespace Omnidots.Model.Dto;


public class SensorDto(string serialId, string? name, DateTime? lastseen, int? batteryCharge,
                 string? connectedUsing, bool online)
{
    public string SerialId { get; } = serialId;
    public string Name { get; } = name ?? OmnidotsProtocol.UNKNOWN;
    public DateTime Lastseen { get; } = DateTimeUtil.TruncateMillis(lastseen ?? DateTime.UtcNow);
    public int BatteryCharge { get; } = batteryCharge ?? -1;
    public string ConnectedUsing { get; } = connectedUsing ?? OmnidotsProtocol.UNKNOWN;
    public bool Online { get; } = online;
}
