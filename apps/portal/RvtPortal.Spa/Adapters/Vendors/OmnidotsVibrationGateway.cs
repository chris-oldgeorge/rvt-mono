// File summary: Provides the outbound HTTP adapter for synchronizing vibration alert levels with the Omnidots vendor.
// Major updates:
// - 2026-07-15 pending Moved the Omnidots vibration HTTP integration behind IVibrationVendorGateway.
//   Note: the vendor call runs inside the alert-level command transaction, before the database write. A later
//   database rollback leaves the vendor holding the new levels until the next successful update; making the two
//   atomic needs post-commit/outbox dispatch and is deliberately out of scope for this refactor.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RVT.BusinessLogic.Ports.Vendors;

namespace RvtPortal.Spa.Adapters.Vendors;

public sealed class OmnidotsAdapterOptions
{
    public string? Url { get; set; }
    public string? Secret { get; set; }
}

public sealed class OmnidotsVibrationGateway : IVibrationVendorGateway
{
    private const string ContentType = "application/json";
    private readonly HttpClient httpClient;
    private readonly OmnidotsAdapterOptions options;

    // Function summary: Initializes this type with the HTTP client and configured Omnidots adapter values.
    public OmnidotsVibrationGateway(HttpClient httpClient, IOptions<OmnidotsAdapterOptions> options)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
    }

    // Function summary: Posts the alert/caution level pair to the configured Omnidots adapter endpoint.
    public async Task<VendorSyncResult> UpdateAlertLevelsAsync(
        string serialId,
        double alertLevel,
        double cautionLevel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
        {
            return VendorSyncResult.Failure("Omnidots adapter secret is not configured.");
        }

        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var endpoint))
        {
            return VendorSyncResult.Failure("Omnidots adapter URL is invalid.");
        }

        var payload = new Dictionary<string, object>
        {
            ["secret"] = options.Secret,
            ["serialid"] = serialId,
            ["level_caution"] = cautionLevel,
            ["level_alert"] = alertLevel
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, ContentType);
        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(endpoint, content, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return VendorSyncResult.Failure("Omnidots adapter timed out.");
        }
        catch (HttpRequestException)
        {
            return VendorSyncResult.Failure("Omnidots adapter could not be reached.");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return VendorSyncResult.Success();
            }

            return VendorSyncResult.Failure($"Omnidots adapter returned HTTP {(int)response.StatusCode}.");
        }
    }
}
