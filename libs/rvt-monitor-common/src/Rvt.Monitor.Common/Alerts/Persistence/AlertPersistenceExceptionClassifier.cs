using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Rvt.Monitor.Common.Alerts.Persistence;

public static class AlertPersistenceExceptionClassifier
{
    private const string PostgreSqlOccurrenceConstraint = "uq_alert_occurrence_source_key";
    private const string SqlServerOccurrenceConstraint = "UQ_AlertOccurrences_SourceKey";
    private const string SafeTransientMessage = "Alert persistence was interrupted by a transient database conflict.";
    private const string SafePermanentMessage = "Alert persistence failed.";

    public static Exception Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException or AlertOccurrenceConflictException or AlertTransientPersistenceException)
        {
            return exception;
        }

        var containsNpgsqlFailure = false;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgreSql)
            {
                return ClassifyPostgreSql(
                    postgreSql.SqlState,
                    postgreSql.ConstraintName,
                    exception);
            }

            containsNpgsqlFailure |= current is NpgsqlException;

            if (current is SqlException sqlServer)
            {
                foreach (SqlError error in sqlServer.Errors)
                {
                    if (IsSqlServerOccurrenceConflict(error.Number, error.Message))
                    {
                        return new AlertOccurrenceConflictException(exception);
                    }
                }

                foreach (SqlError error in sqlServer.Errors)
                {
                    if (IsSqlServerTransient(error.Number))
                    {
                        return Transient(exception);
                    }
                }

                return Permanent(exception);
            }
        }

        return exception is DbUpdateException || containsNpgsqlFailure
            ? Permanent(exception)
            : exception;
    }

    internal static Exception ClassifyPostgreSql(
        string sqlState,
        string? constraintName,
        Exception innerException)
    {
        if (string.Equals(sqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal) &&
            string.Equals(
                constraintName,
                PostgreSqlOccurrenceConstraint,
                StringComparison.Ordinal))
        {
            return new AlertOccurrenceConflictException(innerException);
        }

        return sqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
            ? Transient(innerException)
            : Permanent(innerException);
    }

    internal static Exception ClassifySqlServer(
        int errorNumber,
        string? providerMessage,
        Exception innerException)
    {
        if (IsSqlServerOccurrenceConflict(errorNumber, providerMessage))
        {
            return new AlertOccurrenceConflictException(innerException);
        }

        return IsSqlServerTransient(errorNumber)
            ? Transient(innerException)
            : Permanent(innerException);
    }

    private static bool IsSqlServerOccurrenceConflict(int errorNumber, string? providerMessage) =>
        errorNumber is 2601 or 2627 &&
        providerMessage?.Contains(SqlServerOccurrenceConstraint, StringComparison.Ordinal) == true;

    private static bool IsSqlServerTransient(int errorNumber) => errorNumber is 1205 or 3960;

    private static AlertTransientPersistenceException Transient(Exception innerException) =>
        new(SafeTransientMessage, innerException);

    private static InvalidOperationException Permanent(Exception innerException) =>
        new(SafePermanentMessage, innerException);
}
