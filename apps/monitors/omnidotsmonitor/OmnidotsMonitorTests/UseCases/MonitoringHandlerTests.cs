using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Omnidots.Api;
using Omnidots.Api.Db;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;

namespace OmnidotsAdapterTests.UseCases;

[TestClass]
public sealed class MonitoringHandlerTests
{
    private const string Recipient = "monitoring@example.test";

    [TestMethod]
    public async Task RunAsync_PreviousDateDataWithLaterClockTime_SendsWarning()
    {
        var utcNow = new DateTimeOffset(2026, 7, 14, 9, 30, 0, TimeSpan.Zero);
        var previousDateWithLaterClockTime = new DateTime(2026, 7, 13, 17, 0, 0, DateTimeKind.Utc);
        var (handler, monitorQueries, notifier) = CreateHandler(
            utcNow,
            OmnidotsFixture.MonitorsList(1, previousDateWithLaterClockTime));
        notifier
            .Setup(x => x.SendNoDataWarningAsync(
                Recipient,
                utcNow.UtcDateTime,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await handler.RunAsync();

        monitorQueries.Verify(x => x.ReadMonitorList(null), Times.Once);
        notifier.Verify(x => x.SendNoDataWarningAsync(
            Recipient,
            utcNow.UtcDateTime,
            It.IsAny<CancellationToken>()), Times.Once);
        monitorQueries.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunAsync_FreshDataAcrossUtcMidnight_DoesNotSendWarning()
    {
        var utcNow = new DateTimeOffset(2026, 7, 14, 0, 30, 0, TimeSpan.Zero);
        var freshPreviousDateData = new DateTime(2026, 7, 13, 23, 45, 0, DateTimeKind.Utc);
        var options = ValidOptions(
            windowStart: TimeSpan.Zero,
            windowEnd: TimeSpan.FromHours(3));
        var (handler, monitorQueries, notifier) = CreateHandler(
            utcNow,
            OmnidotsFixture.MonitorsList(1, freshPreviousDateData),
            options);

        await handler.RunAsync();

        monitorQueries.Verify(x => x.ReadMonitorList(null), Times.Once);
        monitorQueries.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunAsync_FreshUnspecifiedDatabaseTimestamp_TreatsValueAsUtc()
    {
        var clockNow = new DateTimeOffset(2026, 7, 14, 12, 30, 0, TimeSpan.FromHours(3));
        var freshDatabaseTimestamp = DateTime.SpecifyKind(
            clockNow.UtcDateTime - TimeSpan.FromMinutes(30),
            DateTimeKind.Unspecified);
        var (handler, monitorQueries, notifier) = CreateHandler(
            clockNow,
            OmnidotsFixture.MonitorsList(1, freshDatabaseTimestamp));

        await handler.RunAsync();

        monitorQueries.Verify(x => x.ReadMonitorList(null), Times.Once);
        monitorQueries.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunAsync_StaleUnspecifiedDatabaseTimestamp_TreatsValueAsUtc()
    {
        var clockNow = new DateTimeOffset(2026, 7, 14, 12, 30, 0, TimeSpan.FromHours(3));
        var staleDatabaseTimestamp = DateTime.SpecifyKind(
            clockNow.UtcDateTime - TimeSpan.FromHours(2),
            DateTimeKind.Unspecified);
        var (handler, monitorQueries, notifier) = CreateHandler(
            clockNow,
            OmnidotsFixture.MonitorsList(1, staleDatabaseTimestamp));
        notifier
            .Setup(x => x.SendNoDataWarningAsync(
                Recipient,
                clockNow.UtcDateTime,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await handler.RunAsync();

        monitorQueries.Verify(x => x.ReadMonitorList(null), Times.Once);
        notifier.Verify(x => x.SendNoDataWarningAsync(
            Recipient,
            clockNow.UtcDateTime,
            It.IsAny<CancellationToken>()), Times.Once);
        monitorQueries.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunAsync_NullNewestTimestamp_SendsWarning()
    {
        var utcNow = new DateTimeOffset(2026, 7, 14, 9, 30, 0, TimeSpan.Zero);
        var (handler, monitorQueries, notifier) = CreateHandler(
            utcNow,
            OmnidotsFixture.MonitorsList(1, lastDataTime: null));
        notifier
            .Setup(x => x.SendNoDataWarningAsync(
                Recipient,
                utcNow.UtcDateTime,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await handler.RunAsync();

        monitorQueries.Verify(x => x.ReadMonitorList(null), Times.Once);
        notifier.Verify(x => x.SendNoDataWarningAsync(
            Recipient,
            utcNow.UtcDateTime,
            It.IsAny<CancellationToken>()), Times.Once);
        monitorQueries.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunAsync_OutsideConfiguredWindow_DoesNotReadFleetOrSendWarning()
    {
        var utcNow = new DateTimeOffset(2026, 7, 14, 7, 0, 0, TimeSpan.Zero); // 08:00 BST
        var monitorQueries = new Mock<IOmnidotsMonitorQueries>(MockBehavior.Strict);
        var notifier = new Mock<IOmnidotsMonitoringNotifier>(MockBehavior.Strict);
        var handler = new MonitoringHandler(
            new OmnidotsMonitorReader(monitorQueries.Object, testLocal: false),
            ValidOptions(),
            notifier.Object,
            new FixedTimeProvider(utcNow));

        await handler.RunAsync();

        monitorQueries.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("2026-01-14T09:30:00+00:00")]
    [DataRow("2026-07-14T08:30:00+00:00")]
    public async Task RunAsync_InsideLondonWindowInGmtAndBst_SendsWarning(string utcNowText)
    {
        var utcNow = DateTimeOffset.Parse(utcNowText);
        var staleData = utcNow.UtcDateTime - TimeSpan.FromHours(2);
        var (handler, monitorQueries, notifier) = CreateHandler(
            utcNow,
            OmnidotsFixture.MonitorsList(1, staleData));
        notifier
            .Setup(x => x.SendNoDataWarningAsync(
                Recipient,
                utcNow.UtcDateTime,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await handler.RunAsync();

        monitorQueries.Verify(x => x.ReadMonitorList(null), Times.Once);
        notifier.Verify(x => x.SendNoDataWarningAsync(
            Recipient,
            utcNow.UtcDateTime,
            It.IsAny<CancellationToken>()), Times.Once);
        monitorQueries.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunAsync_EmptyFleet_DoesNotSendWarning()
    {
        var utcNow = new DateTimeOffset(2026, 7, 14, 9, 30, 0, TimeSpan.Zero);
        var (handler, monitorQueries, notifier) = CreateHandler(utcNow, []);

        await handler.RunAsync();

        monitorQueries.Verify(x => x.ReadMonitorList(null), Times.Once);
        monitorQueries.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunAsync_RequestedCancellationStopsBeforeReadingFleet()
    {
        var monitorQueries = new Mock<IOmnidotsMonitorQueries>(MockBehavior.Strict);
        var notifier = new Mock<IOmnidotsMonitoringNotifier>(MockBehavior.Strict);
        var handler = new MonitoringHandler(
            new OmnidotsMonitorReader(monitorQueries.Object, testLocal: false),
            ValidOptions(),
            notifier.Object,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 14, 9, 30, 0, TimeSpan.Zero)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            handler.RunAsync(cancellation.Token));

        monitorQueries.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void AddOmnidotsMonitor_AlertRecipientEnvironmentSettingOverridesSectionFallback()
    {
        const string overrideRecipient = "override@example.test";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT:OMNIDOTS_MONITORING_ALERT_TO"] = overrideRecipient,
                [$"{OmnidotsMonitoringOptions.SectionName}:Recipient"] = Recipient,
                [$"{OmnidotsMonitoringOptions.SectionName}:TimeZoneId"] = "Europe/London",
                [$"{OmnidotsMonitoringOptions.SectionName}:WindowStart"] = "08:30:00",
                [$"{OmnidotsMonitoringOptions.SectionName}:WindowEnd"] = "18:00:00",
                [$"{OmnidotsMonitoringOptions.SectionName}:StaleAfter"] = "01:00:00"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddOmnidotsMonitor();
        using var provider = services.BuildServiceProvider();

        Assert.AreEqual(
            overrideRecipient,
            provider.GetRequiredService<OmnidotsMonitoringOptions>().Recipient);
    }

    [TestMethod]
    [DataRow("recipient")]
    [DataRow("timezone")]
    [DataRow("window-order")]
    [DataRow("window-range")]
    [DataRow("stale-threshold")]
    public void Validate_InvalidOptions_ThrowsWithoutExposingRecipient(string invalidField)
    {
        var options = InvalidOptions(invalidField);

        var exception = Assert.ThrowsExactly<OptionsValidationException>(options.Validate);

        Assert.AreEqual(OmnidotsMonitoringOptions.SectionName, exception.OptionsName);
        Assert.IsFalse(exception.Message.Contains(Recipient, StringComparison.Ordinal));
    }

    [TestMethod]
    public void AddOmnidotsMonitor_InvalidMonitoringOptions_FailsWhenServicesStart()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OmnidotsMonitoringOptions.SectionName}:Recipient"] = Recipient,
                [$"{OmnidotsMonitoringOptions.SectionName}:TimeZoneId"] = "Not/A-Time-Zone",
                [$"{OmnidotsMonitoringOptions.SectionName}:WindowStart"] = "08:30:00",
                [$"{OmnidotsMonitoringOptions.SectionName}:WindowEnd"] = "18:00:00",
                [$"{OmnidotsMonitoringOptions.SectionName}:StaleAfter"] = "01:00:00"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddOmnidotsMonitor();
        using var provider = services.BuildServiceProvider();

        var exception = Assert.ThrowsExactly<OptionsValidationException>(
            provider.GetRequiredService<OmnidotsMonitoringOptions>);

        Assert.IsFalse(exception.Message.Contains(Recipient, StringComparison.Ordinal));
    }

    [TestMethod]
    public void AddOmnidotsMonitor_RegistersNarrowImportPortsAgainstCompatibilityFacade()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OmnidotsMonitoringOptions.SectionName}:Recipient"] = Recipient,
                [$"{OmnidotsMonitoringOptions.SectionName}:TimeZoneId"] = "Europe/London",
                [$"{OmnidotsMonitoringOptions.SectionName}:WindowStart"] = "08:30:00",
                [$"{OmnidotsMonitoringOptions.SectionName}:WindowEnd"] = "18:00:00",
                [$"{OmnidotsMonitoringOptions.SectionName}:StaleAfter"] = "01:00:00"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddOmnidotsMonitor();
        using var provider = services.BuildServiceProvider();

        var compatibilityFacade = provider.GetRequiredService<IDBClient>();

        Assert.AreSame<object>(
            compatibilityFacade,
            provider.GetRequiredService<IOmnidotsImportCursorQueries>());
        Assert.AreSame<object>(
            compatibilityFacade,
            provider.GetRequiredService<IOmnidotsMeasurementImportCommands>());
        Assert.IsFalse(typeof(IOmnidotsImportCursorQueries).IsAssignableFrom(typeof(IDBClient)));
        Assert.IsFalse(typeof(IOmnidotsMeasurementImportCommands).IsAssignableFrom(typeof(IDBClient)));
    }

    [TestMethod]
    public async Task AddOmnidotsMonitor_InvalidMonitoringOptions_HostStartFailsSafely()
    {
        const string invalidTimeZone = "Not/A-Time-Zone";
        using var host = CreateHost(invalidTimeZone);

        var exception = await Assert.ThrowsExactlyAsync<OptionsValidationException>(
            () => host.StartAsync());

        Assert.IsFalse(exception.Message.Contains(Recipient, StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains(invalidTimeZone, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AddOmnidotsMonitor_ValidMonitoringOptions_HostStarts()
    {
        using var host = CreateHost("Europe/London");

        await host.StartAsync();

        await host.StopAsync();
    }

    private static (MonitoringHandler Handler, Mock<IOmnidotsMonitorQueries> MonitorQueries,
        Mock<IOmnidotsMonitoringNotifier> Notifier) CreateHandler(
            DateTimeOffset utcNow,
            List<VibrationMonitorDto> monitors,
            OmnidotsMonitoringOptions? options = null)
    {
        var monitorQueries = new Mock<IOmnidotsMonitorQueries>(MockBehavior.Strict);
        monitorQueries
            .Setup(x => x.ReadMonitorList(null))
            .Returns(monitors);
        var notifier = new Mock<IOmnidotsMonitoringNotifier>(MockBehavior.Strict);
        var handler = new MonitoringHandler(
            new OmnidotsMonitorReader(monitorQueries.Object, testLocal: false),
            options ?? ValidOptions(),
            notifier.Object,
            new FixedTimeProvider(utcNow));

        return (handler, monitorQueries, notifier);
    }

    private static OmnidotsMonitoringOptions ValidOptions(
        string recipient = Recipient,
        string timeZoneId = "Europe/London",
        TimeSpan? windowStart = null,
        TimeSpan? windowEnd = null,
        TimeSpan? staleAfter = null) => new()
        {
            Recipient = recipient,
            TimeZoneId = timeZoneId,
            WindowStart = windowStart ?? new TimeSpan(8, 30, 0),
            WindowEnd = windowEnd ?? new TimeSpan(18, 0, 0),
            StaleAfter = staleAfter ?? TimeSpan.FromHours(1)
        };

    private static OmnidotsMonitoringOptions InvalidOptions(string invalidField) => invalidField switch
    {
        "recipient" => ValidOptions(recipient: " "),
        "timezone" => ValidOptions(timeZoneId: "Not/A-Time-Zone"),
        "window-order" => ValidOptions(
            windowStart: TimeSpan.FromHours(18),
            windowEnd: TimeSpan.FromHours(8)),
        "window-range" => ValidOptions(windowEnd: TimeSpan.FromHours(25)),
        "stale-threshold" => ValidOptions(staleAfter: TimeSpan.Zero),
        _ => throw new ArgumentOutOfRangeException(nameof(invalidField))
    };

    private static IHost CreateHost(string timeZoneId)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RVT:EMAIL_ENABLED"] = "false",
            ["RVT:SMS_ENABLED"] = "false",
            [$"{OmnidotsMonitoringOptions.SectionName}:Recipient"] = Recipient,
            [$"{OmnidotsMonitoringOptions.SectionName}:TimeZoneId"] = timeZoneId,
            [$"{OmnidotsMonitoringOptions.SectionName}:WindowStart"] = "08:30:00",
            [$"{OmnidotsMonitoringOptions.SectionName}:WindowEnd"] = "18:00:00",
            [$"{OmnidotsMonitoringOptions.SectionName}:StaleAfter"] = "01:00:00"
        });
        builder.Services.AddOmnidotsMonitor();
        TestUtil.UseTestMonitorContextFactory(builder.Services);
        return builder.Build();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
