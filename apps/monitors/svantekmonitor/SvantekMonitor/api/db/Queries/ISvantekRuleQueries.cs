using Rvt.Monitor.Common.Rules;

namespace Svantek.Api.Db;

public interface ISvantekRuleQueries
{
    List<RvtAlertRuleDto> ReadRules(string? serialNumber);

    double GetAverageNoiseLevel(string serialNumber, string columnName, DateTime start, DateTime end);
}
