// File summary: Verifies the SQL-translatable ownership predicate stays equivalent to the in-memory window.
// Major updates:
// - 2026-07-17 pending Flattened theory data to nullable scalar values for warning-free xUnit discovery.
// - 2026-07-14 pending Added equivalence and SQL-translation coverage for MonitorOwnershipWindowResolver.OwnsAt.

using RVT.Entities;
using RvtPortal.Spa.Application.Monitors;

namespace RvtPortal.Spa.Tests;

public sealed class MonitorOwnershipWindowTests
{
    private static readonly DateTime _anchor = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string, DateTime, DateTime?, DateTime?, DateTime?> DeploymentShapes() =>
    new()
    {
        { "open-ended, no contract", _anchor.AddDays(-10), null, null, null },
        { "closed, no contract", _anchor.AddDays(-10), _anchor.AddDays(-2), null, null },
        { "contract starts later than deployment", _anchor.AddDays(-10), null, _anchor.AddDays(-5), null },
        { "contract starts before deployment", _anchor.AddDays(-10), null, _anchor.AddDays(-30), null },
        { "contract off-hire is a whole day", _anchor.AddDays(-10), null, _anchor.AddDays(-30), _anchor.Date },
        { "contract off-hire has a time", _anchor.AddDays(-10), null, _anchor.AddDays(-30), _anchor.AddHours(-1) },
        { "deployment ends before contract", _anchor.AddDays(-10), _anchor.AddDays(-3), _anchor.AddDays(-30), _anchor.AddDays(10) },
        { "contract ends before deployment", _anchor.AddDays(-10), _anchor.AddDays(10), _anchor.AddDays(-30), _anchor.AddDays(-3) },
        { "starts exactly at the timestamp", _anchor, null, null, null },
        { "ends exactly at the timestamp", _anchor.AddDays(-10), _anchor, null, null }
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
        DateTime?[] caps = [null, _anchor.AddDays(1), _anchor.AddHours(-1)];
        DateTime[] timestamps =
        [
            _anchor.AddDays(-40), _anchor.AddDays(-20), _anchor.AddDays(-4), _anchor.AddHours(-1),
            _anchor, _anchor.AddHours(1), _anchor.AddDays(4), _anchor.AddDays(40)
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
