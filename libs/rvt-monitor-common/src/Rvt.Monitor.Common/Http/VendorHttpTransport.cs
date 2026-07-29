using System.Net;

namespace Rvt.Monitor.Common.Http;

// Summary: The shared request engine behind every monitor's vendor HTTP client.
// Major updates:
// - 2026-07-29 Vendor transport consolidation: replaced four per-monitor copies
//   of the send/dispose/read loop; MyAtm's pacing, retry, and bounded-read
//   behaviour became the engine's optional capabilities.

/// <summary>
/// Executes vendor requests with optional pacing/retry and an optional response
/// size bound. The monitor-specific pieces stay in each monitor's client:
/// base address, headers, timeout (configured on the <see cref="HttpClient"/>
/// it owns), and how a non-success response is logged and translated —
/// those error contracts are deliberately different per vendor and pinned by
/// each monitor's tests.
/// </summary>
/// <remarks>
/// Responses are requested with
/// <see cref="HttpCompletionOption.ResponseHeadersRead"/>, so a caller that
/// throws without reading the body never downloads it. The request (and
/// therefore its content) is disposed as soon as the send completes; because
/// content cannot be resent once disposed, requests that carry content are
/// never retried.
/// </remarks>
public sealed class VendorHttpTransport
{
    private readonly HttpClient _httpClient;
    private readonly IVendorRequestPolicy? _policy;
    private readonly int? _maxResponseBytes;

    public VendorHttpTransport(
        HttpClient httpClient,
        IVendorRequestPolicy? policy = null,
        int? maxResponseBytes = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (maxResponseBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResponseBytes));
        }

        _httpClient = httpClient;
        _policy = policy;
        _maxResponseBytes = maxResponseBytes;
    }

    /// <summary>
    /// Sends the request, applying the policy loop for content-less requests,
    /// and returns the response for the caller to interpret. The caller owns
    /// disposal of the returned response.
    /// </summary>
    public async Task<VendorHttpResponse> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            if (_policy is not null)
            {
                await _policy.WaitForPermitAsync(cancellationToken);
            }

            HttpResponseMessage response;
            using (HttpRequestMessage request = new(method, path))
            {
                request.Content = content;
                response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            }

            if (response.StatusCode == HttpStatusCode.OK ||
                content is not null ||
                _policy is null ||
                !_policy.ShouldRetry(response.StatusCode, attempt))
            {
                return new VendorHttpResponse(response, _maxResponseBytes);
            }

            TimeSpan delay = _policy.GetRetryDelay(response, attempt);
            response.Dispose();
            await _policy.DelayAsync(delay, cancellationToken);
        }
    }
}
