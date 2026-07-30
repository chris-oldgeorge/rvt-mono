using System.Net;
using AirQ.Api.Http;
using Rvt.Monitor.Common.Diagnostics;

namespace AirQMonitorTests;

[TestClass]
public sealed class HttpWebClientResponseLimitTests
{
    [TestMethod]
    public async Task GetAsync_RejectsResponseLargerThanFourMiB()
    {
        ByteArrayContent content = new([1]);
        content.Headers.ContentLength = (4 * 1024 * 1024) + 1;
        using HttpClient client = new(new ResponseHandler(content));
        HttpWebClient subject = new("https://vendor.example.test/", client);

        await Assert.ThrowsExactlyAsync<AdapterException>(
            () => subject.GetAsync("/latestData", TestContext.CancellationToken));
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

    public TestContext TestContext { get; set; } = null!;
}
