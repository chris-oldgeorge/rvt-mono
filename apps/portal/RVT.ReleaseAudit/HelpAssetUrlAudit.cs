// File summary: Reads and classifies persisted Help asset URLs and writes deterministic, URL-free audit receipts.
// Major updates:
// - 2026-07-28 Added the pure release-audit classifier and receipt model.
// - 2026-07-28 Added fixed-relation read-only PostgreSQL scanning and atomic receipt publication.

using System.Data;
using System.Text;
using System.Text.Json;
using Npgsql;
using RvtPortal.Application.Help;

namespace RVT.ReleaseAudit;

public sealed record HelpAssetUrlAuditRow(
    Guid AssetId,
    Guid HelpArticleId,
    string? Url);

public sealed record HelpAssetUrlViolation(
    Guid AssetId,
    Guid HelpArticleId,
    string ViolationCode);

public sealed record HelpAssetUrlAuditReceipt(
    string Environment,
    string Database,
    DateTimeOffset ExecutedAtUtc,
    string Revision,
    string AuditVersion,
    int RowsScanned,
    int ViolationCount,
    string Outcome,
    IReadOnlyList<HelpAssetUrlViolation> Violations);

internal sealed record HelpAssetUrlAuditReadResult(
    string Database,
    IReadOnlyList<HelpAssetUrlAuditRow> Rows);

internal enum HelpAssetRelation
{
    Production,
    Temporary
}

internal static class HelpAssetUrlAudit
{
    internal const string ProductionRelation = "public.help_asset";
    internal const string TestRelation = "pg_temp.help_asset";

    private const string ProductionQuery =
        "SELECT id, help_article_id, url\n"
        + "FROM " + ProductionRelation + "\n"
        + "ORDER BY help_article_id, id;";

    private const string TestQuery =
        "SELECT id, help_article_id, url\n"
        + "FROM " + TestRelation + "\n"
        + "ORDER BY help_article_id, id;";

    internal static readonly JsonSerializerOptions ReceiptJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static HelpAssetUrlAuditReceipt Classify(
        IEnumerable<HelpAssetUrlAuditRow> rows,
        string environment,
        string database,
        DateTimeOffset executedAtUtc,
        string revision,
        string auditVersion)
    {
        HelpAssetUrlAuditRow[] scannedRows = [.. rows];
        List<HelpAssetUrlViolation> violations = new();

        foreach (HelpAssetUrlAuditRow? row in scannedRows)
        {
            HelpAssetUrlValidationResult validation = HelpAssetUrlPolicy.ValidatePersistedValue(row.Url);
            if (validation.ViolationCode is not { } violationCode)
            {
                continue;
            }

            violations.Add(new HelpAssetUrlViolation(
                row.AssetId,
                row.HelpArticleId,
                violationCode));
        }

        HelpAssetUrlViolation[] orderedViolations = [.. violations
            .OrderBy(violation => violation.HelpArticleId)
            .ThenBy(violation => violation.AssetId)
            .ThenBy(violation => violation.ViolationCode, StringComparer.Ordinal)];

        return new HelpAssetUrlAuditReceipt(
            environment,
            database,
            executedAtUtc,
            revision,
            auditVersion,
            scannedRows.Length,
            orderedViolations.Length,
            orderedViolations.Length == 0 ? "pass" : "blocked",
            orderedViolations);
    }

    internal static string SerializeReceipt(HelpAssetUrlAuditReceipt receipt) =>
        JsonSerializer.Serialize(receipt, ReceiptJsonOptions) + Environment.NewLine;

    internal static async Task<HelpAssetUrlAuditReadResult> ReadProductionRowsAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        string database = connection.Database;

        await using NpgsqlTransaction transaction = await BeginReadOnlyTransactionAsync(
            connection,
            cancellationToken);
        IReadOnlyList<HelpAssetUrlAuditRow> rows = await ReadRowsAsync(
            connection,
            transaction,
            HelpAssetRelation.Production,
            cancellationToken);
        await transaction.RollbackAsync(cancellationToken);

        return new HelpAssetUrlAuditReadResult(database, rows);
    }

    internal static async Task<NpgsqlTransaction> BeginReadOnlyTransactionAsync(
        NpgsqlConnection openConnection,
        CancellationToken cancellationToken)
    {
        NpgsqlTransaction transaction = await openConnection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);

        try
        {
            await using NpgsqlCommand command = new(
                "SET TRANSACTION READ ONLY;",
                openConnection,
                transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    internal static async Task<IReadOnlyList<HelpAssetUrlAuditRow>> ReadRowsAsync(
        NpgsqlConnection openConnection,
        NpgsqlTransaction transaction,
        HelpAssetRelation relation,
        CancellationToken cancellationToken)
    {
        List<HelpAssetUrlAuditRow> rows = new();
        await using NpgsqlCommand command = new(
            GetReadRowsQuery(relation),
            openConnection,
            transaction);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new HelpAssetUrlAuditRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                await reader.IsDBNullAsync(2, cancellationToken)
                    ? null
                    : reader.GetString(2)));
        }

        return rows;
    }

    internal static async Task WriteReceiptAsync(
        string receiptPath,
        string receiptJson,
        CancellationToken cancellationToken)
    {
        string parentDirectory = Path.GetDirectoryName(receiptPath)
            ?? throw new IOException("Receipt path has no parent directory.");
        string temporaryPath = Path.Combine(
            parentDirectory,
            $".{Path.GetFileName(receiptPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            if (!Directory.Exists(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous))
            {
                await using (StreamWriter writer = new(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 4096,
                    leaveOpen: true))
                {
                    await writer.WriteAsync(receiptJson.AsMemory(), cancellationToken);
                    await writer.FlushAsync(cancellationToken);
                }

                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, receiptPath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
                // Preserve the original audit failure and never remove an unknown path.
            }

            throw;
        }
    }

    private static string GetReadRowsQuery(HelpAssetRelation relation) =>
        relation switch
        {
            HelpAssetRelation.Production => ProductionQuery,
            HelpAssetRelation.Temporary => TestQuery,
            _ => throw new ArgumentOutOfRangeException(nameof(relation))
        };
}
