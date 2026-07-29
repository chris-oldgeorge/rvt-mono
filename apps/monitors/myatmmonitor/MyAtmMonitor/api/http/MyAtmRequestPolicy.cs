using MyAtm.Model.Config;
using Rvt.Monitor.Common.Http;

namespace MyAtm.Api.Http;

// Shared per-client request pacing and retry policy for every MyAtmosphere endpoint.
// The mechanics live in the common VendorRequestPolicy; this type maps the
// MyAtm vendor options onto it.
public sealed class MyAtmRequestPolicy(
    MyAtmVendorOptions options,
    TimeProvider? timeProvider = null,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    : VendorRequestPolicy(
        (options ?? throw new ArgumentNullException(nameof(options))).MaximumAttempts,
        TimeSpan.FromMilliseconds(options.MinimumRequestIntervalMilliseconds),
        TimeSpan.FromSeconds(options.FallbackRetryCapSeconds),
        TimeSpan.FromSeconds(options.MaximumRetryDelaySeconds),
        timeProvider,
        delayAsync)
{
    public MyAtmRequestPolicy(
        TimeProvider? timeProvider = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
        : this(new MyAtmVendorOptions(), timeProvider, delayAsync)
    {
    }
}
