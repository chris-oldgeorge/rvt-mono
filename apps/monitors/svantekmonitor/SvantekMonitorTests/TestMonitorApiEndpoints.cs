using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Svantek.Api;
namespace SvantekMonitorTests;

[TestClass]
public class TestMonitorApiEndpoints
{
    [TestMethod]
    public void MapSvantekMonitorApi_RegistersExpectedRoutes()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        WebApplication app = builder.Build();

        app.MapSvantekMonitorApi();

        List<string?> routes = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)];

        CollectionAssert.AreEquivalent(new[]
        {
            "/liveness"
        }, routes);
    }
}
