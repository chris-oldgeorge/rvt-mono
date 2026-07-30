using Rvt.Monitor.Common.Rules;

namespace Svantek.Api.Db;

public interface ISvantekRuleQueries
{
    List<RvtAlertRuleDto> ReadRules(string? serialNumber);

    /// <summary>
    /// Returns the aggregate over the window, or <c>null</c> when the window
    /// holds no samples. Callers must skip rule evaluation on <c>null</c>: a
    /// fabricated 0.0 dB reads as silence and clears every latched rule.
    /// </summary>
    double? GetAverageNoiseLevel(string serialNumber, string columnName, DateTime start, DateTime end);
}
