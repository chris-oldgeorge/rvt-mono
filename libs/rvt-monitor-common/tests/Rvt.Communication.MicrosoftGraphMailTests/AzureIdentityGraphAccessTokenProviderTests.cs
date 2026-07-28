using System.Net.Sockets;
using Azure;
using Azure.Core;
using Azure.Identity;
using Rvt.Communication.Abstractions;
using Rvt.Communication.MicrosoftGraphMail;

namespace Rvt.Communication.MicrosoftGraphMailTests;

[TestClass]
public sealed class AzureIdentityGraphAccessTokenProviderTests
{
    [TestMethod]
    public async Task GetAccessTokenAsync_ReturnsTokenFromCredential()
    {
        var provider = CreateProvider(new StubTokenCredential(new AccessToken("graph-token", DateTimeOffset.MaxValue)));

        var token = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.AreEqual("graph-token", token);
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_WhenIdentityEndpointIsUnreachable_ClassifiesTransient()
    {
        // A wrapped transport fault carries no RequestFailedException, and must
        // not be mistaken for a rejected credential.
        var failure = new AuthenticationFailedException(
            "ClientSecretCredential authentication failed.",
            new HttpRequestException("No such host is known (login.microsoftonline.com:443)."));

        var exception = await AssertDeliveryFailureAsync(failure);

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.AreEqual("Authentication", exception.Code);
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_WhenIdentityEndpointTimesOut_ClassifiesTransient()
    {
        var failure = new AuthenticationFailedException(
            "ClientSecretCredential authentication failed.",
            new TaskCanceledException("The request timed out.", new TimeoutException()));

        var exception = await AssertDeliveryFailureAsync(failure);

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_WhenSocketFailsDeeperInTheChain_ClassifiesTransient()
    {
        var failure = new AuthenticationFailedException(
            "ClientSecretCredential authentication failed.",
            new InvalidOperationException(
                "Transport failure.",
                new SocketException((int)SocketError.ConnectionRefused)));

        var exception = await AssertDeliveryFailureAsync(failure);

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_WhenCredentialIsRejected_ClassifiesPermanent()
    {
        var failure = new AuthenticationFailedException(
            "The provided client secret is invalid.",
            new RequestFailedException(401, "AADSTS7000215: Invalid client secret provided."));

        var exception = await AssertDeliveryFailureAsync(failure);

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
        Assert.AreEqual("401", exception.Code);
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_WhenNoTransportCauseIsPresent_ClassifiesPermanent()
    {
        var failure = new AuthenticationFailedException("Tenant not found.");

        var exception = await AssertDeliveryFailureAsync(failure);

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_WhenIdentityThrottles_ClassifiesTransient()
    {
        var failure = new AuthenticationFailedException(
            "Throttled.",
            new RequestFailedException(429, "Too many requests."));

        var exception = await AssertDeliveryFailureAsync(failure);

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.AreEqual("429", exception.Code);
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var provider = CreateProvider(new StubTokenCredential(
            new OperationCanceledException(cancellation.Token)));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => provider.GetAccessTokenAsync(cancellation.Token).AsTask());
    }

    private static async Task<EmailDeliveryException> AssertDeliveryFailureAsync(Exception credentialFailure)
    {
        var provider = CreateProvider(new StubTokenCredential(credentialFailure));

        return await Assert.ThrowsExactlyAsync<EmailDeliveryException>(
            () => provider.GetAccessTokenAsync(CancellationToken.None).AsTask());
    }

    private static AzureIdentityGraphAccessTokenProvider CreateProvider(TokenCredential credential) =>
        new(credential);

    private sealed class StubTokenCredential : TokenCredential
    {
        private readonly AccessToken token;
        private readonly Exception? failure;

        public StubTokenCredential(AccessToken token) => this.token = token;

        public StubTokenCredential(Exception failure) => this.failure = failure;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            failure is null ? token : throw failure;

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            failure is null ? ValueTask.FromResult(token) : throw failure;
    }
}
