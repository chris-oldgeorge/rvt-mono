using Rvt.Monitor.Common.Rules;

namespace Omnidots.Api.Db;

public interface IOmnidotsRuleQueries
{
    List<RvtAlertRuleDto> ReadRules(string? serialId);
}
