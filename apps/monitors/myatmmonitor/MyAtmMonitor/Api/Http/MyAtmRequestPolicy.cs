using System.Net;
using System.Net.Http.Headers;
using MyAtm.Model.Config;

namespace MyAtm.Api.Http;

// Shared per-client request pacing and retry policy for every MyAtmosphere endpoint.
public sealed class MyAtmRequestPolicy
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly int _maximumAttempts;
    private readonly TimeSpan _minimumRequestInterval;
    private readonly TimeSpan _fallbackRetryCap;
    private readonly TimeSpan _maximumRetryDelay;
    private DateTimeOffset _nextRequestAt = DateTimeOffset.MinValue;

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
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._delayAsync = delayAsync ?? Task.Delay;
        _maximumAttempts = options.MaximumAttempts;
        _minimumRequestInterval = TimeSpan.FromMilliseconds(options.MinimumRequestIntervalMilliseconds);
        _fallbackRetryCap = TimeSpan.FromSeconds(options.FallbackRetryCapSeconds);
        _maximumRetryDelay = TimeSpan.FromSeconds(options.MaximumRetryDelaySeconds);
    }

    public int MaximumAttempts => _maximumAttempts;

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

            _nextRequestAt = _timeProvider.GetUtcNow() + _minimumRequestInterval;
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

        double cappedSeconds = Math.Min(_fallbackRetryCap.TotalSeconds, Math.Pow(2, retryNumber));
        int jitterMilliseconds = Random.Shared.Next(0, 250);
        return Cap(TimeSpan.FromSeconds(cappedSeconds) + TimeSpan.FromMilliseconds(jitterMilliseconds));
    }

    public bool ShouldRetry(HttpStatusCode statusCode, int attempt) =>
        attempt < _maximumAttempts &&
        (statusCode == HttpStatusCode.RequestTimeout ||
         statusCode == HttpStatusCode.TooManyRequests ||
         (int)statusCode >= 500);

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => _delayAsync(delay, cancellationToken);

    private TimeSpan Cap(TimeSpan delay) => delay > _maximumRetryDelay ? _maximumRetryDelay : delay;
}
