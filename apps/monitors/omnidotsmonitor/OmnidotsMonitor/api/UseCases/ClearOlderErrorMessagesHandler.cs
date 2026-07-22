using Omnidots.Api.Db;

namespace Omnidots.Api.UseCases
{
    // Summary: Purges Omnidots error messages older than the retention cutoff.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiTraces).
    public class ClearOlderErrorMessagesHandler
    {
        private readonly IOmnidotsOperationalCommands operationalCommands;

        public ClearOlderErrorMessagesHandler(IOmnidotsOperationalCommands operationalCommands)
        {
            this.operationalCommands = operationalCommands;
        }

        public void Run()
        {

            var cutOff = DateTime.UtcNow.AddDays(-7);
            operationalCommands.ClearErrorMessages(cutOff);

        }
    }
}
