using Microsoft.Extensions.Hosting;

namespace Rvt.Communication.TransmitSms;

public sealed class TransmitSmsStartupValidationService(TransmitSmsOptions options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options.Validate();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
