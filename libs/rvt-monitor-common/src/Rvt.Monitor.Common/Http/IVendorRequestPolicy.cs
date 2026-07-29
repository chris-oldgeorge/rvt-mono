using System.Net;

namespace Rvt.Monitor.Common.Http;

// Summary: Pacing and retry decisions for a vendor HTTP transport.
// Major updates:
// - 2026-07-29 Vendor transport consolidation: generalized from MyAtm's request
//   policy, the one monitor that had pacing, retry, and Retry-After handling.

/// <summary>
/// Decides when a vendor request may be sent and whether a failed attempt is
/// retried. Implementations own their own clock and delay mechanics so tests
/// can drive them deterministically.
/// </summary>
public interface IVendorRequestPolicy
{
    Task WaitForPermitAsync(CancellationToken cancellationToken);

    bool ShouldRetry(HttpStatusCode statusCode, int attempt);

    TimeSpan GetRetryDelay(HttpResponseMessage response, int retryNumber);

    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
