using Microsoft.Extensions.Hosting;

namespace Rvt.Communication.MicrosoftGraphMail;

public sealed class MicrosoftGraphMailStartupValidationService(MicrosoftGraphMailOptions options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options.Validate();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
