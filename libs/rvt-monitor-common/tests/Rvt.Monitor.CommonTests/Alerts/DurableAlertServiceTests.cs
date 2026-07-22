using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class DurableAlertServiceTests
{
    private static readonly DateTime CreatedAt = new(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AcceptAsync_CalculatesIdentityAndReturnsStoreResult(bool isDuplicate)
    {
        var store = new Mock<IAlertCommitStore>();
        AlertCommitRequest? captured = null;
        var commitResult = new AlertCommitResult(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-8222-8222-222222222222"),
            AlertOccurrenceOutcome.Accepted,
            isDuplicate);
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AlertCommitRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(commitResult);
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(CreatedAt));
        var service = new DurableAlertService(store.Object, timeProvider.Object);
        var signal = ValidSignal();

        var result = await service.AcceptAsync(signal);

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
        using var cancellationSource = new CancellationTokenSource();
        CancellationToken captured = default;
        var store = new Mock<IAlertCommitStore>();
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AlertCommitRequest, CancellationToken>((_, cancellationToken) =>
                captured = cancellationToken)
            .ReturnsAsync(CommitResult());
        var service = new DurableAlertService(store.Object, TimeProvider.System);

        await service.AcceptAsync(ValidSignal(), cancellationSource.Token);

        Assert.AreEqual(cancellationSource.Token, captured);
    }

    [TestMethod]
    public async Task AcceptAsync_DoesNotWrapStoreFailure()
    {
        var expected = new InvalidOperationException("store failure");
        var store = new Mock<IAlertCommitStore>();
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);
        var service = new DurableAlertService(store.Object, TimeProvider.System);

        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.AcceptAsync(ValidSignal()));

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public async Task AcceptAsync_RejectsNullSignalBeforeCallingStore()
    {
        await AssertRejectedAsync(null!, typeof(ArgumentNullException));
    }

    [DataTestMethod]
    [DataRow(nameof(AlertSignal.Source))]
    [DataRow(nameof(AlertSignal.SourceEventKey))]
    [DataRow(nameof(AlertSignal.SerialId))]
    [DataRow(nameof(AlertSignal.Field))]
    [DataRow(nameof(AlertSignal.Message))]
    public async Task AcceptAsync_RejectsBlankTextBeforeCallingStore(string propertyName)
    {
        await AssertRejectedAsync(WithText(ValidSignal(), propertyName, " \t"), typeof(ArgumentException));
    }

    [DataTestMethod]
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
        var signal = ValidSignal() with
        {
            Source = new string('s', 128),
            SourceEventKey = new string('k', 512),
            SerialId = new string('i', 128),
            Field = new string('f', 128),
            Message = new string('m', 1024)
        };
        var store = new Mock<IAlertCommitStore>();
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommitResult());
        var service = new DurableAlertService(store.Object, TimeProvider.System);

        await service.AcceptAsync(signal);

        store.Verify(
            x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task AcceptAsync_AcceptsTextAtMaximumUtf16Lengths()
    {
        var signal = ValidSignal() with
        {
            Source = RepeatSurrogatePair(64),
            SourceEventKey = RepeatSurrogatePair(256),
            SerialId = RepeatSurrogatePair(64),
            Field = RepeatSurrogatePair(64),
            Message = RepeatSurrogatePair(512)
        };
        var store = new Mock<IAlertCommitStore>();
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommitResult());
        var service = new DurableAlertService(store.Object, TimeProvider.System);

        await service.AcceptAsync(signal);

        store.Verify(
            x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [DataTestMethod]
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

    [DataTestMethod]
    [DataRow(DateTimeKind.Local)]
    [DataRow(DateTimeKind.Unspecified)]
    public async Task AcceptAsync_RejectsNonUtcEventTimeBeforeCallingStore(DateTimeKind kind)
    {
        var eventTime = DateTime.SpecifyKind(ValidSignal().EventTime, kind);

        await AssertRejectedAsync(
            ValidSignal() with { EventTime = eventTime },
            typeof(ArgumentException));
    }

    [DataTestMethod]
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
        var signal = propertyName == nameof(AlertSignal.Level)
            ? ValidSignal() with { Level = value }
            : ValidSignal() with { Limit = value };

        await AssertRejectedAsync(signal, typeof(ArgumentOutOfRangeException));
    }

    [DataTestMethod]
    [DataRow(AlertType.Offline)]
    [DataRow(AlertType.BatteryAlert)]
    [DataRow(AlertType.BatteryCaution)]
    [DataRow((AlertType)999)]
    public async Task AcceptAsync_RejectsUnsupportedAlertTypesBeforeCallingStore(AlertType alertType)
    {
        await AssertRejectedAsync(
            ValidSignal() with { AlertType = alertType },
            typeof(ArgumentOutOfRangeException));
    }

    [DataTestMethod]
    [DataRow(AlertType.Alert)]
    [DataRow(AlertType.Caution)]
    [DataRow(AlertType.Ignore)]
    public async Task AcceptAsync_AcceptsSupportedAlertTypes(AlertType alertType)
    {
        await AssertAcceptedAsync(ValidSignal() with { AlertType = alertType });
    }

    [DataTestMethod]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(-1)]
    public async Task AcceptAsync_RejectsUnsupportedDeliveryChannelBitsBeforeCallingStore(int channels)
    {
        await AssertRejectedAsync(
            ValidSignal() with { DeliveryChannels = (AlertDeliveryChannels)channels },
            typeof(ArgumentOutOfRangeException));
    }

    [DataTestMethod]
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

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task AcceptAsync_RejectsNonpositiveSuppressionWindowBeforeCallingStore(int ticks)
    {
        await AssertRejectedAsync(
            ValidSignal() with { SuppressionWindow = TimeSpan.FromTicks(ticks) },
            typeof(ArgumentOutOfRangeException));
    }

    [TestMethod]
    public void Constructor_RejectsNullDependencies()
    {
        var store = new Mock<IAlertCommitStore>();

        Assert.ThrowsExactly<ArgumentNullException>(
            () => new DurableAlertService(null!, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new DurableAlertService(store.Object, null!));
    }

    private static async Task AssertRejectedAsync(AlertSignal signal, Type exceptionType)
    {
        var store = new Mock<IAlertCommitStore>(MockBehavior.Strict);
        var service = new DurableAlertService(store.Object, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.AcceptAsync(signal));

        Assert.AreEqual(exceptionType, exception.GetType());
        store.Verify(
            x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async Task AssertAcceptedAsync(AlertSignal signal)
    {
        var store = new Mock<IAlertCommitStore>();
        store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommitResult());
        var service = new DurableAlertService(store.Object, TimeProvider.System);

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
}
