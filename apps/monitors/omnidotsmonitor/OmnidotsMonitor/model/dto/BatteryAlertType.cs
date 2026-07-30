namespace Omnidots.Model.Dto
{
    // Summary: Battery alert level reported for an Omnidots monitor.
    // Major updates:
    // - 2026-07-30 M7: moved off the OmnidotsApi facade so the model layer no
    //   longer imports the api layer.
    public enum BatteryAlertType
    {
        Off = 0,
        BatteryAlert = 1,
        BatteryCaution = 2
    }
}
