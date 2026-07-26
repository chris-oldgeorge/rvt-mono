using RVT.BusinessLogic.Ports.Storage;
using RvtPortal.Application.Sites.Ports;

namespace RvtPortal.Spa.Adapters.Sites;

public sealed class SiteLogoAdapter(ICustomerLogoStorage storage)
    : ISiteLogoPort
{
    public Task<bool> ExistsAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(storage.BuildProtectedLink(siteId) is not null);
    }

    public async Task<SiteLogoSaveResult> SaveAsync(
        Guid siteId,
        SiteLogoUpload upload,
        CancellationToken cancellationToken)
    {
        try
        {
            await storage.SaveAsync(
                siteId,
                new UploadedContentAdapter(upload),
                cancellationToken);
            return new SiteLogoSaveResult(SiteLogoSaveOutcome.Saved, null);
        }
        catch (StorageValidationException exception)
        {
            return new SiteLogoSaveResult(
                SiteLogoSaveOutcome.Invalid,
                exception.Message);
        }
    }

    public Task DeleteAsync(
        Guid siteId,
        CancellationToken cancellationToken) =>
        storage.DeleteAsync(siteId, cancellationToken);

    public async Task<SiteLogoFile?> OpenReadAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var file = await storage.OpenReadAsync(siteId, cancellationToken);
        return file is null
            ? null
            : new SiteLogoFile(
                file.Stream,
                file.ContentType,
                file.FileName);
    }

    private sealed class UploadedContentAdapter(SiteLogoUpload upload)
        : IUploadedContent
    {
        public string FileName => upload.FileName;
        public string ContentType => upload.ContentType;
        public long Length => upload.Length;

        public Stream OpenReadStream()
        {
            ResetContent();
            return new NonDisposingReadStream(upload.Content);
        }

        public Task CopyToAsync(
            Stream target,
            CancellationToken cancellationToken)
        {
            ResetContent();
            return upload.Content.CopyToAsync(target, cancellationToken);
        }

        private void ResetContent()
        {
            if (upload.Content.CanSeek)
            {
                upload.Content.Position = 0;
            }
        }
    }

    private sealed class NonDisposingReadStream(Stream content) : Stream
    {
        public override bool CanRead => content.CanRead;
        public override bool CanSeek => content.CanSeek;
        public override bool CanWrite => false;
        public override long Length => content.Length;

        public override long Position
        {
            get => content.Position;
            set => content.Position = value;
        }

        public override void Flush() => content.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            content.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) =>
            content.Read(buffer);

        public override long Seek(long offset, SeekOrigin origin) =>
            content.Seek(offset, origin);

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            // The caller owns the application upload stream.
            base.Dispose(disposing);
        }
    }
}
