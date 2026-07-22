namespace Rvt.Monitor.Common.Data.EntityFramework;

public interface IMonitorDbContextFactory<out TContext>
    where TContext : MonitorDbContextBase
{
    TContext CreateDbContext();
}
