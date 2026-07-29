using MyAtm.Api.Db;

namespace MyAtm.Api.UseCases
{
    // Summary: Prunes stored error messages older than a week.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the MyAtmApi partials (MyAtmApiMonitors).
    public class ClearOlderErrorMessagesHandler
    {
        private readonly IMyAtmOperationalCommands _operationalCommands;

        public ClearOlderErrorMessagesHandler(IMyAtmOperationalCommands operationalCommands)
        {
            _operationalCommands = operationalCommands;
        }

        public void Run()
        {
            DateTime cutOff = DateTime.UtcNow.AddDays(-7);
            _operationalCommands.ClearErrorMessages(cutOff);
        }
    }
}
