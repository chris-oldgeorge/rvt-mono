using Microsoft.Extensions.Hosting;

namespace Rvt.Monitor.Common.Infrastructure.Communications;

public sealed class CommunicationsStartupValidationService(CommunicationsOptions options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options.Validate();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
