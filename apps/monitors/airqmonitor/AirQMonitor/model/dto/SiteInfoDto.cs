using Rvt.Monitor.Common.Utilities;

namespace AirQ.Model.Dto
{

    public class SiteInfoDto
    {
        public Guid SiteId { get; }
        public TimeSpan? StartTime { get; }
        public TimeSpan? EndTime { get; }

        public TimeSpan? SatStartTime { get; }
        public TimeSpan? SatEndTime { get; }

        public TimeSpan? SunStartTime { get; }
        public TimeSpan? SunEndTime { get; }


        public SiteInfoDto(Guid siteId,
                           TimeSpan? startTime, TimeSpan? endTime,
                           TimeSpan? satStartTime, TimeSpan? satEndTime,
                           TimeSpan? sunStartTime, TimeSpan? sunEndTime)
        {
            SiteId = siteId;

            StartTime = startTime;
            EndTime = endTime;

            SatStartTime = satStartTime;
            SatEndTime = satEndTime;

            SunStartTime = sunStartTime;
            SunEndTime = sunEndTime;
        }

        public bool ShouldReportForDate(DateTime date)
        {

            switch (date.DayOfWeek)
            {
                case DayOfWeek.Sunday:
                    return SunStartTime != null && SunEndTime != null;

                case DayOfWeek.Saturday:
                    return SatStartTime != null && SatEndTime != null;

                default:
                    return StartTime != null && EndTime != null;

            }
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
}
