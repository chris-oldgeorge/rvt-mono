using System.Reflection;

namespace Rvt.Monitor.Common.Configuration;

public interface IMonitorRuntimeDefaultsResolver
{
    MonitorRuntimeDefaults Defaults { get; }
}

public sealed class MonitorRuntimeDefaultsResolver : IMonitorRuntimeDefaultsResolver
{
    public MonitorRuntimeDefaultsResolver(string monitorKind)
    {
        Defaults = RvtConfig.ResolveMonitorDefaults(monitorKind, AppContext.BaseDirectory,
            Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty);
    }

    public MonitorRuntimeDefaults Defaults { get; }
}
