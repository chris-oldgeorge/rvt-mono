using Microsoft.Extensions.Logging;
using Moq;
using Omnidots.Api;
using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using OmnidotsAdapterTests;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Utilities;

namespace OmnidotsMonitorTests.UseCases;

/// <summary>
/// The Omnidots HTTP client caps a response at 4 MB and times out after 30
/// seconds, and the import handlers asked for everything from the cursor to
/// now in a single request. A months-old monitor therefore exceeded one of
/// those bounds, the cursor never advanced, and every later run repeated the
/// same oversized request - a permanent, silent stall. Requests are now split
/// into windows of at most <c>MaximumRequestWindow</c>, as Svantek does.
/// </summary>
[TestClass]
public sealed class OmnidotsImportChunkingTests
{
    private const string _peakRecordsPath = "/api/v1/get_peak_records";

    public OmnidotsImportChunkingTests() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(OmnidotsImportChunkingTests));

    [TestMethod]
    public async Task StorePeakRecords_MonthOldCursor_IsSplitIntoContiguousBoundedWindows()
    {
        DateTime cursor = TruncateToSecond(DateTime.UtcNow.AddDays(-30));
        PeakImportRun run = await RunPeakImportAsync(cursor);
        List<(DateTime Start, DateTime End)> windows = run.Windows;

        // 30 days at a 12-hour window; the single unbounded request is gone.
        Assert.HasCount(61, windows);
        Assert.IsTrue(windows.All(window => window.End - window.Start <= TimeSpan.FromHours(12)));
        // The union is exactly the original range: nothing is skipped.
        Assert.AreEqual(cursor.AddMinutes(-5), windows[0].Start);
        for (int index = 1; index < windows.Count; index++)
        {
            Assert.AreEqual(windows[index - 1].End, windows[index].Start);
        }

        // The last window still ends at now, so a run always reaches the most
        // recent samples even when the earlier windows are empty.
        Assert.IsTrue(windows[^1].End >= run.Before.AddSeconds(-1) && windows[^1].End <= run.After);
    }

    [TestMethod]
    public async Task StorePeakRecords_RecentCursor_StillIssuesASingleRequest()
    {
        DateTime cursor = TruncateToSecond(DateTime.UtcNow.AddMinutes(-30));
        List<(DateTime Start, DateTime End)> windows = (await RunPeakImportAsync(cursor)).Windows;

        Assert.HasCount(1, windows);
        Assert.AreEqual(cursor.AddMinutes(-5), windows[0].Start);
    }

    [TestMethod]
    public async Task StorePeakRecords_NeverImportedMonitorWithAnAncientDeployment_IsCappedAtTheInitialBackfill()
    {
        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        Mock<IOmnidotsImportCursorQueries> cursorQueries = new();
        List<string> requestedUrls = [];
        VibrationMonitorDto monitor = OmnidotsFixture.MonitorsList(1).Single();
        monitor.LastDataTime = null;
        monitor.DeployDate = DateTime.UtcNow.AddYears(-3);

        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .Returns(OmnidotsFixture.AuthenticateTask("peak-token"));
        httpClient.Setup(client => client.GetAsync(
                It.Is<string>(url => url.StartsWith(_peakRecordsPath, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((url, _) => requestedUrls.Add(url))
            .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"samples\":[]}"));
        dbClient.Setup(client => client.ReadMonitorList()).Returns([monitor]);

        OmnidotsApi api = new(
            httpClient.Object,
            dbClient.Object,
            cursorQueries.Object,
            Mock.Of<IOmnidotsMeasurementImportCommands>(),
            Mock.Of<IOmnidotsTraceQueries>(),
            Mock.Of<IMqttClient>(),
            Mock.Of<IAlertIngressPort>(),
            testLocal: false,
            new OmnidotsMonitoringOptions(),
            Mock.Of<Omnidots.Api.UseCases.IOmnidotsMonitoringNotifier>(),
            new OmnidotsTraceCollectionOptions(),
            TimeProvider.System,
            new OmnidotsImportOptions());

        await api.StorePeakRecordsLastDataTimeAsync(TestContext.CancellationToken);

        // Three years of 12-hour windows would be ~2,190 requests in one run.
        // Only the bootstrap path is capped, at seven days.
        Assert.HasCount(14, requestedUrls);
    }

    private sealed record PeakImportRun(
        List<(DateTime Start, DateTime End)> Windows,
        DateTime Before,
        DateTime After);

    private async Task<PeakImportRun> RunPeakImportAsync(DateTime cursor)
    {
        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        Mock<IOmnidotsImportCursorQueries> cursorQueries = new();
        List<string> requestedUrls = [];

        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
            .Returns(OmnidotsFixture.AuthenticateTask("peak-token"));
        httpClient.Setup(client => client.GetAsync(
                It.Is<string>(url => url.StartsWith(_peakRecordsPath, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((url, _) => requestedUrls.Add(url))
            .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"samples\":[]}"));
        dbClient.Setup(client => client.ReadMonitorList()).Returns(OmnidotsFixture.MonitorsList(1));
        cursorQueries.Setup(query => query.ReadImportCursor("1", OmnidotsMeasurementSeries.Peak)).Returns(cursor);

        OmnidotsApi api = new(
            httpClient.Object,
            dbClient.Object,
            cursorQueries.Object,
            Mock.Of<IOmnidotsMeasurementImportCommands>(),
            Mock.Of<IOmnidotsTraceQueries>(),
            Mock.Of<IMqttClient>(),
            Mock.Of<IAlertIngressPort>(),
            testLocal: false,
            new OmnidotsMonitoringOptions(),
            Mock.Of<Omnidots.Api.UseCases.IOmnidotsMonitoringNotifier>(),
            new OmnidotsTraceCollectionOptions(),
            TimeProvider.System,
            new OmnidotsImportOptions());

        DateTime before = DateTime.UtcNow;
        await api.StorePeakRecordsLastDataTimeAsync(TestContext.CancellationToken);
        DateTime after = DateTime.UtcNow;

        return new PeakImportRun([.. requestedUrls.Select(ParseWindow)], before, after);
    }

    private static (DateTime Start, DateTime End) ParseWindow(string url)
    {
        Dictionary<string, string> query = url[(url.IndexOf('?', StringComparison.Ordinal) + 1)..]
            .Split('&')
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        return (
            DateTimeUtil.JAN1_1970.AddMilliseconds(long.Parse(query["start_time"], System.Globalization.CultureInfo.InvariantCulture)),
            DateTimeUtil.JAN1_1970.AddMilliseconds(long.Parse(query["end_time"], System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static DateTime TruncateToSecond(DateTime value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

    public TestContext TestContext { get; set; } = null!;
}
