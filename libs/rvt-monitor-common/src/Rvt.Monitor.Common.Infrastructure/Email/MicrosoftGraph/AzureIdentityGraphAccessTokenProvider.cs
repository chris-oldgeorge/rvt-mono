using Azure;
using Azure.Core;
using Azure.Identity;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Infrastructure.Communications;

namespace Rvt.Monitor.Common.Infrastructure.Email.MicrosoftGraph;

public sealed class AzureIdentityGraphAccessTokenProvider : IMicrosoftGraphAccessTokenProvider
{
    private static readonly TokenRequestContext TokenContext =
        new(["https://graph.microsoft.com/.default"]);

    private readonly TokenCredential credential;

    public AzureIdentityGraphAccessTokenProvider(CommunicationsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        credential = new ClientSecretCredential(
            options.MicrosoftTenantId,
            options.MicrosoftClientId,
            options.MicrosoftClientSecret);
    }

    internal AzureIdentityGraphAccessTokenProvider(TokenCredential credential) =>
        this.credential = credential;

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await credential.GetTokenAsync(TokenContext, cancellationToken).ConfigureAwait(false)).Token;
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
