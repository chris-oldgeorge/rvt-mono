namespace Omnidots.Api.UseCases
{
    // Summary: Shared clamp for how far back the sample-fetch handlers may request data.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiVibrationLevels).
    internal static class SampleFetchWindow
    {
        internal static int MaxInterval(int interval)
        {
            if (interval < -10)
            {
                return -10;
            }
            return interval;
        }

        internal static DateTime Start(DateTime utcNow, TimeSpan lookback, TimeSpan overlap)
        {
            if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("utcNow must be UTC.", nameof(utcNow));
            if (lookback <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lookback));
            if (overlap < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(overlap));
            return utcNow - lookback - overlap;
        }
    }
}
