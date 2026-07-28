using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Rvt.Monitor.CommonTests.Data.EntityFramework;

[TestClass]
public sealed class MonitorDbContextOptionsFactoryTests
{
    [TestMethod]
    public void CreateOptions_UsesOnlyNpgsqlProvider()
    {
        DbContextOptions<DbContext> options = MonitorDbContextOptionsFactory.CreateOptions<DbContext>(
            "Host=localhost;Port=5432;Database=rvt;Username=rvt;Password=rvt");

        string[] extensionNames = options.Extensions
            .Select(extension => extension.GetType().FullName ?? string.Empty)
            .ToArray();
        Assert.IsTrue(extensionNames.Any(name =>
            name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(extensionNames.Any(name =>
            name.Contains("Sql" + "Server", StringComparison.OrdinalIgnoreCase)));
    }
}
