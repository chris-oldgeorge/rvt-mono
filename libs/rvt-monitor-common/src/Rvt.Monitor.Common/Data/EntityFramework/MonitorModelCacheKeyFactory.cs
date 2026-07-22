using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Rvt.Monitor.Common.Data.EntityFramework;

public sealed class MonitorModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        return context is MonitorDbContextBase monitorContext
            ? (context.GetType(), monitorContext.ModelCacheProvider, IdentifierMapKey(monitorContext), designTime)
            : (context.GetType(), designTime);
    }

    private static string IdentifierMapKey(MonitorDbContextBase monitorContext)
    {
        return string.Join(
            "\u001f",
            monitorContext.ModelCacheIdentifierMap
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ThenBy(pair => pair.Value, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}\u001e{pair.Value}"));
    }
}
