using Rvt.Monitor.Common.Data.Queries;

namespace Omnidots.Api.Db.EntityFramework;

public static class OmnidotsAggregateFields
{
    private static readonly IReadOnlyDictionary<string, MonitorAggregateField<OmnidotsPeakLevelEntity>> Fields =
        new Dictionary<string, MonitorAggregateField<OmnidotsPeakLevelEntity>>(StringComparer.Ordinal)
        {
            ["XFdom"] = MonitorAggregateField<OmnidotsPeakLevelEntity>.Average("XFdom", row => row.XFdom),
            ["XVtop"] = MonitorAggregateField<OmnidotsPeakLevelEntity>.Average("XVtop", row => row.XVtop),
            ["XVtopOverflow"] = MonitorAggregateField<OmnidotsPeakLevelEntity>.Average("XVtopOverflow", row => row.XVtopOverflow),
            ["YFdom"] = MonitorAggregateField<OmnidotsPeakLevelEntity>.Average("YFdom", row => row.YFdom),
            ["YVtop"] = MonitorAggregateField<OmnidotsPeakLevelEntity>.Average("YVtop", row => row.YVtop),
            ["YVtopOverflow"] = MonitorAggregateField<OmnidotsPeakLevelEntity>.Average("YVtopOverflow", row => row.YVtopOverflow),
            ["ZFdom"] = MonitorAggregateField<OmnidotsPeakLevelEntity>.Average("ZFdom", row => row.ZFdom),
            ["ZVtop"] = MonitorAggregateField<OmnidotsPeakLevelEntity>.Average("ZVtop", row => row.ZVtop),
            ["ZVtopOverflow"] = MonitorAggregateField<OmnidotsPeakLevelEntity>.Average("ZVtopOverflow", row => row.ZVtopOverflow)
        };

    public static MonitorAggregateField<OmnidotsPeakLevelEntity> Resolve(string fieldName)
    {
        return Fields.TryGetValue(fieldName, out var field)
            ? field
            : throw new NotSupportedException($"Unsupported Omnidots aggregate field '{fieldName}'.");
    }
}
