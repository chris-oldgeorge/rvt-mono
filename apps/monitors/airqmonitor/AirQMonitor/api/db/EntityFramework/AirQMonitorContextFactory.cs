// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace AirQ.Api.Db.EntityFramework;

// Summary: Creates AirQ monitor DbContexts for the durable alert stack.
// Major updates:
// - 2026-07-29 Legacy retirement step 4: added when AirQ alerting moved onto
//   the durable alert stack, which owns its own context lifetimes.

public sealed class AirQMonitorContextFactory : IMonitorDbContextFactory<AirQMonitorContext>
{
    private readonly string _connectionString;
    private readonly MonitorDbOptions _monitorOptions;

    public AirQMonitorContextFactory(
        string connectionString,
        MonitorDbOptions monitorOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(monitorOptions);

        _connectionString = connectionString;
        _monitorOptions = monitorOptions;
    }

    public AirQMonitorContext CreateDbContext()
    {
        DbContextOptions<AirQMonitorContext> options = MonitorDbContextOptionsFactory.CreateOptions<AirQMonitorContext>(
            _connectionString);
        return new AirQMonitorContext(options, _monitorOptions);
    }
}
