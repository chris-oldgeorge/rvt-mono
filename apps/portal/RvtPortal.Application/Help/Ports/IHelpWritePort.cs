// File summary: Defines staged persistence operations required by administrative Help use cases.
// Major updates:
// - 2026-07-28 Added the application-owned Help write port.

namespace RvtPortal.Application.Help.Ports;

public interface IHelpWritePort
{
    Task<Guid> CreateAsync(
        ValidatedHelpArticleMutation mutation,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<bool> UpdateAsync(
        Guid articleId,
        ValidatedHelpArticleMutation mutation,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<bool> SetPublicationAsync(
        Guid articleId,
        bool isPublished,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        Guid articleId,
        CancellationToken cancellationToken);
}
