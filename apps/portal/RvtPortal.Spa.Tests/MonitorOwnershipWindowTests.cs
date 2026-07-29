// File summary: Verifies the SQL-translatable ownership predicate stays equivalent to the in-memory window.
// Major updates:
// - 2026-07-17 pending Flattened theory data to nullable scalar values for warning-free xUnit discovery.
// - 2026-07-14 pending Added equivalence and SQL-translation coverage for MonitorOwnershipWindowResolver.OwnsAt.

using RVT.Entities;
using RvtPortal.Spa.Application.Monitors;

namespace RvtPortal.Spa.Tests;

public sealed class MonitorOwnershipWindowTests
{
    private static readonly DateTime anchor = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string, DateTime, DateTime?, DateTime?, DateTime?> DeploymentShapes() =>
    new()
    {
        { "open-ended, no contract", anchor.AddDays(-10), null, null, null },
        { "closed, no contract", anchor.AddDays(-10), anchor.AddDays(-2), null, null },
        { "contract starts later than deployment", anchor.AddDays(-10), null, anchor.AddDays(-5), null },
        { "contract starts before deployment", anchor.AddDays(-10), null, anchor.AddDays(-30), null },
        { "contract off-hire is a whole day", anchor.AddDays(-10), null, anchor.AddDays(-30), anchor.Date },
        { "contract off-hire has a time", anchor.AddDays(-10), null, anchor.AddDays(-30), anchor.AddHours(-1) },
        { "deployment ends before contract", anchor.AddDays(-10), anchor.AddDays(-3), anchor.AddDays(-30), anchor.AddDays(10) },
        { "contract ends before deployment", anchor.AddDays(-10), anchor.AddDays(10), anchor.AddDays(-30), anchor.AddDays(-3) },
        { "starts exactly at the timestamp", anchor, null, null, null },
        { "ends exactly at the timestamp", anchor.AddDays(-10), anchor, null, null }
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
        Contract? contract = contractOnHire.HasValue
            ? Contract(contractOnHire.Value, contractOffHire)
            : null;
        Deployment deployment = Deployment(deploymentStart, deploymentEnd, contract);
        DateTime?[] caps = [null, anchor.AddDays(1), anchor.AddHours(-1)];
        DateTime[] timestamps =
        [
            anchor.AddDays(-40), anchor.AddDays(-20), anchor.AddDays(-4), anchor.AddHours(-1),
            anchor, anchor.AddHours(1), anchor.AddDays(4), anchor.AddDays(40)
        ];

        foreach (DateTime? cap in caps)
        {
            foreach (DateTime timestamp in timestamps)
            {
                bool expected = MonitorOwnershipWindowResolver.ForDeployment(deployment, cap).Contains(timestamp);
                bool actual = MonitorOwnershipWindowResolver.OwnsAt(timestamp, cap).Compile()(deployment);

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
