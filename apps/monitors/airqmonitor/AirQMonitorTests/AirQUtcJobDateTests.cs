using AirQ.Api;
using AirQ.Api.Db;
using AirQ.Api.Ports;
using AirQ.Api.UseCases;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.IntegrationTesting;

namespace AirQMonitorTests;

/// <summary>
/// The nightly AirQ jobs are scheduled at 00:03/00:05 under
/// <c>TimeZoneId = "UTC"</c>, but derived their date from
/// <see cref="DateTime.Today"/> - the host's local date. On a UTC+2 host the
/// 00:03 run asked the vendor for, and averaged, the wrong day.
/// </summary>
[TestClass]
public sealed class AirQUtcJobDateTests
{
    // 00:03 UTC on 1 January; a UTC+2 host reads 2 January locally.
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 0, 3, 0, TimeSpan.Zero);

    public AirQUtcJobDateTests() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(AirQUtcJobDateTests));

    [TestMethod]
    public async Task StoreAllNoiseLevelsForYesterday_UsesTheUtcDateNotTheHostLocalDate()
    {
        Mock<IAirQVendorGateway> gateway = new();
        gateway
            .Setup(port => port.GetSamplesForDateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Mock<IAirQMonitorQueries> monitorQueries = new();
        monitorQueries.Setup(queries => queries.ReadMonitorList(null)).Returns([Monitor()]);
        StoreNoiseLevelsForDateHandler forDate = new(
            gateway.Object,
            new AirQMonitorReader(monitorQueries.Object, AirQTestLocalMonitorFilter.Create(false, null)),
            Mock.Of<IAirQMeasurementCommands>(),
            Mock.Of<IAirQOperationalCommands>());
        StoreAllNoiseLevelsForYesterdayHandler subject = new(forDate, new FixedTimeProvider(_now));

        await subject.RunAsync("user", "auth", TestContext.CancellationToken);

        gateway.Verify(port => port.GetSamplesForDateAsync(
            "user",
            "auth",
            "Device1",
            "2025-12-31",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public void NotifySiteAverages_DerivesTheAveragedDayFromTheInjectedUtcClock()
    {
        string source = File.ReadAllText(RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "airqmonitor",
            "AirQMonitor",
            "api",
            "AirQService.cs"));

        Assert.Contains("_timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(-1)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Today", source, StringComparison.Ordinal);
    }

    private static NoiseMonitorDto Monitor() =>
        new(
            Guid.NewGuid(),
            _now.UtcDateTime.AddYears(-1),
            lastDataTime: _now.UtcDateTime.AddDays(-1),
            "Device1",
            "Model",
            "Firmware",
            "Turnkey",
            "Fleet-1",
            0,
            0,
            null,
            "UTC",
            null,
            offline: false,
            new NoiseMonitorStatus(
                _now.UtcDateTime,
                NoiseMonitorStatus.ACTIVE,
                0,
                null,
                null,
                null,
                null));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public TestContext TestContext { get; set; } = null!;
}
