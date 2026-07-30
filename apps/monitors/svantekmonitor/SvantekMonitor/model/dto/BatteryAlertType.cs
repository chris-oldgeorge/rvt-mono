namespace Svantek.Model.Dto
{
    // Summary: Battery alert level reported for a Svantek monitor.
    // Major updates:
    // - 2026-07-30 M7: moved off the SvantekApi facade so the model layer no
    //   longer imports the api layer.
    public enum BatteryAlertType
    {
        Off = 0,
        BatteryAlert = 1,
        BatteryCaution = 2
    }
}
