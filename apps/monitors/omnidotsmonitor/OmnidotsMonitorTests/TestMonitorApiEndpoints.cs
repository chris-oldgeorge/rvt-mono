using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Omnidots.Api;
using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Diagnostics;

namespace OmnidotsAdapterTests;

[TestClass]
public sealed class TestMonitorApiEndpoints
{
    private const string WebhookSecret = "WEBHOOK_SECRET_SENTINEL_123456789";
    private const string ConfigSecret = "CONFIG_SECRET_SENTINEL_1234567890";
    private const string Destination = "https://destination-sentinel.example.test/webhook";
    private const string RawException = "RAW_EXCEPTION_SENTINEL";
    private const string VendorResponse = "VENDOR_RESPONSE_SENTINEL";
    private const string BodySentinel = "BODY_SENTINEL";

    [TestMethod]
    public void MapOmnidotsMonitorApi_RegistersExpectedRoutes()
    {
        var builder = WebApplication.CreateBuilder(["--hostBuilder:reloadConfigOnChange=false"]);
        builder.Services.AddRateLimiter();
        var app = builder.Build();

        app.MapOmnidotsMonitorApi();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        CollectionAssert.AreEquivalent(new[]
        {
            "/liveness",
            "/configure-measuring-point",
            "/webhook"
        }, routes);
    }

    [DataRow("missing")]
    [DataRow("blank")]
    [DataRow("malformed")]
    [DataRow("mismatch")]
    [DataTestMethod]
    public async Task Webhook_MissingBlankMalformedOrMismatchedSignature_Returns401(string kind)
    {
        await using var app = await EndpointApp.StartAsync();
        var body = ValidWebhookBody();
        var signature = kind switch
        {
            "missing" => null,
            "blank" => string.Empty,
            "malformed" => "SHA256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            _ => "sha256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        };
        using var request = CreateWebhookRequest(body, signature);

        using var response = await app.Client.SendAsync(request);

        var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Unauthorized webhook request.");
        AssertNoLeakage(problem, app.Logs, BodySentinel, signature, WebhookSecret);
        Assert.AreEqual(0, app.Ingress.AcceptCount);
    }

    [TestMethod]
    public async Task Webhook_MultipleSignatureValues_Returns401()
    {
        await using var app = await EndpointApp.StartAsync();
        var body = ValidWebhookBody();
        using var request = CreateWebhookRequest(body, signature: null);
        request.Headers.TryAddWithoutValidation(
            OmnidotsProtocol.SIGNATURE_HEADER,
            [Signature(body), "sha256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"]);

        using var response = await app.Client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Unauthorized webhook request.");
        Assert.AreEqual(0, app.Ingress.AcceptCount);
    }

    [TestMethod]
    public async Task Webhook_CommaCombinedSignatureValue_Returns401()
    {
        await using var app = await EndpointApp.StartAsync();
        var body = ValidWebhookBody();
        var signature = $"{Signature(body)},sha256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        using var request = CreateWebhookRequest(body, signature);

        using var response = await app.Client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Unauthorized webhook request.");
        Assert.AreEqual(0, app.Ingress.AcceptCount);
    }

    [TestMethod]
    public async Task Webhook_AuthenticatedMalformedUtf8_Returns400()
    {
        await using var app = await EndpointApp.StartAsync();
        byte[] body = [0x7b, 0x22, 0x78, 0x22, 0x3a, 0xff, 0x7d];
        using var request = CreateWebhookRequest(body, Signature(body));

        using var response = await app.Client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Invalid webhook payload.");
        Assert.AreEqual(0, app.Ingress.AcceptCount);
    }

    [DataRow("{\"created_at\":")]
    [DataRow("{\"bodySentinel\":\"BODY_SENTINEL\"}")]
    [DataTestMethod]
    public async Task Webhook_AuthenticatedMalformedJsonOrSchema_Returns400WithoutLeakage(string json)
    {
        await using var app = await EndpointApp.StartAsync();
        var body = Encoding.UTF8.GetBytes(json);
        var signature = Signature(body);
        using var request = CreateWebhookRequest(body, signature);

        using var response = await app.Client.SendAsync(request);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Invalid webhook payload.");
        AssertNoLeakage(problem, app.Logs, BodySentinel, signature, WebhookSecret);
        Assert.AreEqual(0, app.Ingress.AcceptCount);
    }

