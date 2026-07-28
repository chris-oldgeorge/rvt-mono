using Omnidots.Model.Json;

namespace Omnidots.Api.Ports
{
    /// <summary>
    /// Driven port for the Omnidots Honeycomb vendor API.
    /// </summary>
    /// <remarks>
    /// The import use cases depend on this abstraction rather than on the
    /// concrete <c>OmnidotsHttpGateway</c> adapter, so the vibration import
    /// logic can be exercised and substituted without the vendor transport.
    /// Every call is asynchronous and cancellable: the scheduler's shutdown
    /// token has to reach the in-flight vendor request for a container stop to
    /// be graceful.
    /// </remarks>
    public interface IOmnidotsVendorGateway
    {
        Task<TokenResponse> AuthenticateAsync(CancellationToken cancellationToken = default);

        Task<MeasuringPointsResponse> ListMeasuringPointsAsync(CancellationToken cancellationToken = default);

        Task<PeakRecords> GetPeakRecordsAsync(
            string token,
            DateTime startTime,
            DateTime? endTime,
            string measuringPointId,
            CancellationToken cancellationToken = default);

        Task<VeffRecords> GetVeffRecordsAsync(
            string token,
            DateTime startTime,
            DateTime? endTime,
            string measuringPointId,
            CancellationToken cancellationToken = default);

        Task<VdvRecords> GetVdvRecordsAsync(
            string token,
            DateTime startTime,
            DateTime? endTime,
            string measuringPointId,
            CancellationToken cancellationToken = default);

        Task<TracesListResponse> GetTracesListAsync(
            string token,
            string measuringPointId,
            DateTime startTime,
            DateTime? endTime,
            CancellationToken cancellationToken = default);

        Task<TracesReponse> GetTracesAsync(
            string token,
            string measuringPointId,
            DateTime startTime,
            DateTime? endTime,
            CancellationToken cancellationToken = default);

        Task<OmnidotsResponse> ConfigureMeasuringPointAsync(
            string token,
            string measuringPointId,
            string json,
            CancellationToken cancellationToken = default);
    }
}
