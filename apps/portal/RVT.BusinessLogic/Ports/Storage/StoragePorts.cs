// File summary: Defines transport-neutral storage ports used by RVT business workflows.
// Major updates:
// - 2026-07-08 pending Introduced storage ports for the hexagonal-at-the-edges refactor.
// - 2026-07-08 pending Added monitor-picture cleanup for compensating failed database persistence.

namespace RVT.BusinessLogic.Ports.Storage;

public interface IUploadedContent
{
    string FileName { get; }
    string ContentType { get; }
    long Length { get; }

    // Function summary: Opens the uploaded content for validation or persistence.
    Stream OpenReadStream();

    // Function summary: Copies uploaded content to an adapter-owned storage stream.
    Task CopyToAsync(Stream target, CancellationToken cancellationToken);
}

public sealed record StoredContentFile(Stream Stream, string ContentType, string FileName);

public interface ICustomerLogoStorage
{
    // Function summary: Saves a site customer logo and replaces any prior logo for that site.
    Task SaveAsync(Guid siteId, IUploadedContent logo, CancellationToken cancellationToken);

    // Function summary: Opens a protected customer-logo stream for an authorized caller.
    Task<StoredContentFile?> OpenReadAsync(Guid siteId, CancellationToken cancellationToken);

    // Function summary: Deletes any stored customer logo for a site.
    Task DeleteAsync(Guid siteId, CancellationToken cancellationToken);

    // Function summary: Builds the API-facing logo link when a logo exists for a site.
    string? BuildProtectedLink(Guid siteId);
}

public interface IMonitorPictureStorage
{
    // Function summary: Saves an uploaded monitor picture and returns the stored reference kept on the deployment row.
    Task<string> SaveAsync(Guid deploymentId, IUploadedContent picture, CancellationToken cancellationToken);

    // Function summary: Deletes a stored monitor picture reference when database persistence cannot keep it.
    Task DeleteAsync(string? storedLink, CancellationToken cancellationToken);

    // Function summary: Opens a protected monitor picture stream for an authorized caller.
    Task<StoredContentFile?> OpenReadAsync(string? storedLink, CancellationToken cancellationToken);

    // Function summary: Builds the API-facing picture link when a stored reference must remain protected.
    string? BuildProtectedLink(Guid monitorId, string? storedLink);
}

public sealed class StorageValidationException : InvalidOperationException
{
    // Function summary: Creates a validation exception suitable for upload problem responses.
    public StorageValidationException(string message)
        : base(message)
    {
    }
}
