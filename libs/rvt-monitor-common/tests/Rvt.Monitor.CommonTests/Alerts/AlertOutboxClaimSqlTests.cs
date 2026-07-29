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

        Assert.Contains("WITH candidate AS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FOR UPDATE SKIP LOCKED", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "UPDATE alert_delivery_outbox AS target",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "attempt_count = attempt_count + 1",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RETURNING target.*", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@now", sql, StringComparison.Ordinal);
        Assert.Contains("@leaseId", sql, StringComparison.Ordinal);
        Assert.Contains("@leaseUntil", sql, StringComparison.Ordinal);
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
