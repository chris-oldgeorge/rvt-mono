using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class DurableAlertServiceTests
{
    private static readonly DateTime CreatedAt = new(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AcceptAsync_CalculatesIdentityAndReturnsStoreResult(bool isDuplicate)
    {
        Mock<IAlertCommitStore> store = new();
        AlertCommitRequest? captured = null;
        AlertCommitResult commitResult = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-8222-8222-222222222222"),
            AlertOccurrenceOutcome.Accepted,
            isDuplicate);
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AlertCommitRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(commitResult);
        Mock<TimeProvider> timeProvider = new();
        timeProvider.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(CreatedAt));
        DurableAlertService service = new(store.Object, timeProvider.Object);
        AlertSignal signal = ValidSignal();

        AlertIngressResult result = await service.AcceptAsync(signal, TestContext.CancellationToken);

        Assert.IsNotNull(captured);
        Assert.AreSame(signal, captured.Signal);
        CollectionAssert.AreEqual(
            AlertIdentity.CreateSourceKeyHash("body-digest"),
            captured.SourceKeyHash);
        Assert.AreEqual(
            AlertIdentity.CreateNotificationId("omnidots.webhook", captured.SourceKeyHash),
            captured.NotificationId);
        Assert.AreEqual(CreatedAt, captured.CreatedAt);
        Assert.AreEqual(DateTimeKind.Utc, captured.CreatedAt.Kind);
        Assert.AreEqual(commitResult.OccurrenceId, result.OccurrenceId);
        Assert.AreEqual(commitResult.NotificationId, result.NotificationId);
        Assert.AreEqual(commitResult.Outcome, result.Outcome);
        Assert.AreEqual(isDuplicate, result.IsDuplicate);
    }

    [TestMethod]
    public async Task AcceptAsync_PassesCallerCancellationTokenToStore()
    {
        using CancellationTokenSource cancellationSource = new();
        CancellationToken captured = default;
        Mock<IAlertCommitStore> store = new();
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AlertCommitRequest, CancellationToken>((_, cancellationToken) =>
                captured = cancellationToken)
            .ReturnsAsync(CommitResult());
        DurableAlertService service = new(store.Object, TimeProvider.System);

        await service.AcceptAsync(ValidSignal(), cancellationSource.Token);

        Assert.AreEqual(cancellationSource.Token, captured);
    }

    [TestMethod]
    public async Task AcceptAsync_DoesNotWrapStoreFailure()
    {
        InvalidOperationException expected = new("store failure");
        Mock<IAlertCommitStore> store = new();
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);
        DurableAlertService service = new(store.Object, TimeProvider.System);

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.AcceptAsync(ValidSignal(), TestContext.CancellationToken));

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public async Task AcceptAsync_RejectsNullSignalBeforeCallingStore()
    {
        await AssertRejectedAsync(null!, typeof(ArgumentNullException));
    }

    [TestMethod]
    [DataRow(nameof(AlertSignal.Source))]
    [DataRow(nameof(AlertSignal.SourceEventKey))]
    [DataRow(nameof(AlertSignal.SerialId))]
    [DataRow(nameof(AlertSignal.Field))]
    [DataRow(nameof(AlertSignal.Message))]
    public async Task AcceptAsync_RejectsBlankTextBeforeCallingStore(string propertyName)
    {
        await AssertRejectedAsync(WithText(ValidSignal(), propertyName, " \t"), typeof(ArgumentException));
    }

    [TestMethod]
    [DataRow(nameof(AlertSignal.Source), 129)]
    [DataRow(nameof(AlertSignal.SourceEventKey), 513)]
    [DataRow(nameof(AlertSignal.SerialId), 129)]
    [DataRow(nameof(AlertSignal.Field), 129)]
    [DataRow(nameof(AlertSignal.Message), 1025)]
    public async Task AcceptAsync_RejectsOversizedTextBeforeCallingStore(
        string propertyName,
        int length)
    {
        await AssertRejectedAsync(
            WithText(ValidSignal(), propertyName, new string('x', length)),
            typeof(ArgumentException));
    }

    [TestMethod]
    public async Task AcceptAsync_AcceptsTextAtMaximumLengths()
    {
        AlertSignal signal = ValidSignal() with
        {
            Source = new string('s', 128),
            SourceEventKey = new string('k', 512),
            SerialId = new string('i', 128),
            Field = new string('f', 128),
            Message = new string('m', 1024)
        };
        Mock<IAlertCommitStore> store = new();
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommitResult());
        DurableAlertService service = new(store.Object, TimeProvider.System);

        await service.AcceptAsync(signal, TestContext.CancellationToken);

        store.Verify(
            x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task AcceptAsync_AcceptsTextAtMaximumUtf16Lengths()
    {
        AlertSignal signal = ValidSignal() with
        {
            Source = RepeatSurrogatePair(64),
            SourceEventKey = RepeatSurrogatePair(256),
            SerialId = RepeatSurrogatePair(64),
            Field = RepeatSurrogatePair(64),
            Message = RepeatSurrogatePair(512)
        };
        Mock<IAlertCommitStore> store = new();
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommitResult());
        DurableAlertService service = new(store.Object, TimeProvider.System);

        await service.AcceptAsync(signal, TestContext.CancellationToken);

        store.Verify(
            x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    [DataRow(nameof(AlertSignal.Source), 65)]
    [DataRow(nameof(AlertSignal.SourceEventKey), 257)]
    [DataRow(nameof(AlertSignal.SerialId), 65)]
    [DataRow(nameof(AlertSignal.Field), 65)]
    [DataRow(nameof(AlertSignal.Message), 513)]
    public async Task AcceptAsync_RejectsTextWhoseUtf16LengthExceedsMaximumBeforeCallingStore(
        string propertyName,
        int repetitions)
    {
        await AssertRejectedAsync(
            WithText(ValidSignal(), propertyName, RepeatSurrogatePair(repetitions)),
            typeof(ArgumentException));
    }

    [TestMethod]
    [DataRow(DateTimeKind.Local)]
    [DataRow(DateTimeKind.Unspecified)]
    public async Task AcceptAsync_RejectsNonUtcEventTimeBeforeCallingStore(DateTimeKind kind)
    {
        DateTime eventTime = DateTime.SpecifyKind(ValidSignal().EventTime, kind);

        await AssertRejectedAsync(
            ValidSignal() with { EventTime = eventTime },
            typeof(ArgumentException));
    }

    [TestMethod]
    [DataRow(nameof(AlertSignal.Level), double.NaN)]
    [DataRow(nameof(AlertSignal.Level), double.PositiveInfinity)]
    [DataRow(nameof(AlertSignal.Level), double.NegativeInfinity)]
    [DataRow(nameof(AlertSignal.Limit), double.NaN)]
    [DataRow(nameof(AlertSignal.Limit), double.PositiveInfinity)]
    [DataRow(nameof(AlertSignal.Limit), double.NegativeInfinity)]
    public async Task AcceptAsync_RejectsNonFiniteNumbersBeforeCallingStore(
        string propertyName,
        double value)
    {
        AlertSignal signal = propertyName == nameof(AlertSignal.Level)
            ? ValidSignal() with { Level = value }
            : ValidSignal() with { Limit = value };

        await AssertRejectedAsync(signal, typeof(ArgumentOutOfRangeException));
    }

    [TestMethod]
    [DataRow((AlertType)999)]
    public async Task AcceptAsync_RejectsUnsupportedAlertTypesBeforeCallingStore(AlertType alertType)
    {
        await AssertRejectedAsync(
            ValidSignal() with { AlertType = alertType },
            typeof(ArgumentOutOfRangeException));
    }

    [TestMethod]
    [DataRow(AlertType.Alert)]
    [DataRow(AlertType.Caution)]
    [DataRow(AlertType.Ignore)]
    public async Task AcceptAsync_AcceptsSupportedAlertTypes(AlertType alertType)
    {
        await AssertAcceptedAsync(ValidSignal() with { AlertType = alertType });
    }

    [TestMethod]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(-1)]
    public async Task AcceptAsync_RejectsUnsupportedDeliveryChannelBitsBeforeCallingStore(int channels)
    {
        await AssertRejectedAsync(
            ValidSignal() with { DeliveryChannels = (AlertDeliveryChannels)channels },
            typeof(ArgumentOutOfRangeException));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    public async Task AcceptAsync_AcceptsSupportedDeliveryChannelMasks(int channels)
    {
        await AssertAcceptedAsync(
            ValidSignal() with { DeliveryChannels = (AlertDeliveryChannels)channels });
    }

    [TestMethod]
    public async Task AcceptAsync_RejectsNegativeAveragingPeriodBeforeCallingStore()
    {
        await AssertRejectedAsync(
            ValidSignal() with { AveragingPeriod = -1 },
            typeof(ArgumentOutOfRangeException));
    }

    [TestMethod]
    public async Task AcceptAsync_RejectsNegativeSuppressionWindowBeforeCallingStore()
    {
        await AssertRejectedAsync(
            ValidSignal() with { SuppressionWindow = TimeSpan.FromTicks(-1) },
            typeof(ArgumentOutOfRangeException));
    }

    [TestMethod]
    public async Task AcceptAsync_AcceptsZeroSuppressionWindowForSourceLatchedSignals()
    {
        await AssertAcceptedAsync(
            ValidSignal() with { SuppressionWindow = TimeSpan.Zero });
    }

    [TestMethod]
    public void Constructor_RejectsNullDependencies()
    {
        Mock<IAlertCommitStore> store = new();

        Assert.ThrowsExactly<ArgumentNullException>(
            () => new DurableAlertService(null!, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new DurableAlertService(store.Object, null!));
    }

    private static async Task AssertRejectedAsync(AlertSignal signal, Type exceptionType)
    {
        Mock<IAlertCommitStore> store = new(MockBehavior.Strict);
        DurableAlertService service = new(store.Object, TimeProvider.System);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.AcceptAsync(signal));

        Assert.AreEqual(exceptionType, exception.GetType());
        store.Verify(
            x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async Task AssertAcceptedAsync(AlertSignal signal)
    {
        Mock<IAlertCommitStore> store = new();
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommitResult());
        DurableAlertService service = new(store.Object, TimeProvider.System);

        await service.AcceptAsync(signal);

        store.Verify(
            x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AlertSignal WithText(AlertSignal signal, string propertyName, string value) =>
        propertyName switch
        {
            nameof(AlertSignal.Source) => signal with { Source = value },
            nameof(AlertSignal.SourceEventKey) => signal with { SourceEventKey = value },
            nameof(AlertSignal.SerialId) => signal with { SerialId = value },
            nameof(AlertSignal.Field) => signal with { Field = value },
            nameof(AlertSignal.Message) => signal with { Message = value },
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, null)
        };

    private static AlertCommitResult CommitResult() =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-8222-8222-222222222222"),
            AlertOccurrenceOutcome.Accepted,
            IsDuplicate: false);

    private static string RepeatSurrogatePair(int count) =>
        string.Concat(Enumerable.Repeat("\U0001f600", count));

    private static AlertSignal ValidSignal() =>
        new(
            "omnidots.webhook",
            "body-digest",
            new DateTime(2026, 7, 15, 9, 59, 0, DateTimeKind.Utc),
            "23423",
            AlertType.Alert,
            "vtop x",
            12,
            10,
            0,
            "Vibration Alert vtop x level=12 limit=10",
            AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms,
            TimeSpan.FromMinutes(5));

    public TestContext TestContext { get; set; } = null!;
}
