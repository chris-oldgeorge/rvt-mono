using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Omnidots.Api.Db.EntityFramework;

public sealed class OmnidotsMonitorContextFactory : IMonitorDbContextFactory<OmnidotsMonitorContext>
{
    private readonly string connectionString;
    private readonly MonitorDbOptions monitorOptions;

    public OmnidotsMonitorContextFactory(
        string connectionString,
        MonitorDbOptions monitorOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(monitorOptions);

        this.connectionString = connectionString;
        this.monitorOptions = monitorOptions;
    }

    public OmnidotsMonitorContext CreateDbContext()
    {
        DbContextOptions<OmnidotsMonitorContext> options = MonitorDbContextOptionsFactory.CreateOptions<OmnidotsMonitorContext>(
            connectionString);
        return new OmnidotsMonitorContext(options, monitorOptions);
    }
}
