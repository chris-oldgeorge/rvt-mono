using Svantek.Api.Db;

namespace Svantek.Api.UseCases
{
    // Summary: Prunes stored Svantek error messages older than a week.
    // Major updates:
    // - 2026-07-30 B12: Svantek previously had no cleanup job, so the error
    //   table grew without bound; mirrors the other monitors' scheduled job.
    public class ClearOlderErrorMessagesHandler
    {
        private readonly ISvantekOperationalCommands _operationalCommands;

        public ClearOlderErrorMessagesHandler(ISvantekOperationalCommands operationalCommands)
        {
            _operationalCommands = operationalCommands;
        }

        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DateTime cutOff = DateTime.UtcNow.AddDays(-7);
            return _operationalCommands.ClearErrorMessagesAsync(cutOff, cancellationToken);
        }
    }
}
