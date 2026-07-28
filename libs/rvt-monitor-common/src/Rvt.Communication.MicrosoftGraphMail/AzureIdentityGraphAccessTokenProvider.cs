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
            RequestFailedException? requestFailure = FindInner<RequestFailedException>(exception);
            DeliveryFailureKind kind;
            if (requestFailure is not null)
            {
                kind = ClassifyStatus(requestFailure.Status);
            }
            else
            {
                // A token request that never reached a response — the identity
                // endpoint was unreachable or timed out — is a transport fault,
                // not a rejected credential, so it must stay retryable.
                kind = IsTransportFailure(exception)
                    ? DeliveryFailureKind.Transient
                    : DeliveryFailureKind.Permanent;
            }

            throw new EmailDeliveryException(
                "MicrosoftGraph",
                kind,
                requestFailure?.Status.ToString() ?? "Authentication");
        }
        catch (RequestFailedException exception)
        {
            throw new EmailDeliveryException(
                "MicrosoftGraph",
                ClassifyStatus(exception.Status),
                exception.Status.ToString());
        }
    }

    private static DeliveryFailureKind ClassifyStatus(int status) =>
        status is 408 or 429 || status >= 500
            ? DeliveryFailureKind.Transient
            : DeliveryFailureKind.Permanent;

    private static bool IsTransportFailure(Exception exception)
    {
        for (Exception? current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException
                or System.Net.Sockets.SocketException
                or IOException
                or TimeoutException
                or TaskCanceledException)
            {
                return true;
            }
        }

        return false;
    }

    private static TException? FindInner<TException>(Exception exception)
        where TException : Exception
    {
        for (Exception? current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is TException match)
            {
                return match;
            }
        }

        return null;
    }
}
