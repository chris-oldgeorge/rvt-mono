using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Rvt.Monitor.CommonTests.Data.EntityFramework;

[TestClass]
public sealed class MonitorDbContextOptionsFactoryTests
{
    [TestMethod]
    public void CreateOptions_UsesSqlServerProvider()
    {
        var options = MonitorDbContextOptionsFactory.CreateOptions<DbContext>(
            "Server=(local);Database=Rvt;Trusted_Connection=True;TrustServerCertificate=True",
            new MonitorDbOptions(MonitorDatabaseProvider.SqlServer, new Dictionary<string, string>()));

        Assert.IsTrue(options.Extensions.Any(extension =>
            extension.GetType().FullName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true));
    }

    [TestMethod]
    public void CreateOptions_UsesNpgsqlProvider()
    {
        var options = MonitorDbContextOptionsFactory.CreateOptions<DbContext>(
            "Host=localhost;Port=5432;Database=rvt;Username=rvt;Password=rvt",
            new MonitorDbOptions(MonitorDatabaseProvider.PostgreSql, new Dictionary<string, string>()));

        Assert.IsTrue(options.Extensions.Any(extension =>
            extension.GetType().FullName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true));
    }
}
