// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Svantek.Api.Db.EntityFramework;

// Summary: Creates Svantek monitor DbContexts for the durable alert stack.
// Major updates:
// - 2026-07-29 Legacy retirement step 4: added when Svantek alerting moved onto
//   the durable alert stack, which owns its own context lifetimes.

public sealed class SvantekMonitorContextFactory : IMonitorDbContextFactory<SvantekMonitorContext>
{
    private readonly string _connectionString;
    private readonly MonitorDbOptions _monitorOptions;

    public SvantekMonitorContextFactory(
        string connectionString,
        MonitorDbOptions monitorOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(monitorOptions);

        _connectionString = connectionString;
        _monitorOptions = monitorOptions;
    }

    public SvantekMonitorContext CreateDbContext()
    {
        DbContextOptions<SvantekMonitorContext> options = MonitorDbContextOptionsFactory.CreateOptions<SvantekMonitorContext>(
            _connectionString);
        return new SvantekMonitorContext(options, _monitorOptions);
    }
}
