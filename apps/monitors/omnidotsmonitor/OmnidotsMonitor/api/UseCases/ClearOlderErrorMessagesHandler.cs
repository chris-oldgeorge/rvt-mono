using Omnidots.Api.Db;

namespace Omnidots.Api.UseCases;

// Summary: Purges Omnidots error messages older than the retention cutoff.
// Major updates:
// - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiTraces).
public class ClearOlderErrorMessagesHandler(IOmnidotsOperationalCommands operationalCommands)
{
    private readonly IOmnidotsOperationalCommands operationalCommands = operationalCommands;

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DateTime cutOff = DateTime.UtcNow.AddDays(-7);
        operationalCommands.ClearErrorMessages(cutOff);

        return Task.CompletedTask;
    }
}
