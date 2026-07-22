// File summary: Adapts ASP.NET Core form-file uploads to transport-neutral business storage ports.
// Major updates:
// - 2026-07-08 pending Added form-file upload adapter for the hexagonal-at-the-edges refactor.

using RVT.BusinessLogic.Ports.Storage;

namespace RvtPortal.Spa.Adapters.Storage;

public sealed class FormFileUpload : IUploadedContent
{
    private readonly IFormFile file;

    // Function summary: Wraps an inbound multipart form file for business-layer storage ports.
    public FormFileUpload(IFormFile file)
    {
        this.file = file;
    }

    public string FileName => file.FileName;

    public string ContentType => file.ContentType;

    public long Length => file.Length;

    public Stream OpenReadStream()
    {
        return file.OpenReadStream();
    }

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken)
    {
        return file.CopyToAsync(target, cancellationToken);
    }
}
