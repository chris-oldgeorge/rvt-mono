using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace Svantek.Api;

public static class MonitorApiEndpoints
{
    public static IEndpointRouteBuilder MapSvantekMonitorApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/liveness", () => Results.Text(LivenessText(), "text/plain"));
        return endpoints;
    }

    private static string LivenessText() => RvtConfig.SERVICE_NAME + RvtConfig.SERVICE_VERSION;
}
