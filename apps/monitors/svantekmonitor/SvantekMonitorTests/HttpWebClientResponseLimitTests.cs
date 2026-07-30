using System.Net;
using Rvt.Monitor.Common.Diagnostics;
using Svantek.Api.Http;

namespace SvantekMonitorTests;

[TestClass]
public sealed class HttpWebClientResponseLimitTests
{
    [TestMethod]
    public async Task GetAsync_RejectsJsonResponseLargerThanFourMiB()
    {
        ByteArrayContent content = OversizedContent((4 * 1024 * 1024) + 1);
        using HttpClient client = Client(content);
        HttpWebClient subject = new("https://vendor.example.test/", client);

        await Assert.ThrowsExactlyAsync<AdapterException>(
            () => subject.GetAsync("/projects-get-data.php", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task PostForBytesAsync_RejectsRecordingLargerThanSixtyFourMiB()
    {
        ByteArrayContent responseContent = OversizedContent((64 * 1024 * 1024) + 1);
        using HttpClient client = Client(responseContent);
        HttpWebClient subject = new("https://vendor.example.test/", client);
        using MultipartFormDataContent requestContent = new();

        await Assert.ThrowsExactlyAsync<AdapterException>(
            () => subject.PostForBytesAsync(
                "/projects-get-data.php",
                requestContent,
                TestContext.CancellationToken));
    }

    private static ByteArrayContent OversizedContent(long declaredLength)
    {
        ByteArrayContent content = new([1]);
        content.Headers.ContentLength = declaredLength;
        return content;
    }

    private static HttpClient Client(HttpContent content) =>
        new(new ResponseHandler(content))
        {
            BaseAddress = new Uri("https://vendor.example.test/")
        };

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

    public TestContext TestContext { get; set; } = null!;
}
