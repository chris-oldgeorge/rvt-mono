using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using MyAtm.Api;
namespace MyAtmMonitorTests;

[TestClass]
public class TestMonitorApiEndpoints
{
    [TestMethod]
    public void MapMyAtmMonitorApi_RegistersExpectedRoutes()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--hostBuilder:reloadConfigOnChange=false"]
        });
        WebApplication app = builder.Build();

        app.MapMyAtmMonitorApi();

        List<string?> routes = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)];

        CollectionAssert.AreEquivalent(new[]
        {
            "/liveness",
            "/readiness"
        }, routes);
    }
}
