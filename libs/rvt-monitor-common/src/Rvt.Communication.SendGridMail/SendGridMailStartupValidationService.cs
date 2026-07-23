using Microsoft.Extensions.Hosting;

namespace Rvt.Communication.SendGridMail;

public sealed class SendGridMailStartupValidationService(SendGridMailOptions options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options.Validate();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
