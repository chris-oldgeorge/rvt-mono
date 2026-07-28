using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Model.Dto;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmMonitorReaderTests
{
    [TestMethod]
    public void ReadMonitors_ForwardsTheCustomerScopeToTheQueryPort()
    {
        Mock<IMyAtmMonitorQueries> queries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        Mock<IMyAtmOperationalCommands> operations = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        List<DustMonitorDto> expected = MyAtmFixture.CustomerDeviceDtos(lastDataTime: null, singleItem: true);
        queries.Setup(query => query.ReadMonitorList(9, null)).Returns(expected);
        MyAtmMonitorReader reader = new MyAtmMonitorReader(queries.Object, operations.Object, testLocal: false);

        List<DustMonitorDto>? monitors = reader.ReadMonitors(9);

        CollectionAssert.AreEqual(expected, monitors!);
        queries.VerifyAll();
        operations.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void ReadMonitors_WhenTheQueryFails_RecordsTheFailureAndRethrowsTheOriginalException()
    {
        Mock<IMyAtmMonitorQueries> queries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        Mock<IMyAtmOperationalCommands> operations = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        InvalidOperationException expected = new InvalidOperationException("monitor query failed");
        queries.Setup(query => query.ReadMonitorList(9, null)).Throws(expected);
        operations.Setup(command => command.HandleException("ReadMonitors", expected));
        MyAtmMonitorReader reader = new MyAtmMonitorReader(queries.Object, operations.Object, testLocal: false);

        InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => reader.ReadMonitors(9));

        Assert.AreSame(expected, actual);
        queries.VerifyAll();
        operations.VerifyAll();
    }

    [TestMethod]
    public void ReadMonitors_WhenOperationalRecordingFails_RethrowsTheOriginalQueryException()
    {
        Mock<IMyAtmMonitorQueries> queries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        Mock<IMyAtmOperationalCommands> operations = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        InvalidOperationException expected = new InvalidOperationException("monitor query failed");
        queries.Setup(query => query.ReadMonitorList(9, null)).Throws(expected);
        operations.Setup(command => command.HandleException("ReadMonitors", expected))
            .Throws(new InvalidOperationException("operational recording failed"));
        MyAtmMonitorReader reader = new MyAtmMonitorReader(queries.Object, operations.Object, testLocal: false);

        InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => reader.ReadMonitors(9));

        Assert.AreSame(expected, actual);
        queries.VerifyAll();
        operations.VerifyAll();
    }

    [TestMethod]
    public void ReadMonitors_WhenCallerCancellationWasRequested_PropagatesWithoutOperationalRecording()
    {
        Mock<IMyAtmMonitorQueries> queries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        Mock<IMyAtmOperationalCommands> operations = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        OperationCanceledException cancellation = new OperationCanceledException(new CancellationToken(canceled: true));
        queries.Setup(query => query.ReadMonitorList(9, null)).Throws(cancellation);
        MyAtmMonitorReader reader = new MyAtmMonitorReader(queries.Object, operations.Object, testLocal: false);

        OperationCanceledException actual = Assert.ThrowsExactly<OperationCanceledException>(() => reader.ReadMonitors(9));

        Assert.AreSame(cancellation, actual);
        queries.VerifyAll();
        operations.VerifyNoOtherCalls();
    }
}
