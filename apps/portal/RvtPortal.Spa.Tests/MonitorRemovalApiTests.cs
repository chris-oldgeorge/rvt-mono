using System.Reflection;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Tests;

public sealed class MonitorRemovalApiTests
{
    [Fact]
    public void MonitorsController_exposes_unattached_removal_endpoints()
    {
        var methods = typeof(MonitorsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("QueryUnattached", methods);
        Assert.Contains("GetRemovalImpact", methods);
        Assert.Contains("RemoveUnattached", methods);
    }
}
