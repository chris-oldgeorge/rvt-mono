using Microsoft.Extensions.Options;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace Rvt.Reporting.Storage.PortalContent;

/// <summary>
/// Fetches customer report logos from the SPA backend internal report-content API.
/// Major updates: 2026-06-24 added optional customer-logo integration for report branding.
/// </summary>
public sealed class SpaCustomerLogoClient : ICustomerLogoProvider
{
    private const long MaximumLogoBytes = 2 * 1024 * 1024;
    private const string InternalKeyHeader = "X-RVT-Internal-Key";
    private readonly HttpClient _httpClient;
    private readonly SpaCustomerLogoClientOptions _options;

    public SpaCustomerLogoClient(HttpClient httpClient, IOptions<SpaCustomerLogoClientOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<CustomerLogo?> GetSiteLogoAsync(Guid siteId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            return null;
        }

        if (!TryBuildLogoUri(siteId, out var logoUri))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, logoUri);
            request.Headers.TryAddWithoutValidation(InternalKeyHeader, _options.InternalApiKey);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode || !IsSupportedImage(response.Content.Headers.ContentType?.MediaType))
            {
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return content.LongLength is 0 or > MaximumLogoBytes
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

    private bool TryBuildLogoUri(Guid siteId, out Uri logoUri)
    {
        logoUri = null!;
        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
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
