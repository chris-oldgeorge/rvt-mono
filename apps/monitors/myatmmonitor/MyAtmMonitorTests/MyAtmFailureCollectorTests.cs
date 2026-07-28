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
        IOException primary = new("vendor unavailable");
        Mock<IMyAtmOperationalCommands> operational = new(MockBehavior.Strict);
        operational.Setup(commands => commands.HandleException("monitor=11111", primary));
        MyAtmFailureCollector collector = new(operational.Object);

        collector.Capture("monitor=11111", primary);
        MyAtmJobAggregateException aggregate = Assert.ThrowsExactly<MyAtmJobAggregateException>(() =>
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
        IOException primary = new("vendor unavailable");
        InvalidOperationException recording = new("database unavailable");
        Mock<IMyAtmOperationalCommands> operational = new(MockBehavior.Strict);
        operational
            .Setup(commands => commands.HandleException("monitor=11111", primary))
            .Throws(recording);
        MyAtmFailureCollector collector = new(operational.Object);

        collector.Capture("monitor=11111", primary);
        MyAtmJobAggregateException aggregate = Assert.ThrowsExactly<MyAtmJobAggregateException>(() =>
            collector.ThrowIfAny("StoreDustLevels"));

        Assert.AreSame(primary, aggregate.Failures.Single().Exception);
        Assert.AreSame(recording, aggregate.Failures.Single().RecordingException);
    }

    [TestMethod]
    public void ThrowIfAny_NoFailures_DoesNotThrow()
    {
        Mock<IMyAtmOperationalCommands> operational = new(MockBehavior.Strict);
        MyAtmFailureCollector collector = new(operational.Object);

        collector.ThrowIfAny("StoreDustLevels");

        operational.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void Capture_CallerCancellation_RethrowsWithoutRecording()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        OperationCanceledException failure = new(cancellation.Token);
        Mock<IMyAtmOperationalCommands> operational = new(MockBehavior.Strict);
        MyAtmFailureCollector collector = new(operational.Object);

        OperationCanceledException thrown = Assert.ThrowsExactly<OperationCanceledException>(() =>
            collector.Capture("monitor=11111", failure, cancellation.Token));

        Assert.AreSame(failure, thrown);
        operational.VerifyNoOtherCalls();
    }
}
