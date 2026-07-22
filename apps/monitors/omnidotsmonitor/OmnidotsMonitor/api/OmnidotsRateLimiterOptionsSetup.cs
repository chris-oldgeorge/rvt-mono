using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Omnidots.Model.Config;

namespace Omnidots.Api;

internal sealed class OmnidotsRateLimiterOptionsSetup(
    IOptions<OmnidotsApiSecurityOptions> securityOptions)
    : IConfigureOptions<RateLimiterOptions>
{
    public const string WebhookPolicy = "OmnidotsWebhook";
    public const string ConfigurePolicy = "OmnidotsConfigure";

    public void Configure(RateLimiterOptions options)
    {
        var security = securityOptions.Value;
        if (security.WebhookConcurrencyLimit <= 0 || security.ConfigureConcurrencyLimit <= 0)
        {
            throw new InvalidOperationException("Omnidots API concurrency limits are invalid.");
        }

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = static (context, _) => new ValueTask(
            Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many requests.")
                .ExecuteAsync(context.HttpContext));
        options.AddConcurrencyLimiter(WebhookPolicy, limiter =>
        {
            limiter.PermitLimit = security.WebhookConcurrencyLimit;
            limiter.QueueLimit = 0;
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });
        options.AddConcurrencyLimiter(ConfigurePolicy, limiter =>
        {
            limiter.PermitLimit = security.ConfigureConcurrencyLimit;
            limiter.QueueLimit = 0;
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });
    }
}
