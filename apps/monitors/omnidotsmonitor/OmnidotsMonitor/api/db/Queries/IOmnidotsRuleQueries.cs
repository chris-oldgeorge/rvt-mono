using Rvt.Monitor.Common.Rules;

namespace Omnidots.Api.Db;

public interface IOmnidotsRuleQueries
{
    /// <summary>
    /// Every site-level (unassigned) alert rule, excluding deleted ones. This
    /// monitor has no per-serial rules: the per-serial query branch that used
    /// to live here had no production caller.
    /// </summary>
    List<RvtAlertRuleDto> ReadRules();
}
