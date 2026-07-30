using System.Data.Common;
using Npgsql;

namespace Rvt.Monitor.IntegrationTesting.Tests;

[TestClass]
public sealed class MonitorDbParameterExtensionsTests
{
    [TestMethod]
    public void AddWithValue_CreatesNpgsqlParameter()
    {
        using DbCommand command = new NpgsqlCommand();

        DbParameter parameter = command.Parameters.AddWithValue("@value", null);

        Assert.IsInstanceOfType<NpgsqlParameter>(parameter);
        Assert.AreEqual(DBNull.Value, parameter.Value);
    }
}
