// File summary: Verifies the SQL-translatable ownership predicate stays equivalent to the in-memory window.
// Major updates:
// - 2026-07-17 pending Flattened theory data to nullable scalar values for warning-free xUnit discovery.
// - 2026-07-14 pending Added equivalence and SQL-translation coverage for MonitorOwnershipWindowResolver.OwnsAt.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Application.Monitors;

namespace RvtPortal.Spa.Tests;

public sealed class MonitorOwnershipWindowTests
{
    private static readonly DateTime Anchor = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string, DateTime, DateTime?, DateTime?, DateTime?> DeploymentShapes() =>
    new()
    {
        { "open-ended, no contract", Anchor.AddDays(-10), null, null, null },
        { "closed, no contract", Anchor.AddDays(-10), Anchor.AddDays(-2), null, null },
        { "contract starts later than deployment", Anchor.AddDays(-10), null, Anchor.AddDays(-5), null },
        { "contract starts before deployment", Anchor.AddDays(-10), null, Anchor.AddDays(-30), null },
        { "contract off-hire is a whole day", Anchor.AddDays(-10), null, Anchor.AddDays(-30), Anchor.Date },
        { "contract off-hire has a time", Anchor.AddDays(-10), null, Anchor.AddDays(-30), Anchor.AddHours(-1) },
        { "deployment ends before contract", Anchor.AddDays(-10), Anchor.AddDays(-3), Anchor.AddDays(-30), Anchor.AddDays(10) },
        { "contract ends before deployment", Anchor.AddDays(-10), Anchor.AddDays(10), Anchor.AddDays(-30), Anchor.AddDays(-3) },
        { "starts exactly at the timestamp", Anchor, null, null, null },
        { "ends exactly at the timestamp", Anchor.AddDays(-10), Anchor, null, null }
    };

    [Theory]
    [MemberData(nameof(DeploymentShapes))]
    // Function summary: Verifies the translatable predicate agrees with the in-memory window for every shape.
    public void OwnsAt_MatchesInMemoryWindow(
        string shape,
        DateTime deploymentStart,
        DateTime? deploymentEnd,
        DateTime? contractOnHire,
        DateTime? contractOffHire)
    {
        var contract = contractOnHire.HasValue
            ? Contract(contractOnHire.Value, contractOffHire)
            : null;
        var deployment = Deployment(deploymentStart, deploymentEnd, contract);
        DateTime?[] caps = [null, Anchor.AddDays(1), Anchor.AddHours(-1)];
        DateTime[] timestamps =
        [
            Anchor.AddDays(-40), Anchor.AddDays(-20), Anchor.AddDays(-4), Anchor.AddHours(-1),
            Anchor, Anchor.AddHours(1), Anchor.AddDays(4), Anchor.AddDays(40)
        ];

        foreach (var cap in caps)
        {
            foreach (var timestamp in timestamps)
            {
                var expected = MonitorOwnershipWindowResolver.ForDeployment(deployment, cap).Contains(timestamp);
                var actual = MonitorOwnershipWindowResolver.OwnsAt(timestamp, cap).Compile()(deployment);

                Assert.True(
                    expected == actual,
                    $"[{shape}] cap={cap:o} timestamp={timestamp:o}: window said {expected}, predicate said {actual}");
            }
        }
    }

    // Function summary: Builds a deployment with the supplied window shape.
    private static Deployment Deployment(DateTime start, DateTime? end, Contract? contract)
    {
        return new Deployment
        {
            Id = Guid.NewGuid(),
            MonitorId = Guid.NewGuid(),
            ContractId = contract?.Id ?? Guid.NewGuid(),
            StartDate = start,
            EndDate = end,
            Contract = contract!
        };
    }

    // Function summary: Builds a contract with the supplied hire window.
    private static Contract Contract(DateTime onHire, DateTime? offHire)
    {
        return new Contract
        {
            Id = Guid.NewGuid(),
            ContractNumber = "OWN-001",
            CompanyId = Guid.NewGuid(),
            OnHireDate = onHire,
            OffHireDate = offHire
        };
    }
}
