using MyAtm.Model.Dto;

namespace MyAtm.Api.Db
{
    public interface IMyAtmMonitorCommands
    {
        void WriteMonitorList(List<DustMonitorDto> devices);

        void SetMonitorOffline(Guid monitorId, bool offline);
    }
}
