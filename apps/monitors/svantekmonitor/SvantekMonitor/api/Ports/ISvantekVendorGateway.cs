// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using Svantek.Model.Http;

namespace Svantek.Api.Ports;

/// <summary>
/// Driven port for the SvanNET vendor API.
/// </summary>
/// <remarks>
/// The use cases depend on this abstraction rather than on the concrete
/// <c>SvantekHttpGateway</c> adapter, matching the AirQ and Omnidots ports, so
/// the import logic can be exercised and substituted without the vendor
/// transport. Every call is asynchronous and cancellable.
/// </remarks>
public interface ISvantekVendorGateway
{
    Task<List<Project>> GetProjectsAsync(CancellationToken cancellationToken = default);

    Task<List<ProjectFile>> GetProjectFilesAsync(
        string projectId,
        string pointId,
        string? dayCode = null,
        string? filename = null,
        CancellationToken cancellationToken = default);

    Task<List<Station>> GetStationsAsync(CancellationToken cancellationToken = default);

    Task<List<MultiData>> GetDataMultiAsync(
        string projectId,
        IList<MultiDataArgument> arguments,
        CancellationToken cancellationToken = default);

    Task<byte[]> GetSoundFileAsync(
        int project,
        int point,
        string stationType,
        string daycode,
        string serialId,
        string fileName,
        CancellationToken cancellationToken = default);
}
