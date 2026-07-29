using Rvt.Monitor.Common.Utilities;

namespace AirQ.Model.Dto;


public class SiteInfoDto(Guid siteId,
                   TimeSpan? startTime, TimeSpan? endTime,
                   TimeSpan? satStartTime, TimeSpan? satEndTime,
                   TimeSpan? sunStartTime, TimeSpan? sunEndTime)
{
    public Guid SiteId { get; } = siteId;
    public TimeSpan? StartTime { get; } = startTime;
    public TimeSpan? EndTime { get; } = endTime;

    public TimeSpan? SatStartTime { get; } = satStartTime;
    public TimeSpan? SatEndTime { get; } = satEndTime;

    public TimeSpan? SunStartTime { get; } = sunStartTime;
    public TimeSpan? SunEndTime { get; } = sunEndTime;

    public bool ShouldReportForDate(DateTime date)
    {

        return date.DayOfWeek switch
        {
            DayOfWeek.Sunday => SunStartTime != null && SunEndTime != null,
            DayOfWeek.Saturday => SatStartTime != null && SatEndTime != null,
            _ => StartTime != null && EndTime != null,
        };
    }

    public void GetStartAndEndTimeForDate(DateTime date, out DateTime startTime, out DateTime endTime)
    {

        switch (date.DayOfWeek)
        {
            case DayOfWeek.Sunday:
                startTime = DateTimeUtil.LocalToUtc((DateTime)(date + SunStartTime!));
                endTime = DateTimeUtil.LocalToUtc((DateTime)(date + SunEndTime!));
                break;

            case DayOfWeek.Saturday:
                startTime = DateTimeUtil.LocalToUtc((DateTime)(date + SatStartTime!));
                endTime = DateTimeUtil.LocalToUtc((DateTime)(date + SatEndTime!));
                break;
            default:
                startTime = DateTimeUtil.LocalToUtc((DateTime)(date + StartTime!));
                endTime = DateTimeUtil.LocalToUtc((DateTime)(date + EndTime!));
                break;

        }
    }

}
