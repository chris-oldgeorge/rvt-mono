using Rvt.Monitor.Common.Data.Queries;

namespace AirQ.Api.Db.EntityFramework;

public static class AirQAggregateFields
{
    private static readonly IReadOnlyDictionary<string, MonitorAggregateField<AirQNoiseLevelEntity>> Fields =
        new Dictionary<string, MonitorAggregateField<AirQNoiseLevelEntity>>(StringComparer.Ordinal)
        {
            ["LAeq"] = MonitorAggregateField<AirQNoiseLevelEntity>.Average("LAeq", row => row.LAeq),
            ["LAmax"] = MonitorAggregateField<AirQNoiseLevelEntity>.Average("LAmax", row => row.LAmax),
            ["LAMax"] = MonitorAggregateField<AirQNoiseLevelEntity>.Average("LAmax", row => row.LAmax),
            ["LA90"] = MonitorAggregateField<AirQNoiseLevelEntity>.Average("LA90", row => row.LA90),
            ["LA10"] = MonitorAggregateField<AirQNoiseLevelEntity>.Average("LA10", row => row.LA10),
            ["LCeq"] = MonitorAggregateField<AirQNoiseLevelEntity>.Average("LCeq", row => row.LCeq),
            ["LCmax"] = MonitorAggregateField<AirQNoiseLevelEntity>.Average("LCmax", row => row.LCmax),
            ["LCMax"] = MonitorAggregateField<AirQNoiseLevelEntity>.Average("LCmax", row => row.LCmax),
            ["LC90"] = MonitorAggregateField<AirQNoiseLevelEntity>.Average("LC90", row => row.LC90),
            ["LC10"] = MonitorAggregateField<AirQNoiseLevelEntity>.Average("LC10", row => row.LC10)
        };

    public static MonitorAggregateField<AirQNoiseLevelEntity> Resolve(string fieldName)
    {
        return Fields.TryGetValue(fieldName, out var field)
            ? field
            : throw new NotSupportedException($"Unsupported AirQ aggregate field '{fieldName}'.");
    }
}
