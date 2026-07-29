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
}
