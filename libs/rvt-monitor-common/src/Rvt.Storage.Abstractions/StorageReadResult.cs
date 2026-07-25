namespace Rvt.Storage;

public sealed class StorageReadResult : IAsyncDisposable
{
    private readonly IDisposable? lease;

    public StorageReadResult(
        Stream content,
        string? contentType,
        long? length,
        IDisposable? lease = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ContentType = contentType;
        Length = length;
        this.lease = lease;
    }

    public Stream Content { get; }

    public string? ContentType { get; }

    public long? Length { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Content.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            lease?.Dispose();
        }
    }
}
