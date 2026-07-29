namespace Omnidots.Api.UseCases;

public sealed class OmnidotsWebhookAuthenticationException : Exception
{
    public OmnidotsWebhookAuthenticationException() : base("Webhook authentication failed.")
    {
    }
}
