// File summary: Defines shared monitor ownership window helpers for deployment and contract scoped data access.
// Major updates:
// - 2026-06-26 pending Added effective deployment/contract ownership windows for moved-monitor data isolation.

using System.Linq.Expressions;
using RVT.Entities;

namespace RvtPortal.Spa.Application.Monitors;

public readonly record struct MonitorOwnershipWindow(DateTime Start, DateTime? End)
{
    // Function summary: Evaluates whether a timestamp belongs to this monitor ownership window.
    public bool Contains(DateTime timestamp)
    {
        return timestamp >= Start && (!End.HasValue || timestamp < End.Value);
    }

    // Function summary: Evaluates whether a requested date range overlaps this monitor ownership window.
    public bool Intersects(DateTime from, DateTime to)
    {
        return to > Start && (!End.HasValue || from < End.Value);
    }

    // Function summary: Clamps a requested range to this monitor ownership window.
    public (DateTime From, DateTime To) Clamp(DateTime from, DateTime to)
    {
        var clampedFrom = Start > from ? Start : from;
        var clampedTo = End.HasValue && End.Value < to ? End.Value : to;
        return (clampedFrom, clampedTo);
    }
}

public static class MonitorOwnershipWindowResolver
{
    /// <summary>
    /// The SQL-translatable form of <see cref="MonitorOwnershipWindow.Contains"/>: a deployment owns the
    /// monitor's data at <paramref name="timestamp"/>. Lets callers filter deployments in the database
    /// instead of loading every row and applying the window in memory.
    /// </summary>
    /// <remarks>
    /// The window is [max(deploymentStart, contractOnHire), min(deploymentEnd, contractOffHire, cap)).
    /// A timestamp is at-or-after the maximum of the starts exactly when it is at-or-after every start, and
    /// before the minimum of the ends exactly when it is before every non-null end - which is what lets the
    /// max/min collapse into the flat conjunction below. Kept equivalent to ForDeployment(...).Contains(...)
    /// by MonitorOwnershipWindowTests; the translation itself is guarded by MonitorOwnershipWindowSqlTests.
    /// </remarks>
    public static Expression<Func<Deployment, bool>> OwnsAt(DateTime timestamp, DateTime? openEndedCap = null)
    {
        return deployment =>
            timestamp >= deployment.StartDate
            && (deployment.Contract == null || timestamp >= deployment.Contract.OnHireDate)
            && (deployment.EndDate == null || timestamp < deployment.EndDate.Value)
            && (openEndedCap == null || timestamp < openEndedCap.Value)
            && (deployment.Contract == null
                || deployment.Contract.OffHireDate == null
                // A date-only off-hire covers the whole day, so the exclusive end is the next midnight.
                || timestamp < (deployment.Contract.OffHireDate.Value.TimeOfDay == TimeSpan.Zero
                    ? deployment.Contract.OffHireDate.Value.Date.AddDays(1)
                    : deployment.Contract.OffHireDate.Value));
    }

    // Function summary: Builds the effective deployment and contract ownership window for monitor-bound data.
    public static MonitorOwnershipWindow ForDeployment(Deployment deployment, DateTime? openEndedCap = null)
    {
        var start = Max(deployment.StartDate, deployment.Contract?.OnHireDate);
        var end = Min(
            deployment.EndDate,
            NormalizeContractEnd(deployment.Contract?.OffHireDate),
            openEndedCap);

        return new MonitorOwnershipWindow(start, end);
    }

    // Function summary: Finds the deployment whose effective ownership window contains the timestamp.
    public static Deployment? MatchDeploymentAt(
        Guid monitorId,
        DateTime timestamp,
        IEnumerable<Deployment> deployments)
    {
        return deployments
            .Where(deployment => deployment.MonitorId == monitorId)
            .Where(deployment => ForDeployment(deployment).Contains(timestamp))
            .OrderByDescending(deployment => ForDeployment(deployment).Start)
            .ThenByDescending(deployment => deployment.Id)
            .FirstOrDefault();
    }

    // Function summary: Computes the latest starting date among deployment and contract starts.
    private static DateTime Max(DateTime deploymentStart, DateTime? contractStart)
    {
        return contractStart.HasValue && contractStart.Value > deploymentStart
            ? contractStart.Value
            : deploymentStart;
    }

    // Function summary: Computes the earliest non-null end among deployment, contract, and optional cap dates.
    private static DateTime? Min(params DateTime?[] values)
    {
        return values
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .OrderBy(static value => value)
            .Cast<DateTime?>()
            .FirstOrDefault();
    }

    // Function summary: Treats date-only off-hire values as inclusive whole days while preserving explicit times.
    private static DateTime? NormalizeContractEnd(DateTime? offHireDate)
    {
        if (!offHireDate.HasValue)
        {
            return null;
        }

        return offHireDate.Value.TimeOfDay == TimeSpan.Zero
            ? offHireDate.Value.Date.AddDays(1)
            : offHireDate.Value;
    }
}
