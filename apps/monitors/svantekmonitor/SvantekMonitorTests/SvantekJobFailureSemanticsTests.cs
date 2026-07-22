using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Diagnostics;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Api.UseCases;
using Svantek.Model.Dto;

namespace SvantekMonitorTests;

[TestClass]
public sealed class SvantekJobFailureSemanticsTests
{
    private const string ProjectsJson = """
        {
          "status": "ok",
          "projects": [
            { "id": "1", "project_name": "first", "stations": [{ "point_id": "11", "serial": "1001", "short_name": "first" }] },
            { "id": "2", "project_name": "second", "stations": [{ "point_id": "22", "serial": "1002", "short_name": "second" }] },
            { "id": "3", "project_name": "third", "stations": [{ "point_id": "33", "serial": "1003", "short_name": "third" }] }
          ]
        }
        """;

    private const string StationsJson = """
        {
          "status": "ok",
          "stations": [
            { "serial": 1001, "type": "SV307" },
            { "serial": 1002, "type": "SV307" },
            { "serial": 1003, "type": "SV307" }
          ]
        }
        """;

    [TestInitialize]
    public void InitializeLogger() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(SvantekJobFailureSemanticsTests));

    [TestMethod]
    public async Task StoreMonitorsAsync_ContinuesIndependentProjects_RecordsIdentifiers_AndThrowsAggregate()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var http = new Mock<IHttpClient>(MockBehavior.Strict);
        http.SetupSequence(client => client.PostAsync(
                It.IsAny<string>(),
                It.IsAny<HttpContent>(),
                token))
            .ReturnsAsync(ProjectsJson)
            .ReturnsAsync(StationsJson);
        var monitorCommands = new Mock<ISvantekMonitorCommands>(MockBehavior.Strict);
        var persistedProjects = new List<int>();
        monitorCommands.Setup(commands => commands.WriteMonitorListAsync(
                It.IsAny<IReadOnlyList<NoiseMonitorDto>>(),
                token))
            .Callback((IReadOnlyList<NoiseMonitorDto> monitors, CancellationToken _) =>
                persistedProjects.Add(monitors.Single().ProjectId))
            .Returns((IReadOnlyList<NoiseMonitorDto> monitors, CancellationToken _) =>
                monitors.Single().ProjectId is 1 or 3
                    ? Task.FromException(new IOException($"project {monitors.Single().ProjectId} failed"))
                    : Task.CompletedTask);
        var operational = new Mock<ISvantekOperationalCommands>(MockBehavior.Strict);
        var recordedIdentifiers = new List<string>();
        operational.Setup(commands => commands.HandleException(It.IsAny<string>(), It.IsAny<Exception>()))
            .Callback((string identifier, Exception _) => recordedIdentifiers.Add(identifier));
        var handler = new StoreMonitorsHandler(
            new SvantekHttpGateway(http.Object, "key"),
            monitorCommands.Object,
            operational.Object,
            testLocal: false);

        Exception? observed = null;
        try
        {
            await handler.RunAsync(token);
        }
        catch (Exception exception)
        {
            observed = exception;
        }

        Assert.IsInstanceOfType<SvantekJobAggregateException>(observed, observed?.ToString());
        var aggregate = (SvantekJobAggregateException)observed;

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, persistedProjects);
        CollectionAssert.AreEqual(
            new[] { "StoreMonitors project 1", "StoreMonitors project 3" },
            recordedIdentifiers);
        Assert.AreEqual("StoreMonitors", aggregate.JobName);
        Assert.HasCount(2, aggregate.Failures);
        Assert.AreEqual("StoreMonitors project 1", aggregate.Failures[0].Message);
        Assert.AreEqual("StoreMonitors project 3", aggregate.Failures[1].Message);
        Assert.IsInstanceOfType<IOException>(aggregate.Failures[0].InnerException);
        http.VerifyAll();
        monitorCommands.VerifyAll();
        operational.VerifyAll();
    }

    [TestMethod]
    public async Task StoreMonitorsAsync_SetupFailureEscapesImmediatelyWithoutOperationalCapture()
    {
        var authenticationFailure = new UnauthorizedAccessException("authentication rejected");
        var http = new Mock<IHttpClient>(MockBehavior.Strict);
        http.Setup(client => client.PostAsync(
                "projects-get-data.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .ThrowsAsync(authenticationFailure);
        var operational = new Mock<ISvantekOperationalCommands>(MockBehavior.Strict);
        var handler = new StoreMonitorsHandler(
            new SvantekHttpGateway(http.Object, "key"),
            Mock.Of<ISvantekMonitorCommands>(),
            operational.Object,
            testLocal: false);

        var exception = await Assert.ThrowsExactlyAsync<AdapterException>(() => handler.RunAsync());

        Assert.AreSame(authenticationFailure, exception.InnerException, exception.ToString());
        operational.Verify(
            commands => commands.HandleException(It.IsAny<string>(), It.IsAny<Exception>()),
            Times.Never);
    }

    [TestMethod]
    public void FailureCollector_SnapshotsFailuresImmutably_AndNeverCapturesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var operational = new Mock<ISvantekOperationalCommands>(MockBehavior.Strict);
        operational.Setup(commands => commands.HandleException("first", It.IsAny<IOException>()));
        operational.Setup(commands => commands.HandleException("second", It.IsAny<TimeoutException>()));
        operational.Setup(commands => commands.HandleException("later", It.IsAny<InvalidOperationException>()));
        var collector = new SvantekFailureCollector(operational.Object);

        collector.Capture("first", new IOException("one"));
        collector.Capture("second", new TimeoutException("two"));
        var aggregate = Assert.ThrowsExactly<SvantekJobAggregateException>(
            () => collector.ThrowIfAny("job"));
        collector.Capture("later", new InvalidOperationException("three"));
        var cancellationException = new OperationCanceledException(cancellation.Token);

        var observedCancellation = Assert.ThrowsExactly<OperationCanceledException>(
            () => collector.Capture("cancelled", cancellationException));

        Assert.AreSame(cancellationException, observedCancellation);
        Assert.HasCount(2, aggregate.Failures);
        Assert.AreEqual("first", aggregate.Failures[0].Message);
        Assert.AreEqual("second", aggregate.Failures[1].Message);
        operational.Verify(
            commands => commands.HandleException("cancelled", It.IsAny<Exception>()),
            Times.Never);
    }
}
