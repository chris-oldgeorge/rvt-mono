using Rvt.Communication.Abstractions;

namespace Rvt.Monitor.Common.Delivery;

/// <summary>
/// The shared retry backoff used by every durable delivery path.
/// </summary>
/// <remarks>
/// The alert dispatcher and the monitor delivery dispatcher each carried their
/// own copy of this calculation — one in ticks, one in whole seconds — so the
/// same policy could drift apart, and did. Both now call this.
///
/// The schedule is exponential from the initial delay, capped, and never
/// shorter than a provider-requested <c>Retry-After</c>, which is itself
/// bounded by the cap so a hostile or mistaken provider value cannot park a
/// message indefinitely.
/// </remarks>
public static class DeliveryRetrySchedule
{
    public static TimeSpan NextDelay(
        int attemptCount,
        TimeSpan initialDelay,
        TimeSpan cap,
        Exception? exception = null)
    {
        if (cap <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        TimeSpan exponential = Exponential(attemptCount, initialDelay, cap);
        TimeSpan requested = RequestedRetryAfter(exception);
        var ticks = Math.Min(Math.Max(exponential.Ticks, requested.Ticks), cap.Ticks);
        return TimeSpan.FromTicks(Math.Max(0, ticks));
    }

    private static TimeSpan Exponential(int attemptCount, TimeSpan initialDelay, TimeSpan cap)
    {
        if (initialDelay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var exponent = Math.Max(0, attemptCount - 1);
        // Computed in double to allow a large exponent to saturate rather than
        // overflow the tick count before the cap is applied.
        var ticks = initialDelay.Ticks * Math.Pow(2, exponent);
        return ticks >= cap.Ticks ? cap : TimeSpan.FromTicks((long)ticks);
    }

    private static TimeSpan RequestedRetryAfter(Exception? exception) =>
        exception is DeliveryException { RetryAfter: { } retryAfter } && retryAfter > TimeSpan.Zero
            ? retryAfter
            : TimeSpan.Zero;
}
