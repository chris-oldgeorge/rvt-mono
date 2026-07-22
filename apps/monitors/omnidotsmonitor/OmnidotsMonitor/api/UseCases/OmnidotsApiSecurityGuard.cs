using Omnidots.Model.Config;

namespace Omnidots.Api.UseCases;

public static class OmnidotsApiSecurityGuard
{
    public static void EnsureWebhookReady(OmnidotsApiSecurityOptions? options)
    {
        if (!OmnidotsApiSecurityValidation.IsWebhookReady(options))
        {
            throw new InvalidOperationException(OmnidotsApiSecurityValidation.FailureMessage);
        }
    }

    public static void EnsureConfigurationReady(OmnidotsApiSecurityOptions? options)
    {
        if (!OmnidotsApiSecurityValidation.IsConfigurationReady(options))
        {
            throw new InvalidOperationException(OmnidotsApiSecurityValidation.FailureMessage);
        }
    }
}
