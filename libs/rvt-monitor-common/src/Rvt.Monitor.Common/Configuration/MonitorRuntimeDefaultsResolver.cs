using System.Reflection;

namespace Rvt.Monitor.Common.Configuration;

public interface IMonitorRuntimeDefaultsResolver
{
    MonitorRuntimeDefaults Defaults { get; }
}

public sealed class MonitorRuntimeDefaultsResolver(string monitorKind) : IMonitorRuntimeDefaultsResolver
{
    public MonitorRuntimeDefaults Defaults { get; } = RvtConfig.ResolveMonitorDefaults(monitorKind, AppContext.BaseDirectory,
            Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty);
}
