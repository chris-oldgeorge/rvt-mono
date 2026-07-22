namespace Rvt.Monitor.Common.Communications;

public sealed class EmailAttachment
{
    private readonly byte[] content;

    public EmailAttachment(string fileName, string contentType, ReadOnlySpan<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (content.IsEmpty)
        {
            throw new ArgumentException("Attachment content must not be empty.", nameof(content));
        }

        FileName = fileName;
        ContentType = contentType;
        this.content = content.ToArray();
    }

    public string FileName { get; }

    public string ContentType { get; }

    public long Length => content.LongLength;

    public Stream OpenRead() => new MemoryStream(
        content,
        index: 0,
        count: content.Length,
        writable: false,
        publiclyVisible: false);
}
