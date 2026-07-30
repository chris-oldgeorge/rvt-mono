using System.Globalization;

namespace AirQ.Api.UseCases
{
    // Summary: Backfills yesterday's AirQ noise samples via the per-date store handler.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitorsNoiseLevels).
    public class StoreAllNoiseLevelsForYesterdayHandler
    {
        private readonly StoreNoiseLevelsForDateHandler _storeNoiseLevelsForDate;
        private readonly TimeProvider _timeProvider;

        public StoreAllNoiseLevelsForYesterdayHandler(
            StoreNoiseLevelsForDateHandler storeNoiseLevelsForDate,
            TimeProvider? timeProvider = null)
        {
            _storeNoiseLevelsForDate = storeNoiseLevelsForDate;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public Task RunAsync(string userId, string userAuth, CancellationToken cancellationToken = default)
        {
            // The job is scheduled under TimeZoneId "UTC"; DateTime.Today reads
            // the host's local date, so on a UTC+2 host the 00:03 run asked the
            // vendor for the wrong day.
            string dateStr = _timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(-1)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return _storeNoiseLevelsForDate.RunAsync(userId, userAuth, dateStr, cancellationToken);
        }
    }
}
