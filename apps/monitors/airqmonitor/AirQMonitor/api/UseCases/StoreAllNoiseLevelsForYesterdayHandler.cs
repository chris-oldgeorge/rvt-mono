using System.Globalization;

namespace AirQ.Api.UseCases
{
    // Summary: Backfills yesterday's AirQ noise samples via the per-date store handler.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitorsNoiseLevels).
    public class StoreAllNoiseLevelsForYesterdayHandler
    {
        private readonly StoreNoiseLevelsForDateHandler _storeNoiseLevelsForDate;

        public StoreAllNoiseLevelsForYesterdayHandler(StoreNoiseLevelsForDateHandler storeNoiseLevelsForDate)
        {
            _storeNoiseLevelsForDate = storeNoiseLevelsForDate;
        }

        public Task RunAsync(string userId, string userAuth, CancellationToken cancellationToken = default)
        {
            string dateStr = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return _storeNoiseLevelsForDate.RunAsync(userId, userAuth, dateStr, cancellationToken);
        }
    }
}
