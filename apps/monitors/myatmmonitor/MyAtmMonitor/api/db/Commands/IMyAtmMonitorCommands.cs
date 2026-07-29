using MyAtm.Api;
using MyAtm.Model.Dto;

namespace MyAtm.Api.Db
{
    public interface IMyAtmMonitorCommands
    {
        void WriteMonitorList(List<DustMonitorDto> devices);

        void WriteLatestTimestamp(string serialNumber, DateTime lastDataTime, Period period);

        void WriteFleetNr(string serialNumber, string fleetNr);

        void SetMonitorOffline(Guid monitorId, bool offline);
    }
}
