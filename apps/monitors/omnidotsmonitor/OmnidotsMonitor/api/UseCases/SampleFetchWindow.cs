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
            if (utcNow.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("utcNow must be UTC.", nameof(utcNow));
            }

            if (lookback <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(lookback));
            }

            if (overlap < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(overlap));
            }

            return utcNow - lookback - overlap;
        }

        /// <summary>
        /// Splits <c>[start, end)</c> into consecutive windows of at most
        /// <paramref name="maximumWindow"/>, mirroring Svantek's
        /// <c>NoiseRequestWindowCalculator</c>.
        /// </summary>
        /// <remarks>
        /// The vendor client caps a response at 4 MB and times out after 30
        /// seconds. A monitor whose cursor is months old asked for everything
        /// in one request, so exceeding either bound meant the cursor never
        /// advanced and every later run repeated the same oversized request -
        /// a permanent stall. The union of the windows is exactly the original
        /// range, so the data covered by a run is unchanged; the final window
        /// still ends at <paramref name="end"/>, so a run always reaches the
        /// most recent samples even when earlier windows are empty.
        /// </remarks>
        internal static IReadOnlyList<(DateTime Start, DateTime End)> Split(
            DateTime start,
            DateTime end,
            TimeSpan maximumWindow)
        {
            if (maximumWindow <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumWindow));
            }

            if (end <= start)
            {
                return [(start, end)];
            }

            List<(DateTime Start, DateTime End)> windows = [];
            for (DateTime cursor = start; cursor < end;)
            {
                DateTime windowEnd = end - cursor > maximumWindow ? cursor + maximumWindow : end;
                windows.Add((cursor, windowEnd));
                cursor = windowEnd;
            }

            return windows;
        }
    }
}
