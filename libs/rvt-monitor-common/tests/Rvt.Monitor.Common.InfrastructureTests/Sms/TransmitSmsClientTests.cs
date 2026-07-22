using System.Net;
using System.Text;
using Rvt.Monitor.Common.Infrastructure.Sms;

namespace Rvt.Monitor.Common.InfrastructureTests.Sms;

[TestClass]
public sealed class TransmitSmsClientTests
{
    [TestMethod]
    public async Task SendAsync_PostsFormRequestWithBasicAuthentication()
    {
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """{"error":{"code":"SUCCESS","description":"OK"}}""");
        using var httpClient = new HttpClient(handler);
        var client = new TransmitSmsClient(
            httpClient,
            new Uri("https://api.transmitsms.com/send-sms.json"));

        await client.SendAsync(
            new TransmitSmsRequest(
                "api-key",
                "api-secret",
                "447700900123",
                "Threshold breached",
                "KrakenAlert"),
            CancellationToken.None);

        Assert.IsNotNull(handler.Request);
        Assert.AreEqual(HttpMethod.Post, handler.Request.Method);
        Assert.AreEqual("https://api.transmitsms.com/send-sms.json", handler.Request.RequestUri?.ToString());
        Assert.AreEqual(
            "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("api-key:api-secret")),
            handler.Request.Headers.Authorization?.ToString());
        Assert.AreEqual(
            "message=Threshold+breached&to=447700900123&from=KrakenAlert",
            handler.RequestBody);
    }

    [TestMethod]
    public async Task SendAsync_ApiFailureRetainsCodeButNotRawDescription()
    {
        const string rawDescription = "Invalid recipient raw-private-data";
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            "{\"error\":{\"code\":\"FIELD_INVALID\",\"description\":\"" + rawDescription + "\"}}");
        using var httpClient = new HttpClient(handler);
        var client = new TransmitSmsClient(httpClient);

        var exception = await Assert.ThrowsExactlyAsync<TransmitSmsException>(() =>
            client.SendAsync(
                new TransmitSmsRequest("api-key", "api-secret", "bad", "private-body", null),
                CancellationToken.None));

        Assert.AreEqual("FIELD_INVALID", exception.Code);
        Assert.IsNull(exception.StatusCode);
        Assert.DoesNotContain(rawDescription, exception.Message);
        Assert.DoesNotContain("private-body", exception.Message);
    }

    internal sealed class CapturingHandler(
        HttpStatusCode statusCode,
        string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
