using Rvt.Monitor.Common.Data.Queries;

namespace Svantek.Api.Db.EntityFramework;

public static class SvantekAggregateFields
{
    private static readonly IReadOnlyDictionary<string, MonitorAggregateField<SvantekNoiseLevelEntity>> Fields =
        new Dictionary<string, MonitorAggregateField<SvantekNoiseLevelEntity>>(StringComparer.Ordinal)
        {
            ["LAeq"] = MonitorAggregateField<SvantekNoiseLevelEntity>.Average("LAeq", row => row.LAeq),
            ["LAmax"] = MonitorAggregateField<SvantekNoiseLevelEntity>.Maximum("LAmax", row => row.LAmax),
            ["LAMax"] = MonitorAggregateField<SvantekNoiseLevelEntity>.Maximum("LAMax", row => row.LAmax),
            ["LA90"] = MonitorAggregateField<SvantekNoiseLevelEntity>.Average("LA90", row => row.LA90),
            ["LA10"] = MonitorAggregateField<SvantekNoiseLevelEntity>.Average("LA10", row => row.LA10),
            ["LCeq"] = MonitorAggregateField<SvantekNoiseLevelEntity>.Average("LCeq", row => row.LCeq),
            ["LCmax"] = MonitorAggregateField<SvantekNoiseLevelEntity>.Maximum("LCmax", row => row.LCmax),
            ["LCMax"] = MonitorAggregateField<SvantekNoiseLevelEntity>.Maximum("LCMax", row => row.LCmax),
            ["LC90"] = MonitorAggregateField<SvantekNoiseLevelEntity>.Average("LC90", row => row.LC90),
            ["LC10"] = MonitorAggregateField<SvantekNoiseLevelEntity>.Average("LC10", row => row.LC10)
        };

    public static MonitorAggregateField<SvantekNoiseLevelEntity> Resolve(string fieldName)
    {
        return Fields.TryGetValue(fieldName, out var field)
            ? field
            : throw new NotSupportedException($"Unsupported Svantek aggregate field '{fieldName}'.");
    }
}
