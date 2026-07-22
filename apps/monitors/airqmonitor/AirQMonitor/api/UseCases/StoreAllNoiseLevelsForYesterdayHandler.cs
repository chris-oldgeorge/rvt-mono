using System.Globalization;

namespace AirQ.Api.UseCases
{
    // Summary: Backfills yesterday's AirQ noise samples via the per-date store handler.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitorsNoiseLevels).
    public class StoreAllNoiseLevelsForYesterdayHandler
    {
        private readonly StoreNoiseLevelsForDateHandler storeNoiseLevelsForDate;

        public StoreAllNoiseLevelsForYesterdayHandler(StoreNoiseLevelsForDateHandler storeNoiseLevelsForDate)
        {
            this.storeNoiseLevelsForDate = storeNoiseLevelsForDate;
        }

        public void Run(string userId, string userAuth)
        {
            var dateStr = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            storeNoiseLevelsForDate.Run(userId, userAuth, dateStr);
        }
    }
}
