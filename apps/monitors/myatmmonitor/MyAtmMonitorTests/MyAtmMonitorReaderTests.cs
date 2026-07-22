using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmMonitorReaderTests
{
    [TestMethod]
    public void ReadMonitors_ForwardsTheCustomerScopeToTheQueryPort()
    {
        var queries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        var operations = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        var expected = MyAtmFixture.CustomerDeviceDtos(lastDataTime: null, singleItem: true);
        queries.Setup(query => query.ReadMonitorList(9, null)).Returns(expected);
        var reader = new MyAtmMonitorReader(queries.Object, operations.Object, testLocal: false);

        var monitors = reader.ReadMonitors(9);

        CollectionAssert.AreEqual(expected, monitors!);
        queries.VerifyAll();
        operations.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void ReadMonitors_WhenTheQueryFails_RecordsTheFailureAndRethrowsTheOriginalException()
    {
        var queries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        var operations = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        var expected = new InvalidOperationException("monitor query failed");
        queries.Setup(query => query.ReadMonitorList(9, null)).Throws(expected);
        operations.Setup(command => command.HandleException("ReadMonitors", expected));
        var reader = new MyAtmMonitorReader(queries.Object, operations.Object, testLocal: false);

        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => reader.ReadMonitors(9));

        Assert.AreSame(expected, actual);
        queries.VerifyAll();
        operations.VerifyAll();
    }

    [TestMethod]
    public void ReadMonitors_WhenOperationalRecordingFails_RethrowsTheOriginalQueryException()
    {
        var queries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        var operations = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        var expected = new InvalidOperationException("monitor query failed");
        queries.Setup(query => query.ReadMonitorList(9, null)).Throws(expected);
        operations.Setup(command => command.HandleException("ReadMonitors", expected))
            .Throws(new InvalidOperationException("operational recording failed"));
        var reader = new MyAtmMonitorReader(queries.Object, operations.Object, testLocal: false);

        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => reader.ReadMonitors(9));

        Assert.AreSame(expected, actual);
        queries.VerifyAll();
        operations.VerifyAll();
    }

    [TestMethod]
    public void ReadMonitors_WhenCallerCancellationWasRequested_PropagatesWithoutOperationalRecording()
    {
        var queries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        var operations = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        var cancellation = new OperationCanceledException(new CancellationToken(canceled: true));
        queries.Setup(query => query.ReadMonitorList(9, null)).Throws(cancellation);
        var reader = new MyAtmMonitorReader(queries.Object, operations.Object, testLocal: false);

        var actual = Assert.ThrowsExactly<OperationCanceledException>(() => reader.ReadMonitors(9));

        Assert.AreSame(cancellation, actual);
        queries.VerifyAll();
        operations.VerifyNoOtherCalls();
    }
}
