// The namespace follows this test project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using System.Text.Json;
using Moq;
using Omnidots.Api.Http;
using Omnidots.Model.Json;

namespace OmnidotsAdapterTests.Http;

/// <summary>
/// The static-token seam used to live inside HttpWebClient's request path,
/// untested; it is now a composition-selected decorator with the same
/// behaviour, and this pins it.
/// </summary>
[TestClass]
public sealed class OmnidotsStaticTokenClientTests
{
    [TestMethod]
    public async Task PostAsync_AuthenticatePath_ReturnsTheConfiguredTokenWithoutCallingTheVendor()
    {
        Mock<IHttpClient> inner = new(MockBehavior.Strict);
        OmnidotsStaticTokenClient subject = new(inner.Object, "static-token");

        string reply = await subject.PostAsync(
            "/api/v1/user/authenticate", new StringContent("{}"), CancellationToken.None);

        TokenResponse? response = JsonSerializer.Deserialize<TokenResponse>(reply);
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Ok);
        Assert.AreEqual("static-token", response.Token);
        inner.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task PostAsync_OtherPaths_DelegateToTheInnerTransport()
    {
        using StringContent content = new("{}");
        using CancellationTokenSource cancellation = new();
        Mock<IHttpClient> inner = new(MockBehavior.Strict);
        inner.Setup(client => client.PostAsync("/api/v1/other", content, cancellation.Token))
            .ReturnsAsync("vendor-reply");
        OmnidotsStaticTokenClient subject = new(inner.Object, "static-token");

        Assert.AreEqual("vendor-reply", await subject.PostAsync("/api/v1/other", content, cancellation.Token));
        inner.VerifyAll();
    }

    [TestMethod]
    public async Task GetAsync_AlwaysDelegates()
    {
        Mock<IHttpClient> inner = new(MockBehavior.Strict);
        inner.Setup(client => client.GetAsync("/api/v1/user/authenticate", CancellationToken.None))
            .ReturnsAsync("get-reply");
        OmnidotsStaticTokenClient subject = new(inner.Object, "static-token");

        Assert.AreEqual("get-reply", await subject.GetAsync("/api/v1/user/authenticate", CancellationToken.None));
        inner.VerifyAll();
    }
}
