using Rvt.Monitor.Common.Alerts.Persistence;
using DataIsolationLevel = System.Data.IsolationLevel;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class AlertOutboxClaimSqlTests
{
    [TestMethod]
    public void Statement_ClaimsAndUpdatesOnePostgreSqlCandidateAtomically()
    {
        string sql = AlertOutboxClaimSql.Statement;

        StringAssert.Contains(sql, "WITH candidate AS", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "FOR UPDATE SKIP LOCKED", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "LIMIT 1", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(
            sql,
            "UPDATE alert_delivery_outbox AS target",
            StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(
            sql,
            "attempt_count = attempt_count + 1",
            StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "RETURNING target.*", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "@now", StringComparison.Ordinal);
        StringAssert.Contains(sql, "@leaseId", StringComparison.Ordinal);
        StringAssert.Contains(sql, "@leaseUntil", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Statement_ContainsNoAlternateProviderSyntax()
    {
        string sql = AlertOutboxClaimSql.Statement;

        Assert.DoesNotContain("dbo.", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TOP (", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDLOCK", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("READPAST", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OUTPUT INSERTED", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("]", sql, StringComparison.Ordinal);
    }

    [TestMethod]
    public void IsolationLevel_IsReadCommitted()
    {
        Assert.AreEqual(DataIsolationLevel.ReadCommitted, AlertOutboxClaimSql.IsolationLevel);
    }
}
