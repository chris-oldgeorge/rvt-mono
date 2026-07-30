using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rvt.Monitor.Common.Alerts.Persistence;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class AlertPersistenceExceptionClassifierTests
{
    [TestMethod]
    public void PostgreSqlUnique_IsOccurrenceConflictOnlyForExactConstraint()
    {
        Exception conflict = AlertPersistenceExceptionClassifier.Classify(
            PostgreSqlException(PostgresErrorCodes.UniqueViolation, "uq_alert_occurrence_source_key"));
        Exception otherUnique = AlertPersistenceExceptionClassifier.Classify(
            PostgreSqlException(
                PostgresErrorCodes.UniqueViolation,
                "uq_alert_delivery_outbox_delivery_key"));

        Assert.IsInstanceOfType<AlertOccurrenceConflictException>(conflict);
        Assert.AreEqual("The alert occurrence already exists.", conflict.Message);
        Assert.IsNotInstanceOfType<AlertOccurrenceConflictException>(otherUnique);
        Assert.IsNotInstanceOfType<AlertTransientPersistenceException>(otherUnique);
        AssertSafe(otherUnique);
    }

    [TestMethod]
    [DataRow(PostgresErrorCodes.SerializationFailure)]
    [DataRow(PostgresErrorCodes.DeadlockDetected)]
    public void PostgreSqlSerializationAndDeadlock_AreTransient(string sqlState)
    {
        Exception classified = AlertPersistenceExceptionClassifier.Classify(
            PostgreSqlException(sqlState));

        Assert.IsInstanceOfType<AlertTransientPersistenceException>(classified);
        AssertSafe(classified);
    }

    [TestMethod]
    public void WrappedPostgreSqlFailure_IsUnwrappedWithoutLeakingProviderText()
    {
        Exception classified = AlertPersistenceExceptionClassifier.Classify(
            new DbUpdateException(
                "EF provider sentinel",
                PostgreSqlException(PostgresErrorCodes.SerializationFailure)));

        Assert.IsInstanceOfType<AlertTransientPersistenceException>(classified);
        AssertSafe(classified);
    }

    [TestMethod]
    public void UnknownPersistenceFailure_HasSafeTopLevelMessage()
    {
        Exception classified = AlertPersistenceExceptionClassifier.Classify(
            new DbUpdateException(
                "SELECT secret FROM alert WHERE destination='ops@example.test'",
                new InvalidOperationException("connection=provider sentinel")));

        Assert.IsInstanceOfType<InvalidOperationException>(classified);
        AssertSafe(classified);
    }

    [TestMethod]
    public void PostgreSqlProviderFailureWithoutSqlState_HasSafeTopLevelMessage()
    {
        Exception classified = AlertPersistenceExceptionClassifier.Classify(
            new NpgsqlException("connection=provider sentinel destination=ops@example.test"));

        Assert.IsInstanceOfType<InvalidOperationException>(classified);
        AssertSafe(classified);
    }

    [TestMethod]
    public void UnknownMonitorException_PassesThroughAsPermanentOutcome()
    {
        AlertUnknownMonitorException unknownMonitor = new("23423");

        Exception classified = AlertPersistenceExceptionClassifier.Classify(unknownMonitor);

        Assert.AreSame(unknownMonitor, classified);
        Assert.IsNotInstanceOfType<AlertTransientPersistenceException>(classified);
    }

    private static PostgresException PostgreSqlException(
        string sqlState,
        string? constraintName = null) =>
        new(
            "connection=provider sentinel destination=ops@example.test",
            "ERROR",
            "ERROR",
            sqlState,
            detail: null,
            hint: null,
            position: 0,
            internalPosition: 0,
            internalQuery: null,
            where: null,
            schemaName: null,
            tableName: "alert_occurrence",
            columnName: null,
            dataTypeName: null,
            constraintName: constraintName,
            file: null,
            line: null,
            routine: null);

    private static void AssertSafe(Exception exception)
    {
        Assert.IsFalse(exception.Message.Contains("provider sentinel", StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(exception.Message.Contains("ops@example.test", StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase));
    }
}
