using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rvt.Monitor.Common.Alerts.Persistence;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class AlertPersistenceExceptionClassifierTests
{
    private const string OccurrenceConstraint = "uq_alert_occurrence_source_key";
    private const string SqlServerOccurrenceConstraint = "UQ_AlertOccurrences_SourceKey";

    [TestMethod]
    public void PostgreSqlUnique_IsOccurrenceConflictOnlyForExactConstraint()
    {
        var conflict = AlertPersistenceExceptionClassifier.ClassifyPostgreSql(
            "23505",
            OccurrenceConstraint,
            new InvalidOperationException("provider sentinel"));
        var otherUnique = AlertPersistenceExceptionClassifier.ClassifyPostgreSql(
            "23505",
            "uq_alert_delivery_outbox_delivery_key",
            new InvalidOperationException("provider sentinel"));

        Assert.IsInstanceOfType<AlertOccurrenceConflictException>(conflict);
        Assert.AreEqual("The alert occurrence already exists.", conflict.Message);
        Assert.IsNotInstanceOfType<AlertOccurrenceConflictException>(otherUnique);
        Assert.IsNotInstanceOfType<AlertTransientPersistenceException>(otherUnique);
        AssertSafe(otherUnique);
    }

    [TestMethod]
    [DataRow("40001")]
    [DataRow("40P01")]
    public void PostgreSqlSerializationAndDeadlock_AreTransient(string sqlState)
    {
        var classified = AlertPersistenceExceptionClassifier.ClassifyPostgreSql(
            sqlState,
            null,
            new InvalidOperationException("provider sentinel"));

        Assert.IsInstanceOfType<AlertTransientPersistenceException>(classified);
        AssertSafe(classified);
    }

    [TestMethod]
    [DataRow(1205)]
    [DataRow(3960)]
    public void SqlServerDeadlockAndSnapshotConflict_AreTransient(int errorNumber)
    {
        var classified = AlertPersistenceExceptionClassifier.ClassifySqlServer(
            errorNumber,
            "provider sentinel",
            new InvalidOperationException("provider sentinel"));

        Assert.IsInstanceOfType<AlertTransientPersistenceException>(classified);
        AssertSafe(classified);
    }

    [TestMethod]
    [DataRow(2601)]
    [DataRow(2627)]
    public void SqlServerUnique_IsOccurrenceConflictOnlyWhenConstraintIsIdentified(int errorNumber)
    {
        var conflict = AlertPersistenceExceptionClassifier.ClassifySqlServer(
            errorNumber,
            $"Duplicate key for constraint '{SqlServerOccurrenceConstraint}'. provider sentinel",
            new InvalidOperationException("provider sentinel"));
        var otherUnique = AlertPersistenceExceptionClassifier.ClassifySqlServer(
            errorNumber,
            "Duplicate key for constraint 'UQ_AlertDeliveryOutbox_DeliveryKey'. provider sentinel",
            new InvalidOperationException("provider sentinel"));

        Assert.IsInstanceOfType<AlertOccurrenceConflictException>(conflict);
        Assert.IsNotInstanceOfType<AlertOccurrenceConflictException>(otherUnique);
        Assert.IsNotInstanceOfType<AlertTransientPersistenceException>(otherUnique);
        AssertSafe(otherUnique);
    }

    [TestMethod]
    public void WrappedPostgreSqlFailure_IsUnwrappedWithoutLeakingProviderText()
    {
        var providerException = new PostgresException(
            "connection=provider sentinel destination=ops@example.test",
            "ERROR",
            "ERROR",
            "40001");
        var classified = AlertPersistenceExceptionClassifier.Classify(
            new DbUpdateException("EF provider sentinel", providerException));

        Assert.IsInstanceOfType<AlertTransientPersistenceException>(classified);
        AssertSafe(classified);
    }

    [TestMethod]
    public void UnknownPersistenceFailure_HasSafeTopLevelMessage()
    {
        var classified = AlertPersistenceExceptionClassifier.Classify(
            new DbUpdateException(
                "SELECT secret FROM alert WHERE destination='ops@example.test'",
                new InvalidOperationException("connection=provider sentinel")));

        Assert.IsInstanceOfType<InvalidOperationException>(classified);
        AssertSafe(classified);
    }

    [TestMethod]
    public void PostgreSqlProviderFailureWithoutSqlState_HasSafeTopLevelMessage()
    {
        var classified = AlertPersistenceExceptionClassifier.Classify(
            new NpgsqlException("connection=provider sentinel destination=ops@example.test"));

        Assert.IsInstanceOfType<InvalidOperationException>(classified);
        AssertSafe(classified);
    }

    private static void AssertSafe(Exception exception)
    {
        Assert.IsFalse(exception.Message.Contains("provider sentinel", StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(exception.Message.Contains("ops@example.test", StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase));
    }
}
