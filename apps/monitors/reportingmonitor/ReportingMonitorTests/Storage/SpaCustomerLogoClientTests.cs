using System.Net;
using Microsoft.Extensions.Options;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Storage.PortalContent;

namespace ReportingMonitorTests.Storage;

public sealed class SpaCustomerLogoClientTests
{
    private const int _maximumLogoBytes = 2 * 1024 * 1024;

    [Fact]
    public async Task GetSiteLogoAsync_RejectsDeclaredOversizedResponseBeforeReading()
    {
        TrackingContent content = new([1]);
        content.Headers.ContentLength = _maximumLogoBytes + 1;
        content.Headers.ContentType = new("image/png");
        SpaCustomerLogoClient subject = CreateClient(content);

        CustomerLogo? result = await subject.GetSiteLogoAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Null(result);
        Assert.False(content.WasRead);
    }

    [Fact]
    public async Task GetSiteLogoAsync_StopsChunkedResponseAtTheLimit()
    {
        GeneratedReadStream stream = new(_maximumLogoBytes + 81920);
        StreamContent content = new(stream);
        content.Headers.ContentType = new("image/png");
        SpaCustomerLogoClient subject = CreateClient(content);

        CustomerLogo? result = await subject.GetSiteLogoAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(_maximumLogoBytes + 1, stream.BytesRead);
    }

    [Fact]
    public async Task GetSiteLogoAsync_ReturnsSupportedLogoWithinTheLimit()
    {
        byte[] expected = [1, 2, 3, 4];
        ByteArrayContent content = new(expected);
        content.Headers.ContentType = new("image/png");
        SpaCustomerLogoClient subject = CreateClient(content);

        CustomerLogo? result = await subject.GetSiteLogoAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Content);
        Assert.Equal("image/png", result.ContentType);
    }

    private static SpaCustomerLogoClient CreateClient(HttpContent content)
    {
        HttpClient client = new(new ResponseHandler(content));
        IOptions<SpaCustomerLogoClientOptions> options = Options.Create(
            new SpaCustomerLogoClientOptions
            {
                BaseUrl = "https://portal.example.test/",
                InternalApiKey = nameof(SpaCustomerLogoClientTests)
            });
        return new SpaCustomerLogoClient(client, options);
    }

    private sealed class ResponseHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });
    }

    private sealed class TrackingContent(byte[] value) : HttpContent
    {
        public bool WasRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasRead = true;
            return stream.WriteAsync(value).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = value.Length;
            return true;
        }
    }

    private sealed class GeneratedReadStream(long length) : Stream
    {
        private long _remaining = length;

        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = (int)Math.Min(count, _remaining);
            Array.Clear(buffer, offset, read);
            _remaining -= read;
            BytesRead += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = (int)Math.Min(buffer.Length, _remaining);
            buffer.Span[..read].Clear();
            _remaining -= read;
            BytesRead += read;
            return ValueTask.FromResult(read);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
