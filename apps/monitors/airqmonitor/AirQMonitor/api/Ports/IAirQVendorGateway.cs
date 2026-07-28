// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using AirQ.Model.Http;

namespace AirQ.Api.Ports;

/// <summary>
/// Driven port for the AirQ vendor API.
/// </summary>
/// <remarks>
/// The use cases depend on this abstraction rather than on the concrete
/// <c>AirQHttpGateway</c> adapter, so the import logic can be exercised and
/// substituted without the vendor transport. Every call is asynchronous and
/// cancellable: the scheduler's shutdown token has to reach the in-flight
/// vendor request for a container stop to be graceful.
/// </remarks>
public interface IAirQVendorGateway
{
    Task<List<InstrumentResponse>> GetMonitorsAsync(
        string userId,
        string userAuth,
        CancellationToken cancellationToken = default);

    Task<List<MetaDataResponse>> GetMetaDataAsync(
        string userId,
        string userAuth,
        string serialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads samples newer than <paramref name="latestDateTime"/>.
    /// </summary>
    /// <remarks>
    /// Returns the advanced watermark in the result instead of mutating a
    /// <c>ref</c> argument, which an asynchronous call cannot carry.
    /// </remarks>
    Task<LatestSamplesResult> GetLatestSamplesAsync(
        string userId,
        string userAuth,
        string serialId,
        DateTime latestDateTime,
        CancellationToken cancellationToken = default);

    Task<List<SampleResponse>> GetSamplesForDateAsync(
        string userId,
        string userAuth,
        string serialId,
        string date,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Samples newer than the supplied watermark, plus the watermark advanced
/// to the newest sample observed.
/// </summary>
public sealed record LatestSamplesResult(List<SampleResponse> Samples, DateTime LatestDateTime);
