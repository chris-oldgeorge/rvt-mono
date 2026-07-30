using Rvt.Monitor.Common.Rules;

namespace AirQ.Api.Db;

public interface IAirQRuleQueries
{
    List<RvtAlertRuleDto> ReadRules(string? serialNumber);

    double GetAverageNoiseLevel(string serialNumber, string columnName, DateTime start, DateTime end);
}
