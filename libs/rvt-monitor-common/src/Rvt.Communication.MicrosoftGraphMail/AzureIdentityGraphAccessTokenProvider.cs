using Azure;
using Azure.Core;
using Azure.Identity;
using Rvt.Communication.Abstractions;

namespace Rvt.Communication.MicrosoftGraphMail;

public sealed class AzureIdentityGraphAccessTokenProvider : IMicrosoftGraphAccessTokenProvider
{
    private static readonly TokenRequestContext TokenContext =
        new(["https://graph.microsoft.com/.default"]);

    private readonly Lazy<TokenCredential> credential;

    public AzureIdentityGraphAccessTokenProvider(MicrosoftGraphMailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        credential = new Lazy<TokenCredential>(() => new ClientSecretCredential(
            options.TenantId,
            options.ClientId,
            options.ClientSecret));
    }

    internal AzureIdentityGraphAccessTokenProvider(TokenCredential credential) =>
        this.credential = new Lazy<TokenCredential>(() => credential);

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await credential.Value.GetTokenAsync(TokenContext, cancellationToken).ConfigureAwait(false)).Token;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AuthenticationFailedException exception)
        {
            var requestFailure = exception.InnerException as RequestFailedException;
            var kind = requestFailure is { Status: 408 or 429 } || requestFailure?.Status >= 500
                ? DeliveryFailureKind.Transient
                : DeliveryFailureKind.Permanent;
            throw new EmailDeliveryException(
                "MicrosoftGraph",
                kind,
                requestFailure?.Status.ToString() ?? "Authentication");
        }
        catch (RequestFailedException exception)
        {
            var kind = exception.Status is 408 or 429 || exception.Status >= 500
                ? DeliveryFailureKind.Transient
                : DeliveryFailureKind.Permanent;
            throw new EmailDeliveryException(
                "MicrosoftGraph",
                kind,
                exception.Status.ToString());
        }
    }
}
