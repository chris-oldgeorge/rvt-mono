using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Svantek.Api;

using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace SvantekMonitorTests;

[TestClass]
public class TestMonitorApiEndpoints
{
    [TestMethod]
    public void MapSvantekMonitorApi_RegistersExpectedRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        app.MapSvantekMonitorApi();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        CollectionAssert.AreEquivalent(new[]
        {
            "/liveness"
        }, routes);
    }
}
