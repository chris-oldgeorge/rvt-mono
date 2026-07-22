namespace Omnidots.Model.Dto
{
    // Summary: Omnidots webhook/vendor protocol strings (message kinds, header names, placeholder values).
    // Major updates:
    // - 2026-07-12 RvtConfig cleanup: moved from RvtConfig; these are protocol constants, not configuration.
    public static class OmnidotsProtocol
    {
        public const string UNKNOWN = "Unknown";
        public const string UNSPECIFIED = "unspecified";
        public const string ON = "On";
        public const string OFF = "Off";
        public const string MSG_SENSOR_ONLINE = "sensor_went_online";
        public const string MSG_SENSOR_GUIDELINE_ALARM = "sensor_guide_line_alarm";
        public const string MSG_SENSOR_OFFLINE = "sensor_measurement_stopped_clipping";
        public const string SIGNATURE_HEADER = "x-omnidots-notifier-signature";
    }
}
