using Rvt.Monitor.Common.Data.Queries;

namespace MyAtm.Api.Db.EntityFramework;

public static class MyAtmAggregateFields
{
    private static readonly IReadOnlyDictionary<string, MonitorAggregateField<MyAtmDustLevelEntity>> Fields =
        new Dictionary<string, MonitorAggregateField<MyAtmDustLevelEntity>>(StringComparer.Ordinal)
        {
            ["Pm1"] = MonitorAggregateField<MyAtmDustLevelEntity>.Average("Pm1", row => row.Pm1),
            ["Pm2_5"] = MonitorAggregateField<MyAtmDustLevelEntity>.Average("Pm2_5", row => row.Pm2_5),
            ["Pm10"] = MonitorAggregateField<MyAtmDustLevelEntity>.Average("Pm10", row => row.Pm10),
            ["PmTotal"] = MonitorAggregateField<MyAtmDustLevelEntity>.Average("PmTotal", row => row.PmTotal)
        };

    public static MonitorAggregateField<MyAtmDustLevelEntity> Resolve(string fieldName)
    {
        return Fields.TryGetValue(fieldName, out var field)
            ? field
            : throw new NotSupportedException($"Unsupported MyAtm aggregate field '{fieldName}'.");
    }
}
