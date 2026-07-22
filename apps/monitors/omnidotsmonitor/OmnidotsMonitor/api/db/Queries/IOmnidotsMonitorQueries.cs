using Omnidots.Model.Config;
using Omnidots.Model.Dto;

namespace Omnidots.Api.Db;

public interface IOmnidotsMonitorQueries
{
    List<VibrationMonitorDto> ReadMonitorList(DateTime? lastDataTime);

    VibrationMonitorDto ReadMonitor(string serialId);

    DateTime ReadDeployStartDate(Guid monitorId);

    SiteTimes ReadSiteTimes(Guid monitorId);
}
