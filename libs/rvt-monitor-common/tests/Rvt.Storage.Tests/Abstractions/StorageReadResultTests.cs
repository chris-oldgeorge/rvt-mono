using Rvt.Storage;

namespace Rvt.Storage.Tests.Abstractions;

[TestClass]
public sealed class StorageReadResultTests
{
    [TestMethod]
    public async Task DisposeAsync_DisposesContentThenProviderLease()
    {
        List<string> events = new List<string>();
        RecordingStream content = new RecordingStream(events, throwOnDispose: true);
        RecordingLease lease = new RecordingLease(events);
        StorageReadResult result = new StorageReadResult(content, "audio/wav", 42, lease);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await result.DisposeAsync());

        CollectionAssert.AreEqual(new[] { "content", "lease" }, events);
    }

    private sealed class RecordingStream(List<string> events, bool throwOnDispose) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask DisposeAsync()
        {
            events.Add("content");
            return throwOnDispose
                ? ValueTask.FromException(new InvalidOperationException("Simulated content disposal failure."))
                : ValueTask.CompletedTask;
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class RecordingLease(List<string> events) : IDisposable
    {
        public void Dispose() => events.Add("lease");
    }
}
