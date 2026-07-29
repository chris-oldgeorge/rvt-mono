namespace Rvt.Storage;

public sealed class StorageReadResult(
    Stream content,
    string? contentType,
    long? length,
    IDisposable? lease = null) : IAsyncDisposable
{
    private readonly IDisposable? _lease = lease;

    public Stream Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    public string? ContentType { get; } = contentType;

    public long? Length { get; } = length;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Content.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _lease?.Dispose();
        }
    }
}
