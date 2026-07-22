using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmFailureCollectorTests
{
    [TestMethod]
    public void Capture_OperationalRecordingSucceeds_PreservesPrimaryFailure()
    {
        var primary = new IOException("vendor unavailable");
        var operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        operational.Setup(commands => commands.HandleException("monitor=11111", primary));
        var collector = new MyAtmFailureCollector(operational.Object);

        collector.Capture("monitor=11111", primary);
        var aggregate = Assert.ThrowsExactly<MyAtmJobAggregateException>(() =>
            collector.ThrowIfAny("StoreDustLevels"));

        Assert.AreEqual("StoreDustLevels", aggregate.Operation);
        Assert.HasCount(1, aggregate.Failures);
        Assert.AreEqual("monitor=11111", aggregate.Failures[0].Identifier);
        Assert.AreSame(primary, aggregate.Failures[0].Exception);
        Assert.IsNull(aggregate.Failures[0].RecordingException);
        operational.VerifyAll();
    }

    [TestMethod]
    public void Capture_OperationalRecordingFails_PreservesBothFailures()
    {
        var primary = new IOException("vendor unavailable");
        var recording = new InvalidOperationException("database unavailable");
        var operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        operational
            .Setup(commands => commands.HandleException("monitor=11111", primary))
            .Throws(recording);
        var collector = new MyAtmFailureCollector(operational.Object);

        collector.Capture("monitor=11111", primary);
        var aggregate = Assert.ThrowsExactly<MyAtmJobAggregateException>(() =>
            collector.ThrowIfAny("StoreDustLevels"));

        Assert.AreSame(primary, aggregate.Failures.Single().Exception);
        Assert.AreSame(recording, aggregate.Failures.Single().RecordingException);
    }

    [TestMethod]
    public void ThrowIfAny_NoFailures_DoesNotThrow()
    {
        var operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        var collector = new MyAtmFailureCollector(operational.Object);

        collector.ThrowIfAny("StoreDustLevels");

        operational.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void Capture_CallerCancellation_RethrowsWithoutRecording()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = new OperationCanceledException(cancellation.Token);
        var operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        var collector = new MyAtmFailureCollector(operational.Object);

        var thrown = Assert.ThrowsExactly<OperationCanceledException>(() =>
            collector.Capture("monitor=11111", failure, cancellation.Token));

        Assert.AreSame(failure, thrown);
        operational.VerifyNoOtherCalls();
    }
}
