using AirQ.Api.Db;

namespace AirQ.Api.UseCases;

// Summary: Prunes stored AirQ error messages older than a week.
// Major updates:
// - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitors).
public class ClearOlderErrorMessagesHandler(IAirQOperationalCommands operationalCommands)
{
    private readonly IAirQOperationalCommands _operationalCommands = operationalCommands;

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DateTime cutOff = DateTime.UtcNow.AddDays(-7);
        _operationalCommands.ClearErrorMessages(cutOff);

        return Task.CompletedTask;
    }
}
