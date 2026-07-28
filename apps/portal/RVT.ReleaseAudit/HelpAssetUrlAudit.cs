// File summary: Classifies persisted Help asset URL rows and serializes deterministic, URL-free audit receipts.
// Major updates:
// - 2026-07-28 Added the pure release-audit classifier and receipt model.

using System.Text.Json;
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

internal static class HelpAssetUrlAudit
{
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
        var scannedRows = rows.ToArray();
        var violations = new List<HelpAssetUrlViolation>();

        foreach (var row in scannedRows)
        {
            var validation = HelpAssetUrlPolicy.ValidatePersistedValue(row.Url);
            if (validation.ViolationCode is not { } violationCode)
            {
                continue;
            }

            violations.Add(new HelpAssetUrlViolation(
                row.AssetId,
                row.HelpArticleId,
                violationCode));
        }

        var orderedViolations = violations
            .OrderBy(violation => violation.HelpArticleId)
            .ThenBy(violation => violation.AssetId)
            .ThenBy(violation => violation.ViolationCode, StringComparer.Ordinal)
            .ToArray();

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

    // Task 4 replaces this build-only entry point with the fail-closed release-audit CLI.
    private static int Main() => 2;
}
