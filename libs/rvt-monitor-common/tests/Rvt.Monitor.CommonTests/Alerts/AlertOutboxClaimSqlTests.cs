using Microsoft.SqlServer.TransactSql.ScriptDom;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Data;
using DataIsolationLevel = System.Data.IsolationLevel;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class AlertOutboxClaimSqlTests
{
    [TestMethod]
    public void For_PostgreSql_ClaimsAndUpdatesOneCandidateAtomically()
    {
        var sql = AlertOutboxClaimSql.For(MonitorDatabaseProvider.PostgreSql);

        StringAssert.Contains(sql, "WITH candidate AS", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "FOR UPDATE SKIP LOCKED", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "LIMIT 1", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "UPDATE alert_delivery_outbox AS target", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "attempt_count = attempt_count + 1", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "RETURNING target.*", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "@now", StringComparison.Ordinal);
        StringAssert.Contains(sql, "@leaseId", StringComparison.Ordinal);
        StringAssert.Contains(sql, "@leaseUntil", StringComparison.Ordinal);
    }

    [TestMethod]
    public void For_SqlServer_ClaimsAndUpdatesOneLockedCandidateAtomically()
    {
        var sql = AlertOutboxClaimSql.For(MonitorDatabaseProvider.SqlServer);
        var parser = new TSql180Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        _ = parser.Parse(reader, out var errors);

        Assert.HasCount(
            0,
            errors,
            string.Join(
                Environment.NewLine,
                errors.Select(error => $"Line {error.Line}, column {error.Column}: {error.Message}")));
        StringAssert.Contains(sql, "WITH candidate AS", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "SELECT TOP (1)", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(
            sql,
            "WITH (UPDLOCK, READPAST, ROWLOCK)",
            StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(sql.Contains("READCOMMITTEDLOCK", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(sql, "UPDATE candidate", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "AttemptCount = AttemptCount + 1", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "OUTPUT INSERTED.*", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(sql, "@now", StringComparison.Ordinal);
        StringAssert.Contains(sql, "@leaseId", StringComparison.Ordinal);
        StringAssert.Contains(sql, "@leaseUntil", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(MonitorDatabaseProvider.PostgreSql, DataIsolationLevel.ReadCommitted)]
    [DataRow(MonitorDatabaseProvider.SqlServer, DataIsolationLevel.RepeatableRead)]
    public void IsolationLevelFor_UsesProviderSafeClaimIsolation(
        MonitorDatabaseProvider provider,
        DataIsolationLevel expected)
    {
        Assert.AreEqual(expected, AlertOutboxClaimSql.IsolationLevelFor(provider));
    }

    [TestMethod]
    public void For_UnsupportedProvider_Throws()
    {
        Assert.ThrowsExactly<NotSupportedException>(() =>
            AlertOutboxClaimSql.For((MonitorDatabaseProvider)int.MaxValue));
    }

    [TestMethod]
    [DataRow(DateTimeKind.Unspecified)]
    [DataRow(DateTimeKind.Local)]
    [DataRow(DateTimeKind.Utc)]
    public void NormalizeClaimTimestamp_SqlServerSetsUtcWithoutChangingTicks(DateTimeKind sourceKind)
    {
        var value = new DateTime(638881776000000000, sourceKind);

        var normalized = AlertOutboxClaimDateTime.Normalize(
            value,
            MonitorDatabaseProvider.SqlServer);

        Assert.AreEqual(value.Ticks, normalized.Ticks);
        Assert.AreEqual(DateTimeKind.Utc, normalized.Kind);
    }

    [TestMethod]
    public void NormalizeNullableClaimTimestamp_SqlServerHandlesValueAndNull()
    {
        var value = new DateTime(638881776000000000, DateTimeKind.Unspecified);

        var normalized = AlertOutboxClaimDateTime.Normalize(
            (DateTime?)value,
            MonitorDatabaseProvider.SqlServer);
        var normalizedNull = AlertOutboxClaimDateTime.Normalize(
            null,
            MonitorDatabaseProvider.SqlServer);

        Assert.IsNotNull(normalized);
        Assert.AreEqual(value.Ticks, normalized.Value.Ticks);
        Assert.AreEqual(DateTimeKind.Utc, normalized.Value.Kind);
        Assert.IsNull(normalizedNull);
    }

    [TestMethod]
    public void NormalizeClaimTimestamp_PostgreSqlPreservesProviderValue()
    {
        var value = new DateTime(638881776000000000, DateTimeKind.Utc);

        var normalized = AlertOutboxClaimDateTime.Normalize(
            value,
            MonitorDatabaseProvider.PostgreSql);

        Assert.AreEqual(value, normalized);
        Assert.AreEqual(value.Kind, normalized.Kind);
    }
}
