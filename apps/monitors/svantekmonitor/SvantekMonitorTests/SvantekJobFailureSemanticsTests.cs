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
    private static readonly string[] FailureIdentifiers =
        ["StoreMonitors project 1", "StoreMonitors project 3"];

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
        using CancellationTokenSource cancellation = new();
        CancellationToken token = cancellation.Token;
        Mock<IHttpClient> http = new(MockBehavior.Strict);
        http.SetupSequence(client => client.PostAsync(
                It.IsAny<string>(),
                It.IsAny<HttpContent>(),
                token))
            .ReturnsAsync(ProjectsJson)
            .ReturnsAsync(StationsJson);
        Mock<ISvantekMonitorCommands> monitorCommands = new(MockBehavior.Strict);
        List<int> persistedProjects = [];
        monitorCommands.Setup(commands => commands.WriteMonitorListAsync(
                It.IsAny<IReadOnlyList<NoiseMonitorDto>>(),
                token))
            .Callback((IReadOnlyList<NoiseMonitorDto> monitors, CancellationToken _) =>
                persistedProjects.Add(monitors.Single().ProjectId))
            .Returns((IReadOnlyList<NoiseMonitorDto> monitors, CancellationToken _) =>
                monitors.Single().ProjectId is 1 or 3
                    ? Task.FromException(new IOException($"project {monitors.Single().ProjectId} failed"))
                    : Task.CompletedTask);
        Mock<ISvantekOperationalCommands> operational = new(MockBehavior.Strict);
        List<string> recordedIdentifiers = [];
        operational.Setup(commands => commands.HandleException(It.IsAny<string>(), It.IsAny<Exception>()))
            .Callback((string identifier, Exception _) => recordedIdentifiers.Add(identifier));
        StoreMonitorsHandler handler = new(
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
        SvantekJobAggregateException aggregate = (SvantekJobAggregateException)observed;

        CollectionAssert.AreEqual(expected, persistedProjects);
        CollectionAssert.AreEqual(
            FailureIdentifiers,
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
        UnauthorizedAccessException authenticationFailure = new("authentication rejected");
        Mock<IHttpClient> http = new(MockBehavior.Strict);
        http.Setup(client => client.PostAsync(
                "projects-get-data.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .ThrowsAsync(authenticationFailure);
        Mock<ISvantekOperationalCommands> operational = new(MockBehavior.Strict);
        StoreMonitorsHandler handler = new(
            new SvantekHttpGateway(http.Object, "key"),
            Mock.Of<ISvantekMonitorCommands>(),
            operational.Object,
            testLocal: false);

        AdapterException exception = await Assert.ThrowsExactlyAsync<AdapterException>(() => handler.RunAsync(TestContext.CancellationToken));

        Assert.AreSame(authenticationFailure, exception.InnerException, exception.ToString());
        operational.Verify(
            commands => commands.HandleException(It.IsAny<string>(), It.IsAny<Exception>()),
            Times.Never);
    }

    [TestMethod]
    public void FailureCollector_SnapshotsFailuresImmutably_AndNeverCapturesCancellation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Mock<ISvantekOperationalCommands> operational = new(MockBehavior.Strict);
        operational.Setup(commands => commands.HandleException("first", It.IsAny<IOException>()));
        operational.Setup(commands => commands.HandleException("second", It.IsAny<TimeoutException>()));
        operational.Setup(commands => commands.HandleException("later", It.IsAny<InvalidOperationException>()));
        SvantekFailureCollector collector = new(operational.Object);

        collector.Capture("first", new IOException("one"));
        collector.Capture("second", new TimeoutException("two"));
        SvantekJobAggregateException aggregate = Assert.ThrowsExactly<SvantekJobAggregateException>(
            () => collector.ThrowIfAny("job"));
        collector.Capture("later", new InvalidOperationException("three"));
        OperationCanceledException cancellationException = new(cancellation.Token);

        OperationCanceledException observedCancellation = Assert.ThrowsExactly<OperationCanceledException>(
            () => collector.Capture("cancelled", cancellationException));

        Assert.AreSame(cancellationException, observedCancellation);
        Assert.HasCount(2, aggregate.Failures);
        Assert.AreEqual("first", aggregate.Failures[0].Message);
        Assert.AreEqual("second", aggregate.Failures[1].Message);
        operational.Verify(
            commands => commands.HandleException("cancelled", It.IsAny<Exception>()),
            Times.Never);
    }

    public TestContext TestContext { get; set; } = null!;

    private static readonly int[] expected = [1, 2, 3];
}
