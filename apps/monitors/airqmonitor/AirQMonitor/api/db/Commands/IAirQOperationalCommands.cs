using Rvt.Monitor.Common.Rules;

namespace AirQ.Api.Db;

public interface IAirQOperationalCommands
{
    void HandleException(string message, Exception exception);

    void UpdateAlertRule(RvtAlertRuleDto dto);

    void ClearErrorMessages(DateTime before);
}
