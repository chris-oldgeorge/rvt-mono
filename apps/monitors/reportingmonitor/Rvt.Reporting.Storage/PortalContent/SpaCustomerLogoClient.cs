using Microsoft.Extensions.Options;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace Rvt.Reporting.Storage.PortalContent;

/// <summary>
/// Fetches customer report logos from the SPA backend internal report-content API.
/// Major updates: 2026-06-24 added optional customer-logo integration for report branding.
/// </summary>
public sealed class SpaCustomerLogoClient(HttpClient httpClient, IOptions<SpaCustomerLogoClientOptions> options) : ICustomerLogoProvider
{
    private const int _maximumLogoBytes = 2 * 1024 * 1024;
    private const string InternalKeyHeader = "X-RVT-Internal-Key";
    private readonly HttpClient _httpClient = httpClient;
    private readonly SpaCustomerLogoClientOptions _options = options.Value;

    public async Task<CustomerLogo?> GetSiteLogoAsync(Guid siteId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            return null;
        }

        if (!TryBuildLogoUri(siteId, out Uri? logoUri))
        {
            return null;
        }

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, logoUri);
            request.Headers.TryAddWithoutValidation(InternalKeyHeader, _options.InternalApiKey);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode || !IsSupportedImage(response.Content.Headers.ContentType?.MediaType))
            {
                return null;
            }

            byte[]? content = await ReadBoundedLogoAsync(response.Content, cancellationToken).ConfigureAwait(false);
            return content is null
                ? null
                : new CustomerLogo(content, response.Content.Headers.ContentType?.MediaType ?? "image/png");
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<byte[]?> ReadBoundedLogoAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        long? contentLength = content.Headers.ContentLength;
        if (contentLength is 0 or > _maximumLogoBytes)
        {
            return null;
        }

        await using Stream source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream destination = contentLength is > 0
            ? new MemoryStream((int)contentLength.Value)
            : new MemoryStream();
        byte[] buffer = new byte[81920];
        while (true)
        {
            int remainingWithSentinel = (int)(_maximumLogoBytes - destination.Length + 1);
            int requestedBytes = Math.Min(buffer.Length, remainingWithSentinel);
            int read = await source.ReadAsync(
                buffer.AsMemory(0, requestedBytes),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (destination.Length + read > _maximumLogoBytes)
            {
                return null;
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken).ConfigureAwait(false);
        }

        return destination.Length == 0 ? null : destination.ToArray();
    }

    private bool TryBuildLogoUri(Guid siteId, out Uri logoUri)
    {
        logoUri = null!;
        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            return false;
        }

        logoUri = new Uri(baseUri, $"/api/report-content/sites/{siteId}/customer-logo");
        return true;
    }

    private static bool IsSupportedImage(string? contentType)
    {
        return contentType is "image/png" or "image/jpeg" or "image/webp";
    }
}

public sealed class SpaCustomerLogoClientOptions
{
    public string? BaseUrl { get; set; }

    public string? InternalApiKey { get; set; }
}
