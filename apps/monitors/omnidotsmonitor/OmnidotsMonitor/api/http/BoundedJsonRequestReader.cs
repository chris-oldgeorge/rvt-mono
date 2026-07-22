using System.Buffers;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Omnidots.Api.Http;

public static class BoundedJsonRequestReader
{
    public const int MaxBodyBytes = 64 * 1024;

    public static async Task<byte[]> ReadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) ||
            !string.Equals(
                contentType.MediaType.Value,
                "application/json",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new OmnidotsUnsupportedMediaTypeException();
        }

        if (request.Headers.TryGetValue(HeaderNames.ContentEncoding, out var encodings) &&
            (encodings.Count != 1 ||
             !string.Equals(encodings[0], "identity", StringComparison.OrdinalIgnoreCase)))
        {
            throw new OmnidotsUnsupportedMediaTypeException();
        }

        if (request.ContentLength > MaxBodyBytes)
        {
            throw new OmnidotsRequestBodyTooLargeException();
        }

        var buffer = ArrayPool<byte>.Shared.Rent(MaxBodyBytes + 1);
        var bytesRead = 0;
        try
        {
            while (bytesRead <= MaxBodyBytes)
            {
                var read = await request.Body.ReadAsync(
                    buffer.AsMemory(bytesRead, MaxBodyBytes + 1 - bytesRead),
                    cancellationToken);
                if (read == 0)
                {
                    return buffer.AsSpan(0, bytesRead).ToArray();
                }

                bytesRead += read;
                if (bytesRead > MaxBodyBytes)
                {
                    throw new OmnidotsRequestBodyTooLargeException();
                }
            }

            throw new OmnidotsRequestBodyTooLargeException();
        }
        finally
        {
            buffer.AsSpan(0, bytesRead).Clear();
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
