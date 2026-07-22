namespace Omnidots.Model.Config
{
    public class SiteTimes
    {

        public static readonly string ALL_DAY_START = "00:00:00";

        public TimeSpan? WeekdayStart { get; set; }
        public TimeSpan? WeekdayEnd { get; set; }
        public TimeSpan? SaturdayStart { get; set; }
        public TimeSpan? SaturdayEnd { get; set; }
        public TimeSpan? SundayStart { get; set; }
        public TimeSpan? SundayEnd { get; set; }

        public SiteTimes()
        {
        }

        public string GetWeekdayStart()
        {
            return TimeToString(WeekdayStart, ALL_DAY_START);
        }

        public string GetWeekdayEnd()
        {
            return TimeToString(WeekdayEnd, ALL_DAY_START);
        }

        public string GetSaturdayStart()
        {
            return TimeToString(SaturdayStart, ALL_DAY_START);
        }

        public string GetSaturdayEnd()
        {
            return TimeToString(SaturdayEnd, ALL_DAY_START);
        }

        public string GetSundayStart()
        {
            return TimeToString(SundayStart, ALL_DAY_START);
        }

        public string GetSundayEnd()
        {
            return TimeToString(SundayEnd, ALL_DAY_START);
        }

        private static string TimeToString(TimeSpan? timeSpan, string defaultValue)
        {
            return timeSpan != null ? ((TimeSpan)timeSpan!).ToString(@"hh\:mm\:ss") : defaultValue;
        }
    }
}

