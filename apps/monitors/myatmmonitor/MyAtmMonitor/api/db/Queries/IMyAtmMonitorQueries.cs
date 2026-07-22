using MyAtm.Model.Dto;

namespace MyAtm.Api.Db
{
    public interface IMyAtmMonitorQueries
    {
        List<DustMonitorDto> ReadMonitorList(int customerId, DateTime? lastDataTime);

        DustMonitorDto? ReadMonitor(int customerId, string serialId);

        List<DustMonitorDto> ReadMonitorList(DateTime? lastDataTime);

        DustMonitorDto? ReadMonitor(string serialId);
    }
}
