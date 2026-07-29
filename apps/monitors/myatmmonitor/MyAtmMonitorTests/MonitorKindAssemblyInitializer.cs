// Summary: Declares this test host's monitor kind before any RvtConfig read.
// Major updates:
// - 2026-07-29 Legacy retirement step 7: RVT__MONITOR_KIND is the only kind
//   signal; the entry-assembly sniffing these test hosts leaned on is deleted.
//   A module initializer (not [AssemblyInitialize]) because [DynamicData]
//   providers run during test discovery and can snapshot RvtConfig first.

using System.Runtime.CompilerServices;

namespace MyAtmMonitorTests;

internal static class MonitorKindAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize() =>
        Environment.SetEnvironmentVariable("RVT__MONITOR_KIND", "myatm");
}
