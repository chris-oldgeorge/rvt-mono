using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Diagnostics;

namespace OmnidotsAdapterTests.UseCases;

[TestClass]
public sealed class ConfigureMeasuringPointHandlerTests
{
    private const string ConfigSecret = "cccccccccccccccccccccccccccccccc";
    private const string WebhookSecret = "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww";
    private const string WebhookUrl = "https://alerts.example.test/webhook";
    private const string SerialId = "23423";

    public ConfigureMeasuringPointHandlerTests()
    {
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
        RvtLogger.CreateLogger(loggerFactory, nameof(ConfigureMeasuringPointHandlerTests));
    }

    [TestMethod]
    public async Task RunAsync_InvalidSecurityOptions_PrecedeJsonParsing()
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(
            out Mock<IHttpClient>? httpClient,
            out Mock<IOmnidotsMonitorQueries>? monitorQueries,
            options: new OmnidotsApiSecurityOptions());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            handler.RunAsync(Bytes("{"), CancellationToken.None));

        httpClient.VerifyNoOtherCalls();
        monitorQueries.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunAsync_MalformedJson_ThrowsJsonExceptionBeforeAuthentication()
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out Mock<IHttpClient>? httpClient, out Mock<IOmnidotsMonitorQueries>? monitorQueries);

        await Assert.ThrowsExactlyAsync<JsonException>(() =>
            handler.RunAsync(Bytes("{\"secret\":"), CancellationToken.None));

        httpClient.VerifyNoOtherCalls();
        monitorQueries.VerifyNoOtherCalls();
    }

    [DataRow("{}")]
    [DataRow("{\"secret\":null}")]
    [DataRow("{\"secret\":123}")]
    [TestMethod]
    public async Task RunAsync_MissingOrNonStringSecret_ThrowsAuthenticationException(string json)
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out Mock<IHttpClient>? httpClient, out Mock<IOmnidotsMonitorQueries>? monitorQueries);

        OmnidotsConfigurationAuthenticationException exception = await Assert.ThrowsExactlyAsync<OmnidotsConfigurationAuthenticationException>(() =>
            handler.RunAsync(Bytes(json), CancellationToken.None));

        Assert.AreEqual("Measuring point configuration authentication failed.", exception.Message);
        httpClient.VerifyNoOtherCalls();
        monitorQueries.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunAsync_WrongSecret_PrecedesInvalidBusinessFields()
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out Mock<IHttpClient>? httpClient, out Mock<IOmnidotsMonitorQueries>? monitorQueries);
        const string json = """
            {
              "secret": "wrong-secret",
              "serialid": " ",
              "flat_level": 1.7976931348623157E+308,
              "unsupported": true
            }
            """;

        await Assert.ThrowsExactlyAsync<OmnidotsConfigurationAuthenticationException>(() =>
            handler.RunAsync(Bytes(json), CancellationToken.None));

        httpClient.VerifyNoOtherCalls();
        monitorQueries.VerifyNoOtherCalls();
    }

    [DataRow("unsupported", "true")]
    [DataRow("webhook", "\"https://attacker.example.test/capture\"")]
    [TestMethod]
    public async Task RunAsync_AuthenticatedUnsupportedMember_ThrowsJsonException(
        string propertyName,
        string propertyValue)
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out Mock<IHttpClient>? httpClient, out Mock<IOmnidotsMonitorQueries>? monitorQueries);
        string json = $"{{\"secret\":\"{ConfigSecret}\",\"serialid\":\"{SerialId}\",\"{propertyName}\":{propertyValue}}}";

        await Assert.ThrowsExactlyAsync<JsonException>(() =>
            handler.RunAsync(Bytes(json), CancellationToken.None));

        httpClient.VerifyNoOtherCalls();
        monitorQueries.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunAsync_BlankSerialId_ThrowsJsonException()
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out Mock<IHttpClient>? httpClient, out Mock<IOmnidotsMonitorQueries>? monitorQueries);
        string json = $"{{\"secret\":\"{ConfigSecret}\",\"serialid\":\"   \"}}";

        await Assert.ThrowsExactlyAsync<JsonException>(() =>
            handler.RunAsync(Bytes(json), CancellationToken.None));

        httpClient.VerifyNoOtherCalls();
        monitorQueries.VerifyNoOtherCalls();
    }

    [DataRow("trace_save_level", "\"NaN\"")]
    [DataRow("trace_pre_trigger", "1e400")]
    [DataRow("trace_post_trigger", "-1")]
    [DataRow("flat_level", "1.7976931348623157E+308")]
    [DataRow("level_alert", "-0.01")]
    [DataRow("level_caution", "1000000000")]
    [TestMethod]
    public async Task RunAsync_InvalidTuningValue_ThrowsJsonException(string propertyName, string propertyValue)
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out Mock<IHttpClient>? httpClient, out Mock<IOmnidotsMonitorQueries>? monitorQueries);
        string json = $"{{\"secret\":\"{ConfigSecret}\",\"serialid\":\"{SerialId}\",\"{propertyName}\":{propertyValue}}}";

        await Assert.ThrowsExactlyAsync<JsonException>(() =>
            handler.RunAsync(Bytes(json), CancellationToken.None));

        httpClient.VerifyNoOtherCalls();
        monitorQueries.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void FixedTimeSecretComparer_HashesUtf8SecretsBeforeComparison()
    {
        Assert.IsTrue(OmnidotsFixedTimeSecretComparer.Matches(ConfigSecret, ConfigSecret));
        Assert.IsFalse(OmnidotsFixedTimeSecretComparer.Matches(ConfigSecret, ConfigSecret + "x"));
        Assert.IsFalse(OmnidotsFixedTimeSecretComparer.Matches("é", "e"));
        Assert.IsFalse(OmnidotsFixedTimeSecretComparer.Matches("\ud800", ConfigSecret));
    }

    [TestMethod]
    public async Task RunAsync_UsesOnlyDeployedWebhookAndReturnsExactSafeResult()
    {
        ConfigureMeasuringPointHandler handler = CreateSuccessfulHandler(
            out Mock<IHttpClient>? httpClient,
            out Mock<IOmnidotsMonitorQueries>? monitorQueries,
            out ConfigRequestCapture? capture);
        string json = $"{{\"secret\":\"{ConfigSecret}\",\"serialid\":\"{SerialId}\",\"level_alert\":10,\"level_caution\":7}}";

        ConfigureMeasuringPointResult result = await handler.RunAsync(Bytes(json), CancellationToken.None);

        Assert.IsNotNull(capture.Request);
        Assert.AreEqual(WebhookUrl, capture.Request.WebhookRecipient!.Url);
        Assert.AreEqual(WebhookSecret, capture.Request.WebhookRecipient.Secret);
        string responseJson = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsFalse(responseJson.Contains("serial", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("{\"configured\":true}", responseJson);
        httpClient.VerifyAll();
        monitorQueries.VerifyAll();
    }

    [DataRow(true, true, true, null, null, false)]
    [DataRow(false, true, true, null, null, false)]
    [DataRow(true, false, true, null, null, false)]
    [DataRow(true, true, false, null, null, false)]
    [DataRow(true, true, true, "09:10:11", "17:18:19", true)]
    [TestMethod]
    public async Task RunAsync_MapsMonitorScheduleAndConfiguration(
        bool weekdays,
        bool sundays,
        bool saturdays,
        string? startTime,
        string? endTime,
        bool useTunedTraceValues)
    {
        SiteTimes siteTimes = CreateSiteTimes(weekdays, sundays, saturdays, startTime, endTime);
        ConfigureMeasuringPointHandler handler = CreateSuccessfulHandler(
            out Mock<IHttpClient>? httpClient,
            out Mock<IOmnidotsMonitorQueries>? monitorQueries,
            out ConfigRequestCapture? capture,
            siteTimes);
        Dictionary<string, object?> request = new()
        {
            ["secret"] = ConfigSecret,
            ["serialid"] = SerialId,
            ["level_caution"] = 7,
            ["level_alert"] = 10
        };
        if (useTunedTraceValues)
        {
            request["trace_save_level"] = 12.5;
            request["trace_pre_trigger"] = 4.5;
            request["trace_post_trigger"] = 5.5;
        }

        ConfigureMeasuringPointResult result = await handler.RunAsync(
            Bytes(JsonSerializer.Serialize(request)),
            CancellationToken.None);

        Assert.IsNotNull(capture.Request);
        AssertSchedule(capture.Request, siteTimes);
        Assert.AreEqual("BS7385_250Hz", capture.Request.GuideLine);
        Assert.AreEqual("unspecified", capture.Request.BuildingLevel);
        Assert.AreEqual(useTunedTraceValues ? 12.5 : 10.0, capture.Request.TraceSaveLevel);
        Assert.AreEqual(useTunedTraceValues ? 4.5 : 3.0, capture.Request.TracePreTrigger);
        Assert.AreEqual(useTunedTraceValues ? 5.5 : 3.0, capture.Request.TracePostTrigger);
        Assert.AreEqual(0, capture.Request.AlarmLevel1);
        Assert.AreEqual(70, capture.Request.AlarmLevel2);
        Assert.AreEqual(100, capture.Request.AlarmLevel3);
        Assert.AreEqual(WebhookUrl, capture.Request.WebhookRecipient!.Url);
        Assert.AreEqual(WebhookSecret, capture.Request.WebhookRecipient.Secret);
        Assert.AreEqual("{\"configured\":true}", JsonSerializer.Serialize(
            result,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        httpClient.VerifyAll();
        monitorQueries.VerifyAll();
    }

    [TestMethod]
    public async Task RunAsync_VendorFalseResponse_ThrowsSafeVendorException()
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out Mock<IHttpClient>? httpClient, out Mock<IOmnidotsMonitorQueries>? monitorQueries);
        SetupMonitor(monitorQueries);
        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"ok\":true,\"token\":\"vendor-token\"}");
        httpClient.Setup(client => client.PostAsync(
                "/api/v1/configure_measuring_point?token=vendor-token&measuring_point_id=23423",
                It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"ok\":false,\"message\":\"raw-vendor-body-marker\"}");

        OmnidotsVendorConfigurationException exception = await Assert.ThrowsExactlyAsync<OmnidotsVendorConfigurationException>(() =>
            handler.RunAsync(Bytes(ValidJson()), CancellationToken.None));

        AssertSafeVendorException(exception);
    }

    [TestMethod]
    public async Task RunAsync_VendorNetworkFailure_ThrowsSafeVendorException()
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out Mock<IHttpClient>? httpClient, out Mock<IOmnidotsMonitorQueries>? monitorQueries);
        SetupMonitor(monitorQueries);
        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("raw-vendor-body-marker"));

        OmnidotsVendorConfigurationException exception = await Assert.ThrowsExactlyAsync<OmnidotsVendorConfigurationException>(() =>
            handler.RunAsync(Bytes(ValidJson()), CancellationToken.None));

        AssertSafeVendorException(exception);
    }

    [TestMethod]
    public async Task RunAsync_InvalidVendorResponse_ThrowsSafeVendorException()
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out Mock<IHttpClient>? httpClient, out Mock<IOmnidotsMonitorQueries>? monitorQueries);
        SetupMonitor(monitorQueries);
        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("raw-vendor-body-marker");

        OmnidotsVendorConfigurationException exception = await Assert.ThrowsExactlyAsync<OmnidotsVendorConfigurationException>(() =>
            handler.RunAsync(Bytes(ValidJson()), CancellationToken.None));

        AssertSafeVendorException(exception);
    }

    [TestMethod]
    public async Task RunAsync_SafeConcreteClientFailure_ThrowsSafeVendorException()
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out Mock<IHttpClient>? httpClient, out Mock<IOmnidotsMonitorQueries>? monitorQueries);
        SetupMonitor(monitorQueries);
        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"ok\":true,\"token\":\"vendor-token\"}");
        httpClient.Setup(client => client.PostAsync(
                "/api/v1/configure_measuring_point?token=vendor-token&measuring_point_id=23423",
                It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(AdapterException.Of("Omnidots API request failed."));

        OmnidotsVendorConfigurationException exception = await Assert.ThrowsExactlyAsync<OmnidotsVendorConfigurationException>(() =>
            handler.RunAsync(Bytes(ValidJson()), CancellationToken.None));

        AssertSafeVendorException(exception);
    }

    [TestMethod]
    public async Task RunAsync_CancellationWhileAwaitingVendorCall_IsPreserved()
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out Mock<IHttpClient>? httpClient, out Mock<IOmnidotsMonitorQueries>? monitorQueries);
        SetupMonitor(monitorQueries);
        TaskCompletionSource<string> pendingResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .Returns(pendingResponse.Task);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        // The token now reaches the vendor call itself; an already-cancelled
        // request stops before the call rather than being abandoned around it.
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            handler.RunAsync(Bytes(ValidJson()), cancellation.Token));

        monitorQueries.VerifyAll();
    }

    private static ConfigureMeasuringPointHandler CreateSuccessfulHandler(
        out Mock<IHttpClient> httpClient,
        out Mock<IOmnidotsMonitorQueries> monitorQueries,
        out ConfigRequestCapture capture,
        SiteTimes? siteTimes = null)
    {
        ConfigureMeasuringPointHandler handler = CreateHandler(out httpClient, out monitorQueries);
        SetupMonitor(monitorQueries, siteTimes);
        capture = new ConfigRequestCapture();
        ConfigRequestCapture requestCapture = capture;
        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"ok\":true,\"token\":\"vendor-token\"}");
        httpClient.Setup(client => client.PostAsync(
                "/api/v1/configure_measuring_point?token=vendor-token&measuring_point_id=23423",
                It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .Callback<string, HttpContent, CancellationToken>((_, content, _) =>
                requestCapture.Request = JsonSerializer.Deserialize<ConfigRequest>(
                    content.ReadAsStringAsync().GetAwaiter().GetResult()))
            .ReturnsAsync("{\"ok\":true}");
        return handler;
    }

    private static ConfigureMeasuringPointHandler CreateHandler(
        out Mock<IHttpClient> httpClient,
        out Mock<IOmnidotsMonitorQueries> monitorQueries,
        OmnidotsApiSecurityOptions? options = null)
    {
        httpClient = new Mock<IHttpClient>(MockBehavior.Strict);
        monitorQueries = new Mock<IOmnidotsMonitorQueries>(MockBehavior.Strict);
        OmnidotsHttpGateway gateway = new(httpClient.Object, "vendor-user", "vendor-password");
        return new ConfigureMeasuringPointHandler(gateway, monitorQueries.Object, options ?? ValidOptions());
    }

    private static void SetupMonitor(
        Mock<IOmnidotsMonitorQueries> monitorQueries,
        SiteTimes? siteTimes = null)
    {
        VibrationMonitorDto monitor = OmnidotsFixture.MonitorsList(1, serialIdIn: 23422).Single();
        monitorQueries.Setup(queries => queries.ReadMonitor(SerialId)).Returns(monitor);
        monitorQueries.Setup(queries => queries.ReadSiteTimes(monitor.Id)).Returns(siteTimes ?? new SiteTimes());
    }

    private static SiteTimes CreateSiteTimes(
        bool weekdays,
        bool sundays,
        bool saturdays,
        string? startTime,
        string? endTime)
    {
        SiteTimes siteTimes = new();
        if (startTime is null || endTime is null)
        {
            return siteTimes;
        }

        TimeSpan start = TimeSpan.Parse(startTime);
        TimeSpan end = TimeSpan.Parse(endTime);
        if (weekdays)
        {
            siteTimes.WeekdayStart = start;
            siteTimes.WeekdayEnd = end;
        }

        if (sundays)
        {
            siteTimes.SundayStart = start;
            siteTimes.SundayEnd = end;
        }

        if (saturdays)
        {
            siteTimes.SaturdayStart = start;
            siteTimes.SaturdayEnd = end;
        }

        return siteTimes;
    }

    private static void AssertSchedule(ConfigRequest request, SiteTimes siteTimes)
    {
        Assert.AreEqual(siteTimes.GetSundayStart(), request.EnableTime0);
        Assert.AreEqual(siteTimes.GetSundayEnd(), request.DisableTime0);
        Assert.AreEqual(siteTimes.GetWeekdayStart(), request.EnableTime1);
        Assert.AreEqual(siteTimes.GetWeekdayEnd(), request.DisableTime1);
        Assert.AreEqual(siteTimes.GetWeekdayStart(), request.EnableTime2);
        Assert.AreEqual(siteTimes.GetWeekdayEnd(), request.DisableTime2);
        Assert.AreEqual(siteTimes.GetWeekdayStart(), request.EnableTime3);
        Assert.AreEqual(siteTimes.GetWeekdayEnd(), request.DisableTime3);
        Assert.AreEqual(siteTimes.GetWeekdayStart(), request.EnableTime4);
        Assert.AreEqual(siteTimes.GetWeekdayEnd(), request.DisableTime4);
        Assert.AreEqual(siteTimes.GetWeekdayStart(), request.EnableTime5);
        Assert.AreEqual(siteTimes.GetWeekdayEnd(), request.DisableTime5);
        Assert.AreEqual(siteTimes.GetSaturdayStart(), request.EnableTime6);
        Assert.AreEqual(siteTimes.GetSaturdayEnd(), request.DisableTime6);
    }

    private static OmnidotsApiSecurityOptions ValidOptions() => new()
    {
        WebhookUrl = WebhookUrl,
        WebhookSecret = WebhookSecret,
        ConfigSecret = ConfigSecret,
        NotificationDelayMinutes = 5,
        WebhookConcurrencyLimit = 8,
        ConfigureConcurrencyLimit = 2
    };

    private static string ValidJson() =>
        $"{{\"secret\":\"{ConfigSecret}\",\"serialid\":\"{SerialId}\"}}";

    private static ReadOnlyMemory<byte> Bytes(string value) => Encoding.UTF8.GetBytes(value);

    private static void AssertSafeVendorException(OmnidotsVendorConfigurationException exception)
    {
        Assert.AreEqual("Measuring point configuration failed.", exception.Message);
        Assert.IsNull(exception.InnerException);
        Assert.IsFalse(exception.ToString().Contains("raw-vendor-body-marker", StringComparison.Ordinal));
    }

    private sealed class ConfigRequestCapture
    {
        public ConfigRequest? Request { get; set; }
    }
}
