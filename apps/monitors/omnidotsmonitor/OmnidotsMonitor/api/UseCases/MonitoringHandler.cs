using Omnidots.Model.Config;
using Omnidots.Model.Dto;

namespace Omnidots.Api.UseCases
{
    // Summary: Emails a warning when no monitor has delivered data for an hour during working hours.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiVibrationLevels).
    public class MonitoringHandler
    {
        private readonly OmnidotsMonitorReader monitorReader;
        private readonly OmnidotsMonitoringOptions options;
        private readonly IOmnidotsMonitoringNotifier notifier;
        private readonly TimeProvider timeProvider;
        private readonly TimeZoneInfo monitoringTimeZone;

        public MonitoringHandler(
            OmnidotsMonitorReader monitorReader,
            OmnidotsMonitoringOptions options,
            IOmnidotsMonitoringNotifier notifier,
            TimeProvider timeProvider)
        {
            this.monitorReader = monitorReader;
            this.options = options;
            this.notifier = notifier;
            this.timeProvider = timeProvider;
            monitoringTimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
            TimeSpan localTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, monitoringTimeZone).TimeOfDay;
            if (localTime <= options.WindowStart || localTime >= options.WindowEnd)
            {
                return;
            }

            List<VibrationMonitorDto> monitors = monitorReader.ReadMonitors();
            if (monitors.Count == 0)
            {
                return;
            }

            DateTime? newestLastDataTime = AsUtc(monitors.Max(x => x.LastDataTime));
            if (!newestLastDataTime.HasValue
                || newestLastDataTime.Value < utcNow - options.StaleAfter)
            {
                await notifier.SendNoDataWarningAsync(
                    options.Recipient,
                    utcNow,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private static DateTime? AsUtc(DateTime? value) => value?.Kind switch
        {
            null => null,
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
