using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MyAtm.Api.Db;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace MyAtm.Api;

public static class MonitorApiEndpoints
{
    public static IEndpointRouteBuilder MapMyAtmMonitorApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/liveness", () => Results.Text(LivenessText(), "text/plain"));
        endpoints.MapGet("/readiness", ([FromServices] IMyAtmHealthQueries database) =>
            database.CanConnect()
                ? Results.Ok(new { status = "ready" })
                : Results.Json(new { status = "not-ready" }, statusCode: StatusCodes.Status503ServiceUnavailable));
        return endpoints;
    }

    private static string LivenessText() => RvtConfig.SERVICE_NAME + RvtConfig.SERVICE_VERSION;
}
