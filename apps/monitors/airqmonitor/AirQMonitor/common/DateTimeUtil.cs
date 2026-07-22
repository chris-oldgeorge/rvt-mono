
namespace AirQ.Common
{
    public sealed class DateTimeUtil
    {
        public static DateTime ToUtc(DateTime dateTime)
        {
            return TimeZoneInfo.ConvertTime((DateTime)dateTime!, TimeZoneInfo.Utc);
        }
    }
}
