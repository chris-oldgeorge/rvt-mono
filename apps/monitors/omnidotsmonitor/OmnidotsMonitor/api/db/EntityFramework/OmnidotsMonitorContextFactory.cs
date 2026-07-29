using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Omnidots.Api.Db.EntityFramework;

public sealed class OmnidotsMonitorContextFactory : IMonitorDbContextFactory<OmnidotsMonitorContext>
{
    private readonly string _connectionString;
    private readonly MonitorDbOptions _monitorOptions;

    public OmnidotsMonitorContextFactory(
        string connectionString,
        MonitorDbOptions monitorOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(monitorOptions);

        _connectionString = connectionString;
        _monitorOptions = monitorOptions;
    }

    public OmnidotsMonitorContext CreateDbContext()
    {
        DbContextOptions<OmnidotsMonitorContext> options = MonitorDbContextOptionsFactory.CreateOptions<OmnidotsMonitorContext>(
            _connectionString);
        return new OmnidotsMonitorContext(options, _monitorOptions);
    }
}
