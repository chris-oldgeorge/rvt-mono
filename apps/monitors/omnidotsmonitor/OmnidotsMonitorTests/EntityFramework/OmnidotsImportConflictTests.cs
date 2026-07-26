using Omnidots.Api.Db;

namespace OmnidotsAdapterTests.EntityFramework;

[TestClass]
public sealed class OmnidotsImportConflictTests
{
    [TestMethod]
    [DataRow("40001")]
    [DataRow("40P01")]
    [DataRow("23505")]
    public void PostgreSqlSerializationDeadlockAndUniqueStates_AreRetryable(string sqlState)
    {
        Assert.IsTrue(DBClient.IsRetryablePostgreSqlState(sqlState));
    }

    [TestMethod]
    public void PostgreSqlOtherState_IsNotRetryable()
    {
        Assert.IsFalse(DBClient.IsRetryablePostgreSqlState("P0001"));
    }

}
