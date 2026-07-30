using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class EfAlertCommitStoreUnknownMonitorTests
{
    [TestMethod]
    public async Task CommitAsync_UnknownMonitorSerial_ThrowsDistinctPermanentException()
    {
        InMemoryContextFactory factory = NewFactory();
        SeedMonitor(factory, "23423");
        EfAlertCommitStore<TestMonitorContext> store = new(factory, new AcceptAllPolicy());

        AlertUnknownMonitorException exception = await Assert.ThrowsExactlyAsync<AlertUnknownMonitorException>(
            () => store.CommitAsync(CommitRequest("99999"), TestContext.CancellationToken));

        Assert.AreEqual("99999", exception.SerialId);
        Assert.IsNotInstanceOfType<AlertTransientPersistenceException>(exception);
    }

    [TestMethod]
    public async Task CommitAsync_KnownMonitorSerialWithSurroundingWhitespace_Commits()
    {
        InMemoryContextFactory factory = NewFactory();
        SeedMonitor(factory, "23423");
        EfAlertCommitStore<TestMonitorContext> store = new(factory, new AcceptAllPolicy());

        AlertCommitResult result = await store.CommitAsync(
            CommitRequest(" 23423 "),
            TestContext.CancellationToken);

        Assert.AreEqual(AlertOccurrenceOutcome.Accepted, result.Outcome);
        Assert.IsFalse(result.IsDuplicate);
    }

    private static InMemoryContextFactory NewFactory()
    {
        MonitorDbOptions options = new(new Dictionary<string, string>());
        DbContextOptions<TestMonitorContext> contextOptions = new DbContextOptionsBuilder<TestMonitorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new InMemoryContextFactory(contextOptions, options);
    }

    private static void SeedMonitor(InMemoryContextFactory factory, string serialId)
    {
        using TestMonitorContext context = factory.CreateDbContext();
        context.Monitors.Add(new MonitorEntity
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            SerialId = serialId,
            FleetNr = "test-fleet",
            ListedAtTime = DateTime.UnixEpoch,
            Model = "SWARM",
            Manufacturer = "Omnidots",
            FirmwareVersion = "1.0",
            TypeOfMonitor = 2
        });
        context.SaveChanges();
    }

    private static AlertCommitRequest CommitRequest(string serialId)
    {
        byte[] sourceKeyHash = [.. Enumerable.Repeat((byte)0x2a, 32)];
        return new AlertCommitRequest(
            new AlertSignal(
                "omnidots.webhook",
                "body-hash",
                new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc),
                serialId,
                AlertType.Alert,
                "Vtop",
                80,
                70,
                60,
                "Vibration alarm.",
                AlertDeliveryChannels.None,
                TimeSpan.FromHours(1)),
            sourceKeyHash,
            AlertIdentity.CreateNotificationId("omnidots.webhook", sourceKeyHash),
            new DateTime(2026, 7, 15, 10, 1, 0, DateTimeKind.Utc));
    }

    private sealed class TestMonitorContext(
        DbContextOptions<TestMonitorContext> options,
        MonitorDbOptions monitorOptions)
        : MonitorDbContextBase(options, monitorOptions);

    private sealed class InMemoryContextFactory(
        DbContextOptions<TestMonitorContext> contextOptions,
        MonitorDbOptions monitorOptions)
        : IMonitorDbContextFactory<TestMonitorContext>
    {
        public TestMonitorContext CreateDbContext() => new(contextOptions, monitorOptions);
    }

    private sealed class AcceptAllPolicy : IAlertAcceptancePolicy
    {
        public AlertOccurrenceOutcome Evaluate(
            AlertType incoming,
            IReadOnlyCollection<AlertType> recentAlertTypes) =>
            AlertOccurrenceOutcome.Accepted;
    }

    public TestContext TestContext { get; set; } = null!;
}
