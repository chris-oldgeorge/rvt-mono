using System.Net;
using System.Text;
using Rvt.Monitor.Common.Diagnostics;

namespace Rvt.Monitor.Common.Http;

// Summary: A vendor response whose body reads honor the configured size bound.

/// <summary>
/// Wraps the vendor response. The body is only downloaded when a read method
/// is called, so error paths that must not touch the vendor body simply do not
/// read it.
/// </summary>
public sealed class VendorHttpResponse : IDisposable
{
    private readonly HttpResponseMessage _response;
    private readonly int? _maxResponseBytes;

    internal VendorHttpResponse(HttpResponseMessage response, int? maxResponseBytes)
    {
        _response = response;
        _maxResponseBytes = maxResponseBytes;
    }

    public HttpStatusCode StatusCode => _response.StatusCode;

    public bool IsOk => _response.StatusCode == HttpStatusCode.OK;

    public async Task<string> ReadStringAsync(CancellationToken cancellationToken)
    {
        if (_maxResponseBytes is not { } limit)
        {
            return await _response.Content.ReadAsStringAsync(cancellationToken);
        }

        byte[] bounded = await ReadBoundedAsync(limit, cancellationToken);
        return Encoding.UTF8.GetString(bounded);
    }

    public async Task<byte[]> ReadBytesAsync(CancellationToken cancellationToken)
    {
        if (_maxResponseBytes is not { } limit)
        {
            return await _response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        return await ReadBoundedAsync(limit, cancellationToken);
    }

    private async Task<byte[]> ReadBoundedAsync(int maxResponseBytes, CancellationToken cancellationToken)
    {
        long? contentLength = _response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > maxResponseBytes)
        {
            throw AdapterException.Of($"HTTP response exceeded the configured {maxResponseBytes}-byte limit.");
        }

        await using Stream source = await _response.Content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream destination = new();
        byte[] buffer = new byte[Math.Min(81920, maxResponseBytes)];
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (destination.Length + read > maxResponseBytes)
            {
                throw AdapterException.Of($"HTTP response exceeded the configured {maxResponseBytes}-byte limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return destination.ToArray();
    }

    public void Dispose() => _response.Dispose();
}