    [DataRow(false)]
    [DataRow(true)]
    [DataTestMethod]
    public async Task Webhook_FreshOrDurableDuplicate_ReturnsExact200Body(bool duplicate)
    {
        var result = IngressResult(duplicate);
        var ingress = new CapturingIngress((_, _) => Task.FromResult(result));
        await using var app = await EndpointApp.StartAsync(ingress: ingress);
        var body = ValidWebhookBody();
        using var request = CreateWebhookRequest(body, Signature(body));

        using var response = await app.Client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual("{\"processed\":true}", await response.Content.ReadAsStringAsync());
        Assert.AreEqual(1, ingress.AcceptCount);
    }

    [TestMethod]
    public async Task Webhook_TransientPersistenceFailure_Returns503WithoutLeakage()
    {
        var ingress = new CapturingIngress((_, _) => throw new AlertTransientPersistenceException(
            RawException,
            new InvalidOperationException($"{RawException}:{Destination}")));
        await using var app = await EndpointApp.StartAsync(ingress: ingress);
        var body = ValidWebhookBody();
        var signature = Signature(body);
        using var request = CreateWebhookRequest(body, signature);

        using var response = await app.Client.SendAsync(request);

        var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.ServiceUnavailable,
            "Webhook temporarily unavailable.");
        AssertNoLeakage(problem, app.Logs, RawException, Destination, signature, WebhookSecret);
    }

    [TestMethod]
    public async Task Webhook_UnexpectedPermanentFailure_Returns500WithoutLeakage()
    {
        var ingress = new CapturingIngress((_, _) => throw new InvalidOperationException(
            $"{RawException}:{Destination}"));
        await using var app = await EndpointApp.StartAsync(ingress: ingress);
        var body = ValidWebhookBody();
        var signature = Signature(body);
        using var request = CreateWebhookRequest(body, signature);

        using var response = await app.Client.SendAsync(request);

        var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.InternalServerError,
            "Webhook processing failed.");
        AssertNoLeakage(problem, app.Logs, RawException, Destination, signature, WebhookSecret);
    }

    [TestMethod]
    public async Task Webhook_OversizedBody_Returns413()
    {
        await using var app = await EndpointApp.StartAsync();
        var body = new byte[BoundedJsonRequestReader.MaxBodyBytes + 1];
        using var request = CreateWebhookRequest(body, Signature(body));

        using var response = await app.Client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "Request body too large.");
        Assert.AreEqual(0, app.Ingress.AcceptCount);
    }

    [TestMethod]
    public async Task Webhook_UnsupportedMediaType_Returns415()
    {
        await using var app = await EndpointApp.StartAsync();
        var body = ValidWebhookBody();
        using var request = CreateWebhookRequest(body, Signature(body), "text/plain");

        using var response = await app.Client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "Unsupported media type.");
        Assert.AreEqual(0, app.Ingress.AcceptCount);
    }

    [TestMethod]
    public async Task Webhook_ConcurrencyLimitHasNoQueueAndReturns429ProblemDetails()
    {
        var ingress = new BlockingIngress();
        var options = ValidOptions();
        options.WebhookConcurrencyLimit = 1;
        await using var app = await EndpointApp.StartAsync(ingress: ingress, options: options);
        var body = ValidWebhookBody();
        using var firstRequest = CreateWebhookRequest(body, Signature(body));
        var firstResponseTask = app.Client.SendAsync(firstRequest);
        await ingress.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var secondRequest = CreateWebhookRequest(body, Signature(body));

        using var secondResponse = await app.Client.SendAsync(secondRequest);

        await AssertProblemAsync(secondResponse, HttpStatusCode.TooManyRequests, "Too many requests.");
        Assert.AreEqual(1, ingress.AcceptCount);
        ingress.Release.TrySetResult();
        using var firstResponse = await firstResponseTask;
        Assert.AreEqual(HttpStatusCode.OK, firstResponse.StatusCode);
    }

    [DataRow("{}")]
    [DataRow("{\"secret\":\"wrong\",\"serialid\":\"23423\"}")]
    [DataTestMethod]
    public async Task ConfigureMeasuringPoint_MissingOrWrongSecret_Returns401(string json)
    {
        await using var app = await EndpointApp.StartAsync();

        using var response = await app.Client.PostAsync(
            "/configure-measuring-point",
            JsonContent(json));

        var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Unauthorized measuring point configuration request.");
        AssertNoLeakage(problem, app.Logs, json, ConfigSecret, WebhookSecret, Destination);
    }

    [DataRow("{\"secret\":")]
    [DataRow("{\"secret\":\"CONFIG_SECRET_SENTINEL_1234567890\",\"serialid\":\"23423\",\"webhook\":\"https://attacker.example.test\"}")]
    [DataTestMethod]
    public async Task ConfigureMeasuringPoint_MalformedOrUnsupportedRequest_Returns400(string json)
    {
        await using var app = await EndpointApp.StartAsync();

        using var response = await app.Client.PostAsync(
            "/configure-measuring-point",
            JsonContent(json));

        var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "Invalid measuring point configuration request.");
        AssertNoLeakage(problem, app.Logs, json, ConfigSecret, WebhookSecret, Destination);
    }

    [TestMethod]
    public async Task ConfigureMeasuringPoint_Success_ReturnsExactSafe200Body()
    {
        await using var app = await EndpointApp.StartAsync();

        using var response = await app.Client.PostAsync(
            "/configure-measuring-point",
            JsonContent(ValidConfigurationJson()));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("{\"configured\":true}", body);
        AssertNoLeakage(body, app.Logs, ConfigSecret, WebhookSecret, Destination, VendorResponse);
    }

    [TestMethod]
    public async Task ConfigureMeasuringPoint_OversizedBody_Returns413()
    {
        await using var app = await EndpointApp.StartAsync();
        using var content = new ByteArrayContent(new byte[BoundedJsonRequestReader.MaxBodyBytes + 1]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await app.Client.PostAsync("/configure-measuring-point", content);

        await AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "Request body too large.");
    }

    [TestMethod]
    public async Task ConfigureMeasuringPoint_UnsupportedMediaType_Returns415()
    {
        await using var app = await EndpointApp.StartAsync();
        using var content = new StringContent(ValidConfigurationJson(), Encoding.UTF8, "text/plain");

        using var response = await app.Client.PostAsync("/configure-measuring-point", content);

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "Unsupported media type.");
    }

    [TestMethod]
    public async Task ConfigureMeasuringPoint_VendorFailure_ReturnsSanitized502Boundary()
    {
        var vendorClient = DefaultVendorClient();
        vendorClient.Setup(client => client.PostAsync(
                "/api/v1/user/authenticate",
                It.IsAny<HttpContent>()))
            .ReturnsAsync($"{{\"ok\":false,\"message\":\"{VendorResponse}\"}}");
        await using var app = await EndpointApp.StartAsync(vendorClient: vendorClient);

        using var response = await app.Client.PostAsync(
            "/configure-measuring-point",
            JsonContent(ValidConfigurationJson()));

        var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.BadGateway,
            "Measuring point vendor request failed.");
        AssertNoLeakage(
            problem,
            app.Logs,
            VendorResponse,
            ConfigSecret,
            WebhookSecret,
            Destination);
    }

    [TestMethod]
    public async Task ConfigureMeasuringPoint_UnexpectedFailure_Returns500WithoutLeakage()
    {
        var queries = DefaultMonitorQueries();
        queries.Setup(query => query.ReadMonitor("23423"))
            .Throws(new InvalidOperationException($"{RawException}:{Destination}"));
        await using var app = await EndpointApp.StartAsync(monitorQueries: queries);

        using var response = await app.Client.PostAsync(
            "/configure-measuring-point",
            JsonContent(ValidConfigurationJson()));

        var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.InternalServerError,
            "Measuring point configuration failed.");
        AssertNoLeakage(problem, app.Logs, RawException, ConfigSecret, WebhookSecret, Destination);
    }

    [TestMethod]
    public async Task ConfigureMeasuringPoint_ConcurrencyLimitHasNoQueueAndReturns429ProblemDetails()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vendorClient = DefaultVendorClient();
        vendorClient.Setup(client => client.PostAsync(
                "/api/v1/user/authenticate",
                It.IsAny<HttpContent>()))
            .Callback(() => entered.TrySetResult())
            .Returns(release.Task);
        var options = ValidOptions();
        options.ConfigureConcurrencyLimit = 1;
        await using var app = await EndpointApp.StartAsync(
            options: options,
            vendorClient: vendorClient);
        var firstResponseTask = app.Client.PostAsync(
            "/configure-measuring-point",
            JsonContent(ValidConfigurationJson()));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        using var secondResponse = await app.Client.PostAsync(
            "/configure-measuring-point",
            JsonContent(ValidConfigurationJson()));

        await AssertProblemAsync(secondResponse, HttpStatusCode.TooManyRequests, "Too many requests.");
        release.TrySetResult("{\"ok\":true,\"token\":\"test-token\"}");
        using var firstResponse = await firstResponseTask;
        Assert.AreEqual(HttpStatusCode.OK, firstResponse.StatusCode);
    }

    private static async Task<string> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode statusCode,
        string title)
    {
        Assert.AreEqual(statusCode, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.AreEqual((int)statusCode, document.RootElement.GetProperty("status").GetInt32());
        Assert.AreEqual(title, document.RootElement.GetProperty("title").GetString());
        return body;
    }

    private static void AssertNoLeakage(
        string responseBody,
        CapturingLoggerProvider logs,
        params string?[] sentinels)
    {
        var capturedLogs = string.Join(Environment.NewLine, logs.Messages);
        foreach (var sentinel in sentinels.Where(value => !string.IsNullOrEmpty(value)))
        {
            Assert.IsFalse(
                responseBody.Contains(sentinel!, StringComparison.Ordinal),
                $"Response leaked sentinel '{sentinel}'.");
            Assert.IsFalse(
                capturedLogs.Contains(sentinel!, StringComparison.Ordinal),
                $"Logs leaked sentinel '{sentinel}'.");
        }
    }

    private static HttpRequestMessage CreateWebhookRequest(
        byte[] body,
        string? signature,
        string mediaType = "application/json")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhook")
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        if (signature is not null)
        {
            request.Headers.TryAddWithoutValidation(OmnidotsProtocol.SIGNATURE_HEADER, signature);
        }

        return request;
    }

    private static ByteArrayContent JsonContent(string json)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8"
        };
        return content;
    }

    private static string ValidConfigurationJson() =>
        $"{{\"secret\":\"{ConfigSecret}\",\"serialid\":\"23423\",\"level_caution\":7,\"level_alert\":10}}";

    private static byte[] ValidWebhookBody() => Encoding.UTF8.GetBytes("""
        {"created_at":1721037600,"data":{"alarms":{"alarm_level_1":30,"alarm_level_2":70,"alarm_level_3":100},"axes":{"x":{"vtop":{"value":12}},"y":{"vtop":{"value":8}},"z":{"vtop":{"value":4}}}},"measuring_point_id":23423}
        """);

    private static string Signature(ReadOnlySpan<byte> body)
    {
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), body);
        return $"sha256={Convert.ToHexStringLower(digest)}";
    }

    private static AlertIngressResult IngressResult(bool duplicate) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        AlertOccurrenceOutcome.Accepted,
        duplicate);

    private static OmnidotsApiSecurityOptions ValidOptions() => new()
    {
        WebhookUrl = Destination,
        WebhookSecret = WebhookSecret,
        ConfigSecret = ConfigSecret,
        NotificationDelayMinutes = 5,
        WebhookConcurrencyLimit = 8,
        ConfigureConcurrencyLimit = 2
    };

    private static Mock<IHttpClient> DefaultVendorClient()
    {
        var client = new Mock<IHttpClient>();
        client.Setup(value => value.PostAsync(
                "/api/v1/user/authenticate",
                It.IsAny<HttpContent>()))
            .ReturnsAsync("{\"ok\":true,\"token\":\"test-token\"}");
        client.Setup(value => value.PostAsync(
                "/api/v1/configure_measuring_point?token=test-token&measuring_point_id=23423",
                It.IsAny<HttpContent>()))
            .ReturnsAsync("{\"ok\":true}");
        return client;
    }

    private static Mock<IOmnidotsMonitorQueries> DefaultMonitorQueries()
    {
        var queries = new Mock<IOmnidotsMonitorQueries>();
        var monitor = OmnidotsFixture.MonitorsList(1, alwaysMakeSensor: true, serialIdIn: 23422)[0];
        queries.Setup(query => query.ReadMonitor("23423")).Returns(monitor);
        queries.Setup(query => query.ReadSiteTimes(monitor.Id)).Returns(OmnidotsFixture.AlwaysOpenSiteTimes());
        return queries;
    }

    private sealed class EndpointApp : IAsyncDisposable
    {
        private EndpointApp(
            WebApplication application,
            HttpClient client,
            CapturingLoggerProvider logs,
            CapturingIngress ingress)
        {
            Application = application;
            Client = client;
            Logs = logs;
            Ingress = ingress;
        }

        public WebApplication Application { get; }

        public HttpClient Client { get; }

        public CapturingLoggerProvider Logs { get; }

        public CapturingIngress Ingress { get; }

        public static async Task<EndpointApp> StartAsync(
            CapturingIngress? ingress = null,
            OmnidotsApiSecurityOptions? options = null,
            Mock<IHttpClient>? vendorClient = null,
            Mock<IOmnidotsMonitorQueries>? monitorQueries = null)
        {
            ingress ??= new CapturingIngress();
            options ??= ValidOptions();
            vendorClient ??= DefaultVendorClient();
            monitorQueries ??= DefaultMonitorQueries();
            var logs = new CapturingLoggerProvider();
            var webhookHandler = new ProcessWebhookHandler(
                ingress,
                new OmnidotsAlarmTranslator(),
                options,
                new OmnidotsWebhookSignatureValidator());
            var configurationHandler = new ConfigureMeasuringPointHandler(
                new OmnidotsHttpGateway(vendorClient.Object, "vendor-user", "vendor-auth"),
                monitorQueries.Object,
                options);
            var builder = WebApplication.CreateBuilder(["--hostBuilder:reloadConfigOnChange=false"]);
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(logs);
            builder.Services.AddSingleton(webhookHandler);
            builder.Services.AddSingleton(configurationHandler);
            builder.Services.AddRateLimiter();
            builder.Services.AddSingleton<IConfigureOptions<RateLimiterOptions>>(
                new OmnidotsRateLimiterOptionsSetup(Options.Create(options)));
            var app = builder.Build();
            RvtLogger.CreateLogger(app.Services.GetRequiredService<ILoggerFactory>(), "OmnidotsEndpointTests");
            app.MapOmnidotsMonitorApi();
            await app.StartAsync();
            return new EndpointApp(app, app.GetTestClient(), logs, ingress);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Application.DisposeAsync();
        }
    }

    private class CapturingIngress : IAlertIngressPort
    {
        private readonly Func<AlertSignal, CancellationToken, Task<AlertIngressResult>> accept;

        public CapturingIngress()
            : this((_, _) => Task.FromResult(IngressResult(duplicate: false)))
        {
        }

        public CapturingIngress(Func<AlertSignal, CancellationToken, Task<AlertIngressResult>> accept)
        {
            this.accept = accept;
        }

        public int AcceptCount { get; protected set; }

        public virtual Task<AlertIngressResult> AcceptAsync(
            AlertSignal signal,
            CancellationToken cancellationToken = default)
        {
            AcceptCount++;
            return accept(signal, cancellationToken);
        }
    }

    private sealed class BlockingIngress : CapturingIngress
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<AlertIngressResult> AcceptAsync(
            AlertSignal signal,
            CancellationToken cancellationToken = default)
        {
            AcceptCount++;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return IngressResult(duplicate: false);
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
                if (exception is not null)
                {
                    messages.Enqueue(exception.ToString());
                }
            }
        }
    }
}
