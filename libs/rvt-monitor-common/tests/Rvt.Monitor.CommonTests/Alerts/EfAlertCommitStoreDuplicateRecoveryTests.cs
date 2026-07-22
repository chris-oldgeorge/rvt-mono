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
        var exception = await Assert.ThrowsExactlyAsync<AlertTransientPersistenceException>(
            () => CreateStore(new PostgresException(
                "provider sentinel connection=secret",
                "ERROR",
                "ERROR",
                "40001"))
                .CommitAsync(CommitRequest()));

        AssertSafe(exception);
    }

    [TestMethod]
    public async Task CommitAsync_DuplicateRecoveryPermanentProviderFailure_IsSanitized()
    {
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateStore(new NpgsqlException(
                "provider sentinel connection=secret destination=ops@example.test"))
                .CommitAsync(CommitRequest()));

        AssertSafe(exception);
    }

    [TestMethod]
    public async Task CommitAsync_DuplicateRecoveryCancellation_PreservesOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellation = new OperationCanceledException(cancellationSource.Token);

        var thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => CreateStore(cancellation).CommitAsync(
                CommitRequest(),
                cancellationSource.Token));

        Assert.AreSame(cancellation, thrown);
        Assert.AreEqual(cancellationSource.Token, thrown.CancellationToken);
    }

    private static EfAlertCommitStore<TestMonitorContext> CreateStore(
        Exception duplicateRecoveryFailure)
    {
        var options = new MonitorDbOptions(
            MonitorDatabaseProvider.PostgreSql,
            new Dictionary<string, string>());
        var contextOptions = new DbContextOptionsBuilder<TestMonitorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new TestMonitorContext(contextOptions, options);
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

        return new EfAlertCommitStore<TestMonitorContext>(
            new FailingDuplicateReadFactory(context, duplicateRecoveryFailure),
            new OccurrenceConflictPolicy());
    }

    private static AlertCommitRequest CommitRequest()
    {
        var sourceKeyHash = Enumerable.Repeat((byte)0x2a, 32).ToArray();
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
}
