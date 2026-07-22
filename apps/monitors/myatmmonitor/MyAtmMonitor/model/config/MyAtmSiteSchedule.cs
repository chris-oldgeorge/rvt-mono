namespace MyAtm.Model.Config;

public sealed class MyAtmSiteSchedule
{
    public TimeSpan? WeekdayStart { get; init; }
    public TimeSpan? WeekdayEnd { get; init; }
    public TimeSpan? SaturdayStart { get; init; }
    public TimeSpan? SaturdayEnd { get; init; }
    public TimeSpan? SundayStart { get; init; }
    public TimeSpan? SundayEnd { get; init; }
}
