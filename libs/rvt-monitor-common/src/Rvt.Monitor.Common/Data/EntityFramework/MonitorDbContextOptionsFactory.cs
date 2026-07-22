using Microsoft.EntityFrameworkCore;

namespace Rvt.Monitor.Common.Data.EntityFramework;

public static class MonitorDbContextOptionsFactory
{
    public static DbContextOptions<TContext> CreateOptions<TContext>(
        string connectionString,
        MonitorDbOptions options)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        if (options.IsPostgreSql)
        {
            builder.UseNpgsql(connectionString);
        }
        else
        {
            builder.UseSqlServer(connectionString);
        }

        return builder.Options;
    }
}
