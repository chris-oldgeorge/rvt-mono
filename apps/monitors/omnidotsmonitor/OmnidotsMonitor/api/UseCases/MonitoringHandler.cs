using Omnidots.Model.Config;
using Omnidots.Model.Dto;

namespace Omnidots.Api.UseCases
{
    // Summary: Emails a warning when no monitor has delivered data for an hour during working hours.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiVibrationLevels).
    public class MonitoringHandler
    {
        private readonly OmnidotsMonitorReader _monitorReader;
        private readonly OmnidotsMonitoringOptions _options;
        private readonly IOmnidotsMonitoringNotifier _notifier;
        private readonly TimeProvider _timeProvider;
        private readonly TimeZoneInfo _monitoringTimeZone;

        public MonitoringHandler(
            OmnidotsMonitorReader monitorReader,
            OmnidotsMonitoringOptions options,
            IOmnidotsMonitoringNotifier notifier,
            TimeProvider timeProvider)
        {
            _monitorReader = monitorReader;
            _options = options;
            _notifier = notifier;
            _timeProvider = timeProvider;
            _monitoringTimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;
            TimeSpan localTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, _monitoringTimeZone).TimeOfDay;
            if (localTime <= _options.WindowStart || localTime >= _options.WindowEnd)
            {
                return;
            }

            List<VibrationMonitorDto> monitors = _monitorReader.ReadMonitors();
            if (monitors.Count == 0)
            {
                return;
            }

            DateTime? newestLastDataTime = AsUtc(monitors.Max(x => x.LastDataTime));
            if (!newestLastDataTime.HasValue
                || newestLastDataTime.Value < utcNow - _options.StaleAfter)
            {
                await _notifier.SendNoDataWarningAsync(
                    _options.Recipient,
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
