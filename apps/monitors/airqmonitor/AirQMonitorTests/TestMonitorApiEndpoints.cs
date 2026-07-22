using System.Net;
using System.Text;
using AirQ.Api;
using AirQ.Api.UseCases;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace AirQMonitorTests;

[TestClass]
public class TestMonitorApiEndpoints
{
    [TestMethod]
    public void MapAirQMonitorApi_RejectsMissingApiKeyConfiguration()
    {
        using var app = CreateApp(apiKey: null, "RVT:MONITOR_API_KEY", new Mock<IAirQDateImporter>().Object);

        Assert.Throws<InvalidOperationException>(() => app.MapAirQMonitorApi());
    }

    [TestMethod]
    public void MapAirQMonitorApi_RegistersLivenessAndOnlyProtectedPostImportRoute()
    {
        using var app = CreateApp("monitor-api-key", "RVT__MONITOR_API_KEY", new Mock<IAirQDateImporter>().Object);
        app.MapAirQMonitorApi();

        var import = GetRoute(app, "/store-noise-levels-for-date");
        CollectionAssert.AreEquivalent(new[] { "POST" }, import.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.ToList());
        Assert.IsNull(((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().SingleOrDefault(endpoint => endpoint.RoutePattern.RawText == "/store-noise-levels-for-date" &&
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("GET")));
    }

    [TestMethod]
    public async Task StoreNoiseLevelsForDate_ReturnsUnauthorizedBeforeParsingMissingOrMalformedBodies()
    {
        var importer = new Mock<IAirQDateImporter>(MockBehavior.Strict);
        await using var app = await StartAppAsync("monitor-api-key", importer.Object);
        using var client = app.GetTestClient();

        foreach (var suppliedKey in new string?[] { null, "wrong-api-key" })
        {
            foreach (var body in new[] { string.Empty, "{" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/store-noise-levels-for-date")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                if (suppliedKey is not null)
                {
                    request.Headers.Add("X-Api-Key", suppliedKey);
                }

                using var response = await client.SendAsync(request);
                Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }

        importer.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task StoreNoiseLevelsForDate_ReturnsBadRequestForMissingMalformedOrNonCanonicalDate()
    {
        var importer = new Mock<IAirQDateImporter>(MockBehavior.Strict);
        await using var app = await StartAppAsync("monitor-api-key", importer.Object);
        using var client = app.GetTestClient();

        foreach (var body in new[] { string.Empty, "{", "{}", "{\"date\":\"2026-7-14\"}" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/store-noise-levels-for-date")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Api-Key", "monitor-api-key");

            using var response = await client.SendAsync(request);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        importer.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task StoreNoiseLevelsForDate_DispatchesOnlyCanonicalDateAfterApiKeyValidation()
    {
        var importer = new Mock<IAirQDateImporter>(MockBehavior.Strict);
        importer.Setup(service => service.StoreNoiseLevelsForDate("2026-07-14"));
        await using var app = await StartAppAsync("monitor-api-key", importer.Object);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/store-noise-levels-for-date")
        {
            Content = new StringContent("{\"date\":\"2026-07-14\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", "monitor-api-key");

        using var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        importer.Verify(service => service.StoreNoiseLevelsForDate("2026-07-14"), Times.Once);
    }

    [TestMethod]
    public async Task Liveness_ReturnsOkWithoutAnApiKey()
    {
        await using var app = await StartAppAsync("monitor-api-key", new Mock<IAirQDateImporter>(MockBehavior.Strict).Object);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/liveness");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private static WebApplication CreateApp(string? apiKey, string configurationKey, IAirQDateImporter importer)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [configurationKey] = apiKey
        });
        builder.Services.AddSingleton(importer);
        return builder.Build();
    }

    private static async Task<WebApplication> StartAppAsync(string apiKey, IAirQDateImporter importer)
    {
        var app = CreateApp(apiKey, "RVT:MONITOR_API_KEY", importer);
        app.MapAirQMonitorApi();
        await app.StartAsync();
        return app;
    }

    private static RouteEndpoint GetRoute(WebApplication app, string path) =>
        ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == path);
}
