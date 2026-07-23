namespace RvtPortal.Application.Sites.Ports;

public sealed record SiteLogoUpload(
    Stream Content,
    long Length,
    string ContentType,
    string FileName);

public sealed record SiteLogoFile(
    Stream Content,
    string ContentType,
    string FileName);

public enum SiteLogoSaveOutcome
{
    Saved,
    Invalid
}

public sealed record SiteLogoSaveResult(
    SiteLogoSaveOutcome Outcome,
    string? Message);

public interface ISiteLogoPort
{
    Task<bool> ExistsAsync(
        Guid siteId,
        CancellationToken cancellationToken);

    Task<SiteLogoSaveResult> SaveAsync(
        Guid siteId,
        SiteLogoUpload upload,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        Guid siteId,
        CancellationToken cancellationToken);

    Task<SiteLogoFile?> OpenReadAsync(
        Guid siteId,
        CancellationToken cancellationToken);
}
