using Microsoft.AspNetCore.Http;
using Omnidots.Api.Http;

namespace OmnidotsAdapterTests.Http;

[TestClass]
public sealed class BoundedJsonRequestReaderTests
{
    [TestMethod]
    public async Task ReadAsync_ApplicationJsonWithCharset_ReturnsExactBytes()
    {
        byte[] body = [0x7b, 0xff, 0x7d];
        var request = CreateRequest(body, "application/json; charset=utf-8");

        var result = await BoundedJsonRequestReader.ReadAsync(request, CancellationToken.None);

        CollectionAssert.AreEqual(body, result);
    }

    [DataRow(null)]
    [DataRow("")]
    [DataRow("text/plain")]
    [DataTestMethod]
    public async Task ReadAsync_MissingOrNonJsonMediaType_ThrowsUnsupportedMediaType(string? contentType)
    {
        var request = CreateRequest("{}"u8.ToArray(), contentType);

        await Assert.ThrowsExactlyAsync<OmnidotsUnsupportedMediaTypeException>(() =>
            BoundedJsonRequestReader.ReadAsync(request, CancellationToken.None));
    }

    [DataRow(null)]
    [DataRow("identity")]
    [DataTestMethod]
    public async Task ReadAsync_NoEncodingOrIdentityEncoding_ReturnsBody(string? contentEncoding)
    {
        var request = CreateRequest("{}"u8.ToArray(), "application/json");
        if (contentEncoding is not null)
        {
            request.Headers.ContentEncoding = contentEncoding;
        }

        var result = await BoundedJsonRequestReader.ReadAsync(request, CancellationToken.None);

        CollectionAssert.AreEqual("{}"u8.ToArray(), result);
    }

    [DataRow("gzip")]
    [DataRow("identity, gzip")]
    [DataTestMethod]
    public async Task ReadAsync_NonIdentityEncoding_ThrowsUnsupportedMediaType(string contentEncoding)
    {
        var request = CreateRequest("{}"u8.ToArray(), "application/json");
        request.Headers.ContentEncoding = contentEncoding;

        await Assert.ThrowsExactlyAsync<OmnidotsUnsupportedMediaTypeException>(() =>
            BoundedJsonRequestReader.ReadAsync(request, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_MultipleIdentityEncodingValues_ThrowsUnsupportedMediaType()
    {
        var request = CreateRequest("{}"u8.ToArray(), "application/json");
        request.Headers.Append("Content-Encoding", "identity");
        request.Headers.Append("Content-Encoding", "identity");

        await Assert.ThrowsExactlyAsync<OmnidotsUnsupportedMediaTypeException>(() =>
            BoundedJsonRequestReader.ReadAsync(request, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_DeclaredBodyOverLimit_ThrowsBeforeReading()
    {
        var request = CreateRequest([], "application/json");
        request.ContentLength = BoundedJsonRequestReader.MaxBodyBytes + 1;
        request.Body = new ThrowOnReadStream();

        await Assert.ThrowsExactlyAsync<OmnidotsRequestBodyTooLargeException>(() =>
            BoundedJsonRequestReader.ReadAsync(request, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_ExactlyAtLimit_ReturnsEntireBody()
    {
        var body = Enumerable.Repeat((byte)'x', BoundedJsonRequestReader.MaxBodyBytes).ToArray();
        var request = CreateRequest(body, "application/json");

        var result = await BoundedJsonRequestReader.ReadAsync(request, CancellationToken.None);

        Assert.AreEqual(BoundedJsonRequestReader.MaxBodyBytes, result.Length);
        CollectionAssert.AreEqual(body, result);
    }

    [TestMethod]
    public async Task ReadAsync_ChunkedBodyWithExtraByte_ThrowsBodyTooLarge()
    {
        var body = Enumerable.Repeat((byte)'x', BoundedJsonRequestReader.MaxBodyBytes + 1).ToArray();
        var request = CreateRequest([], "application/json");
        request.ContentLength = null;
        request.Body = new ChunkedReadStream(body, chunkSize: 997);

        await Assert.ThrowsExactlyAsync<OmnidotsRequestBodyTooLargeException>(() =>
            BoundedJsonRequestReader.ReadAsync(request, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_EarlyCancellation_PropagatesCancellation()
    {
        var request = CreateRequest([], "application/json");
        request.Body = new CancellationOnlyStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            BoundedJsonRequestReader.ReadAsync(request, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
    }

    [TestMethod]
    public void Source_DoesNotUseUnboundedStreamHelpers()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "omnidotsmonitor",
            "OmnidotsMonitor",
            "api",
            "http",
            "BoundedJsonRequestReader.cs"));

        Assert.IsFalse(source.Contains("CopyToAsync", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("ReadToEndAsync", StringComparison.Ordinal));
        Assert.IsTrue(source.Contains("ArrayPool<byte>", StringComparison.Ordinal));
    }

    private static HttpRequest CreateRequest(byte[] body, string? contentType)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentType = contentType;
        context.Request.ContentLength = body.Length;
        return context.Request;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "rvt-monitors.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class ThrowOnReadStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            throw new AssertFailedException("The declared oversized body must not be read.");
    }

    private sealed class ChunkedReadStream(byte[] body, int chunkSize) : MemoryStream(body)
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
    }

    private sealed class CancellationOnlyStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromCanceled<int>(cancellationToken);
    }
}
