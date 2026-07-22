using System.Net;
using MyAtm.Model.Config;

namespace MyAtm.Api.Http;

// Shared per-client request pacing and retry policy for every MyAtmosphere endpoint.
public sealed class MyAtmRequestPolicy
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeProvider timeProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly int maximumAttempts;
    private readonly TimeSpan minimumRequestInterval;
    private readonly TimeSpan fallbackRetryCap;
    private readonly TimeSpan maximumRetryDelay;
    private DateTimeOffset nextRequestAt = DateTimeOffset.MinValue;

    public MyAtmRequestPolicy(
        TimeProvider? timeProvider = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
        : this(new MyAtmVendorOptions(), timeProvider, delayAsync)
    {
    }

    public MyAtmRequestPolicy(
        MyAtmVendorOptions options,
        TimeProvider? timeProvider = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.delayAsync = delayAsync ?? Task.Delay;
        maximumAttempts = options.MaximumAttempts;
        minimumRequestInterval = TimeSpan.FromMilliseconds(options.MinimumRequestIntervalMilliseconds);
        fallbackRetryCap = TimeSpan.FromSeconds(options.FallbackRetryCapSeconds);
        maximumRetryDelay = TimeSpan.FromSeconds(options.MaximumRetryDelaySeconds);
    }

    public int MaximumAttempts => maximumAttempts;

    public async Task WaitForPermitAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            var delay = nextRequestAt - now;
            if (delay > TimeSpan.Zero)
            {
                await delayAsync(delay, cancellationToken);
            }

            nextRequestAt = timeProvider.GetUtcNow() + minimumRequestInterval;
        }
        finally
        {
            gate.Release();
        }
    }

    public TimeSpan GetRetryDelay(HttpResponseMessage response, int retryNumber)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return Cap(delta);
        }

        if (retryAfter?.Date is { } date)
        {
            var delay = date - timeProvider.GetUtcNow();
            if (delay > TimeSpan.Zero)
            {
                return Cap(delay);
            }
        }

        var cappedSeconds = Math.Min(fallbackRetryCap.TotalSeconds, Math.Pow(2, retryNumber));
        var jitterMilliseconds = Random.Shared.Next(0, 250);
        return Cap(TimeSpan.FromSeconds(cappedSeconds) + TimeSpan.FromMilliseconds(jitterMilliseconds));
    }

    public bool ShouldRetry(HttpStatusCode statusCode, int attempt) =>
        attempt < maximumAttempts &&
        (statusCode == HttpStatusCode.RequestTimeout ||
         statusCode == HttpStatusCode.TooManyRequests ||
         (int)statusCode >= 500);

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => delayAsync(delay, cancellationToken);

    private TimeSpan Cap(TimeSpan delay) => delay > maximumRetryDelay ? maximumRetryDelay : delay;
}
