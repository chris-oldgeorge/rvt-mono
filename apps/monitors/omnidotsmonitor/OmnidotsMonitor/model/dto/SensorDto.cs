using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

namespace Omnidots.Model.Dto
{

    public class SensorDto
    {
        public string SerialId { get; }
        public string Name { get; }
        public DateTime Lastseen { get; }
        public int BatteryCharge { get; }
        public string ConnectedUsing { get; }
        public bool Online { get; }

        public SensorDto(string serialId, string? name, DateTime? lastseen, int? batteryCharge,
                         string? connectedUsing, bool online)
        {
            SerialId = serialId;
            Name = name ?? OmnidotsProtocol.UNKNOWN;
            Lastseen = DateTimeUtil.TruncateMillis(lastseen ?? DateTime.UtcNow);
            BatteryCharge = batteryCharge ?? -1;
            ConnectedUsing = connectedUsing ?? OmnidotsProtocol.UNKNOWN;
            Online = online;
        }
    }
}
