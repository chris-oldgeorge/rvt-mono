// File summary: Verifies the dashboard breach query builds UTC bounds so it survives the timestamptz column.
// Major updates:
// - 2026-07-15 pending Added after DateTime.Today bounds (Kind=Local) were found to throw against notification_time.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RVT.BusinessLogic;
using RVT.BusinessLogic.Application.Paging;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Application.Dashboard;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// notification_time is a timestamptz column, so the breach query's day bounds must be Kind=Utc. The default
/// path (no requested date) used DateTime.Today, which is Kind=Local and made Npgsql reject the query. The rest
/// of the suite runs on InMemory, which ignores DateTimeKind, so only a real PostgreSQL query can catch this.
/// </summary>
public sealed class DashboardBreachTimestamptzTests
{
    [RequiresPostgresFact]
    // Function summary: Verifies the default-date breach query executes against PostgreSQL instead of throwing on Kind.
    public async Task QueryAsync_WithDefaultDate_ExecutesAgainstRealPostgres()
    {
        var connectionString = Environment.GetEnvironmentVariable(RequiresPostgresFactAttribute.ConnectionVariable);
        var options = new DbContextOptionsBuilder<RVTDbContext>().UseNpgsql(connectionString).Options;
        await using var context = new RVTDbContext(options);

        var provider = new RvtDateTimeProvider(Options.Create(new RvtTimeZoneOptions { Local = "Europe/London" }));
        var service = new DashboardBreachApplicationService(context, provider);

        // request.Date == null is the path that used DateTime.Today (Kind=Local); before the fix this threw when
        // the bound reached notification_time. Executing at all is the assertion.
        var result = await service.QueryAsync(
            new DashboardBreachQuery(null, new PageRequest(null, 1, 10, DashboardBreachApplicationService.DefaultSort, "asc")),
            CancellationToken.None);

        Assert.NotNull(result);
    }
}
