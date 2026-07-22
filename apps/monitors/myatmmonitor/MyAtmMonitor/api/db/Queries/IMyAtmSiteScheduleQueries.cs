using MyAtm.Model.Config;

namespace MyAtm.Api.Db;

public interface IMyAtmSiteScheduleQueries
{
    MyAtmSiteSchedule ReadSiteSchedule(Guid monitorId);
}
