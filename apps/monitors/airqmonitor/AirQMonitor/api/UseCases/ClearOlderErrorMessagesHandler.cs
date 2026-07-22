using AirQ.Api.Db;

namespace AirQ.Api.UseCases
{
    // Summary: Prunes stored AirQ error messages older than a week.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitors).
    public class ClearOlderErrorMessagesHandler
    {
        private readonly IAirQOperationalCommands operationalCommands;

        public ClearOlderErrorMessagesHandler(IAirQOperationalCommands operationalCommands)
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
