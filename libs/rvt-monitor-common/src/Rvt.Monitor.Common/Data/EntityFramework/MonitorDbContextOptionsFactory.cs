using Microsoft.EntityFrameworkCore;

namespace Rvt.Monitor.Common.Data.EntityFramework;

public static class MonitorDbContextOptionsFactory
{
    public static DbContextOptions<TContext> CreateOptions<TContext>(
        string connectionString)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseNpgsql(connectionString);
        return builder.Options;
    }
}
