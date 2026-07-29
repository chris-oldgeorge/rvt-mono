using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Api.UseCases;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Rvt.Monitor.Common.Diagnostics;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class StoreAccessoryInfoHandlerTests
{
    [TestInitialize]
    public void InitializeLogger()
    {
        RvtLogger.CreateLogger(LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug)), nameof(StoreAccessoryInfoHandlerTests));
    }

    [TestMethod]
    public async Task RunAsync_UsesTheSuccessfullyCommittedPageCursorForTheNextDirectKeysetRequest()
    {
        Mock<IHttpClient> http = new(MockBehavior.Strict);
        Mock<IDBClient> database = new(MockBehavior.Strict);
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(lastDataTime: null, singleItem: true).Single();
        DateTime firstTimestamp = new(2026, 7, 14, 9, 0, 0, DateTimeKind.Utc);
        DateTime secondTimestamp = firstTimestamp.AddMinutes(1);
        int calls = 0;
        List<string> requestPaths = [];
        http.Setup(client => client.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string path, CancellationToken _) =>
            {
                requestPaths.Add(path);
                calls++;
                return Task.FromResult(AccessoryPageJson(calls == 1 ? firstTimestamp : secondTimestamp));
            });
        database.Setup(query => query.ReadMonitorList(9, null)).Returns([monitor]);
        database.Setup(query => query.ReadLatestAccessoryTimestamp(monitor.SerialId)).Returns((DateTime?)null);
        database.Setup(command => command.InsertAccessoryPageAsync(
                It.Is<IReadOnlyList<MyAtm.Model.Dto.AccessoryInfoDto>>(page =>
                    page.Count == 1 && page[0].SampleTime == firstTimestamp),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        database.Setup(command => command.InsertAccessoryPageAsync(
                It.Is<IReadOnlyList<MyAtm.Model.Dto.AccessoryInfoDto>>(page =>
                    page.Count == 1 && page[0].SampleTime == secondTimestamp),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        MyAtmMonitorReader reader = new(database.Object, database.Object, testLocal: false);
        MyAtmHttpGateway gateway = new(http.Object, devicePageSize: 10, accessoryPageSize: 1);
        StoreAccessoryInfoHandler handler = new(gateway, reader, database.Object, database.Object, database.Object, maxPagesPerMonitorPerRun: 2);

        await handler.RunAsync(9, TestContext.CancellationToken);

        Assert.AreEqual(2, calls);
        Assert.Contains(
            "timestamp gt " + firstTimestamp.ToString("O", CultureInfo.InvariantCulture),
            requestPaths[1]);
        database.Verify(query => query.ReadMonitorList(9, null), Times.Once);
        database.Verify(query => query.ReadLatestAccessoryTimestamp(monitor.SerialId), Times.Once);
        database.Verify(command => command.InsertAccessoryPageAsync(It.IsAny<IReadOnlyList<MyAtm.Model.Dto.AccessoryInfoDto>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        database.VerifyNoOtherCalls();
        http.VerifyAll();
    }

    [TestMethod]
    public async Task RunAsync_StopsWhenTheDirectGatewayReturnsNoNextCursor()
    {
        Mock<IHttpClient> http = new(MockBehavior.Strict);
        Mock<IDBClient> database = new(MockBehavior.Strict);
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(lastDataTime: null, singleItem: true).Single();
        DateTime cursor = new(2026, 7, 14, 9, 0, 0, DateTimeKind.Utc);
        DateTime nextTimestamp = cursor.AddMinutes(1);
        int calls = 0;
        http.Setup(client => client.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken _) =>
            {
                calls++;
                return Task.FromResult(AccessoryPageJson(nextTimestamp));
            });
        database.Setup(query => query.ReadMonitorList(9, null)).Returns([monitor]);
        database.Setup(query => query.ReadLatestAccessoryTimestamp(monitor.SerialId)).Returns(cursor);
        database.Setup(command => command.InsertAccessoryPageAsync(
                It.Is<IReadOnlyList<MyAtm.Model.Dto.AccessoryInfoDto>>(page => page.Count == 1 && page[0].SampleTime == nextTimestamp),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        StoreAccessoryInfoHandler handler = CreateHandler(http.Object, database.Object, maxPagesPerMonitorPerRun: 10);

        await handler.RunAsync(9, TestContext.CancellationToken);

        Assert.AreEqual(2, calls);
        database.Verify(query => query.ReadMonitorList(9, null), Times.Once);
        database.Verify(query => query.ReadLatestAccessoryTimestamp(monitor.SerialId), Times.Once);
        database.Verify(command => command.InsertAccessoryPageAsync(It.IsAny<IReadOnlyList<MyAtm.Model.Dto.AccessoryInfoDto>>(), It.IsAny<CancellationToken>()), Times.Once);
        database.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunAsync_ContinuesLaterMonitorsAndAggregatesPageCommitFailures()
    {
        Mock<IHttpClient> http = new(MockBehavior.Strict);
        Mock<IDBClient> database = new(MockBehavior.Strict);
        List<DustMonitorDto> monitors = MyAtmFixture.CustomerDeviceDtos(lastDataTime: null);
        DustMonitorDto first = monitors[0];
        DustMonitorDto second = monitors[1];
        DateTime timestamp = new(2026, 7, 14, 9, 0, 0, DateTimeKind.Utc);
        InvalidOperationException failure = new("page commit failed");
        IOException recordingFailure = new("error store unavailable");
        http.Setup(client => client.GetAsync(It.Is<string>(path => path.Contains(first.SerialId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessoryPageJson(timestamp));
        http.Setup(client => client.GetAsync(It.Is<string>(path => path.Contains(second.SerialId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessoryPageJson(timestamp));
        database.Setup(query => query.ReadMonitorList(9, null)).Returns(monitors);
        database.Setup(query => query.ReadLatestAccessoryTimestamp(first.SerialId)).Returns((DateTime?)null);
        database.Setup(query => query.ReadLatestAccessoryTimestamp(second.SerialId)).Returns((DateTime?)null);
        database.Setup(command => command.InsertAccessoryPageAsync(
                It.Is<IReadOnlyList<MyAtm.Model.Dto.AccessoryInfoDto>>(page => page.Single().SerialId == first.SerialId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        database.Setup(command => command.InsertAccessoryPageAsync(
                It.Is<IReadOnlyList<MyAtm.Model.Dto.AccessoryInfoDto>>(page => page.Single().SerialId == second.SerialId),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        database.Setup(command => command.HandleException("StoreAccessoryInfo SerialId=" + first.SerialId, failure))
            .Throws(recordingFailure);

        StoreAccessoryInfoHandler handler = CreateHandler(http.Object, database.Object, maxPagesPerMonitorPerRun: 10);

        MyAtmJobAggregateException aggregate = await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(() => handler.RunAsync(9, TestContext.CancellationToken));

        CollectionAssert.AreEqual(new[] { failure }, aggregate.Failures.Select(item => item.Exception).ToArray());
        Assert.AreSame(recordingFailure, aggregate.Failures.Single().RecordingException);
        database.Verify(query => query.ReadMonitorList(9, null), Times.Once);
        database.Verify(query => query.ReadLatestAccessoryTimestamp(first.SerialId), Times.Once);
        database.Verify(query => query.ReadLatestAccessoryTimestamp(second.SerialId), Times.Once);
        database.Verify(command => command.InsertAccessoryPageAsync(It.IsAny<IReadOnlyList<MyAtm.Model.Dto.AccessoryInfoDto>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        database.Verify(command => command.HandleException("StoreAccessoryInfo SerialId=" + first.SerialId, failure), Times.Once);
        database.VerifyNoOtherCalls();
        http.VerifyAll();
    }

    [TestMethod]
    public async Task RunAsync_RequestedCancellationStopsWithoutRecordingFailure()
    {
        Mock<IHttpClient> http = new(MockBehavior.Strict);
        Mock<IDBClient> database = new(MockBehavior.Strict);
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(lastDataTime: null, singleItem: true).Single();
        database.Setup(query => query.ReadMonitorList(9, null)).Returns([monitor]);
        StoreAccessoryInfoHandler handler = CreateHandler(http.Object, database.Object, maxPagesPerMonitorPerRun: 10);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.RunAsync(9, cancellation.Token));

        database.Verify(query => query.ReadMonitorList(9, null), Times.Once);
        database.VerifyNoOtherCalls();
        http.VerifyNoOtherCalls();
    }

    private static StoreAccessoryInfoHandler CreateHandler(
        IHttpClient http,
        IDBClient database,
        int maxPagesPerMonitorPerRun)
    {
        MyAtmMonitorReader reader = new(database, database, testLocal: false);
        MyAtmHttpGateway gateway = new(http, devicePageSize: 10, accessoryPageSize: 1);
        return new StoreAccessoryInfoHandler(gateway, reader, database, database, database, maxPagesPerMonitorPerRun);
    }

    private static string AccessoryPageJson(DateTime timestamp) =>
        JsonSerializer.Serialize(new[] { new AccessoryInfo { Timestamp = timestamp } });

    public TestContext TestContext { get; set; } = null!;
}
