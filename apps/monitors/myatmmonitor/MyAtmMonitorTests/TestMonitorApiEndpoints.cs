using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using MyAtm.Api;

using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace MyAtmMonitorTests;

[TestClass]
public class TestMonitorApiEndpoints
{
    [TestMethod]
    public void MapMyAtmMonitorApi_RegistersExpectedRoutes()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--hostBuilder:reloadConfigOnChange=false"]
        });
        var app = builder.Build();

        app.MapMyAtmMonitorApi();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        CollectionAssert.AreEquivalent(new[]
        {
            "/liveness",
            "/readiness"
        }, routes);
    }
}
