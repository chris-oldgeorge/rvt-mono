using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class EfAlertCommitStoreDuplicateRecoveryTests
{
    [TestMethod]
    public async Task CommitAsync_DuplicateRecoverySerializationFailure_IsClassifiedTransient()
    {
        AlertTransientPersistenceException exception = await Assert.ThrowsExactlyAsync<AlertTransientPersistenceException>(
            () => CreateStore(new PostgresException(
                "provider sentinel connection=secret",
                "ERROR",
                "ERROR",
                "40001"))
                .CommitAsync(CommitRequest(), TestContext.CancellationToken));

        AssertSafe(exception);
    }

    [TestMethod]
    public async Task CommitAsync_DuplicateRecoveryPermanentProviderFailure_IsSanitized()
    {
        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateStore(new NpgsqlException(
                "provider sentinel connection=secret destination=ops@example.test"))
                .CommitAsync(CommitRequest(), TestContext.CancellationToken));

        AssertSafe(exception);
    }

    [TestMethod]
    public async Task CommitAsync_DuplicateRecoveryCancellation_PreservesOperationCanceledException()
    {
        using CancellationTokenSource cancellationSource = new();
        OperationCanceledException cancellation = new(cancellationSource.Token);

        OperationCanceledException thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => CreateStore(cancellation).CommitAsync(
                CommitRequest(),
                cancellationSource.Token));

        Assert.AreSame(cancellation, thrown);
        Assert.AreEqual(cancellationSource.Token, thrown.CancellationToken);
    }

    [TestMethod]
    public async Task CommitAsync_PassThroughFailure_PreservesTheOriginalStackTrace()
    {
        EfAlertCommitStore<TestMonitorContext> store = CreateStore(
            new PassThroughFailurePolicy());

        TimeoutException thrown = await Assert.ThrowsExactlyAsync<TimeoutException>(
            () => store.CommitAsync(CommitRequest(), TestContext.CancellationToken));

        Assert.IsNotNull(thrown.StackTrace);
        Assert.Contains(nameof(PassThroughFailurePolicy.Evaluate), thrown.StackTrace, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task CommitAsync_Cancellation_IsRethrownBeforeClassification()
    {
        using CancellationTokenSource cancellationSource = new();

        OperationCanceledException thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => CreateStore(new CancellingPolicy(cancellationSource.Token)).CommitAsync(
                CommitRequest(),
                cancellationSource.Token));

        Assert.IsNotNull(thrown.StackTrace);
        Assert.Contains(nameof(CancellingPolicy.Evaluate), thrown.StackTrace, StringComparison.Ordinal);
    }

    private static EfAlertCommitStore<TestMonitorContext> CreateStore(
        Exception duplicateRecoveryFailure) =>
        new(
            new FailingDuplicateReadFactory(SeededContext(), duplicateRecoveryFailure),
            new OccurrenceConflictPolicy());

    private static EfAlertCommitStore<TestMonitorContext> CreateStore(IAlertAcceptancePolicy policy) =>
        new(
            new FailingDuplicateReadFactory(SeededContext(), new InvalidOperationException("unreachable")),
            policy);

    private static TestMonitorContext SeededContext()
    {
        MonitorDbOptions options = new(new Dictionary<string, string>());
        DbContextOptions<TestMonitorContext> contextOptions = new DbContextOptionsBuilder<TestMonitorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        TestMonitorContext context = new(contextOptions, options);
        context.Monitors.Add(new MonitorEntity
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            SerialId = "23423",
            FleetNr = "test-fleet",
            ListedAtTime = DateTime.UnixEpoch,
            Model = "SWARM",
            Manufacturer = "Omnidots",
            FirmwareVersion = "1.0",
            TypeOfMonitor = 2
        });
        context.SaveChanges();
        return context;
    }

    private static AlertCommitRequest CommitRequest()
    {
        byte[] sourceKeyHash = [.. Enumerable.Repeat((byte)0x2a, 32)];
        return new AlertCommitRequest(
            new AlertSignal(
                "omnidots.webhook",
                "body-hash",
                new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc),
                "23423",
                AlertType.Ignore,
                "Vtop",
                0,
                5,
                60,
                "Ignored vibration alarm.",
                AlertDeliveryChannels.None,
                TimeSpan.FromHours(1)),
            sourceKeyHash,
            AlertIdentity.CreateNotificationId("omnidots.webhook", sourceKeyHash),
            new DateTime(2026, 7, 15, 10, 1, 0, DateTimeKind.Utc));
    }

    private static PostgresException OccurrenceConflict() =>
        new(
            "duplicate occurrence provider sentinel",
            "ERROR",
            "ERROR",
            "23505",
            detail: null,
            hint: null,
            position: 0,
            internalPosition: 0,
            internalQuery: null,
            where: null,
            schemaName: null,
            tableName: "alert_occurrence",
            columnName: null,
            dataTypeName: null,
            constraintName: "uq_alert_occurrence_source_key",
            file: null,
            line: null,
            routine: null);

    private static void AssertSafe(Exception exception)
    {
        Assert.IsFalse(exception.Message.Contains("provider sentinel", StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(exception.Message.Contains("ops@example.test", StringComparison.Ordinal));
    }

    private sealed class TestMonitorContext(
        DbContextOptions<TestMonitorContext> options,
        MonitorDbOptions monitorOptions)
        : MonitorDbContextBase(options, monitorOptions);

    private sealed class FailingDuplicateReadFactory(
        TestMonitorContext firstContext,
        Exception duplicateRecoveryFailure)
        : IMonitorDbContextFactory<TestMonitorContext>
    {
        private int callCount;

        public TestMonitorContext CreateDbContext()
        {
            callCount++;
            return callCount == 1
                ? firstContext
                : throw duplicateRecoveryFailure;
        }
    }

    private sealed class OccurrenceConflictPolicy : IAlertAcceptancePolicy
    {
        public AlertOccurrenceOutcome Evaluate(
            AlertType incoming,
            IReadOnlyCollection<AlertType> recentAlertTypes) =>
            throw OccurrenceConflict();
    }

    private sealed class PassThroughFailurePolicy : IAlertAcceptancePolicy
    {
        public AlertOccurrenceOutcome Evaluate(
            AlertType incoming,
            IReadOnlyCollection<AlertType> recentAlertTypes) =>
            throw new TimeoutException("Alert acceptance stalled.");
    }

    private sealed class CancellingPolicy(CancellationToken cancellationToken) : IAlertAcceptancePolicy
    {
        public AlertOccurrenceOutcome Evaluate(
            AlertType incoming,
            IReadOnlyCollection<AlertType> recentAlertTypes) =>
            throw new OperationCanceledException(cancellationToken);
    }

    public TestContext TestContext { get; set; } = null!;
}
