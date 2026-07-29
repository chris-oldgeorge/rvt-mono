using System.Net;
using System.Net.Http.Headers;

namespace Rvt.Monitor.Common.Http;

// Summary: Per-client request pacing and retry policy for vendor endpoints.
// Major updates:
// - 2026-07-29 Vendor transport consolidation: moved from MyAtm, whose policy
//   contained no vendor-specific logic — Retry-After parsing, capped
//   exponential backoff with jitter, and minimum request spacing.

/// <summary>
/// Honors <c>Retry-After</c> (delta or date form), falls back to capped
/// exponential backoff with jitter, retries transient statuses up to a
/// configured attempt count, and spaces consecutive requests by a minimum
/// interval.
/// </summary>
public class VendorRequestPolicy(
    int maximumAttempts,
    TimeSpan minimumRequestInterval,
    TimeSpan fallbackRetryCap,
    TimeSpan maximumRetryDelay,
    TimeProvider? timeProvider = null,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null) : IVendorRequestPolicy
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync = delayAsync ?? Task.Delay;
    private DateTimeOffset _nextRequestAt = DateTimeOffset.MinValue;

    public int MaximumAttempts { get; } = maximumAttempts;

    public async Task WaitForPermitAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            TimeSpan delay = _nextRequestAt - now;
            if (delay > TimeSpan.Zero)
            {
                await _delayAsync(delay, cancellationToken);
            }

            _nextRequestAt = _timeProvider.GetUtcNow() + minimumRequestInterval;
        }
        finally
        {
            _gate.Release();
        }
    }

    public TimeSpan GetRetryDelay(HttpResponseMessage response, int retryNumber)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return Cap(delta);
        }

        if (retryAfter?.Date is { } date)
        {
            TimeSpan delay = date - _timeProvider.GetUtcNow();
            if (delay > TimeSpan.Zero)
            {
                return Cap(delay);
            }
        }

        double cappedSeconds = Math.Min(fallbackRetryCap.TotalSeconds, Math.Pow(2, retryNumber));
        int jitterMilliseconds = Random.Shared.Next(0, 250);
        return Cap(TimeSpan.FromSeconds(cappedSeconds) + TimeSpan.FromMilliseconds(jitterMilliseconds));
    }

    public bool ShouldRetry(HttpStatusCode statusCode, int attempt) =>
        attempt < MaximumAttempts &&
        (statusCode == HttpStatusCode.RequestTimeout ||
         statusCode == HttpStatusCode.TooManyRequests ||
         (int)statusCode >= 500);

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        _delayAsync(delay, cancellationToken);

    private TimeSpan Cap(TimeSpan delay) => delay > maximumRetryDelay ? maximumRetryDelay : delay;
}
