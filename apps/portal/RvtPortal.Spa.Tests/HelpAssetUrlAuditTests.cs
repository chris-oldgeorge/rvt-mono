// File summary: Verifies Help asset URL release-audit classification and secret-safe deterministic receipts.
// Major updates:
// - 2026-07-28 Added release-audit classifier and receipt contract coverage.

using System.Text.Json;
using RVT.ReleaseAudit;
using RvtPortal.Testing.Help;

namespace RvtPortal.Spa.Tests;

public sealed class HelpAssetUrlAuditTests
{
    private static readonly DateTimeOffset ExecutedAtUtc =
        new(2026, 7, 28, 12, 34, 56, TimeSpan.Zero);

    public static TheoryData<string, string?, string?> PersistedCases
    {
        get
        {
            var cases = new TheoryData<string, string?, string?>();
            foreach (var @case in HelpAssetUrlPolicyCases.All)
            {
                cases.Add(@case.Name, @case.Input, @case.PersistedViolation);
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(PersistedCases))]
    public void Classify_UsesPersistedPolicyForEverySharedCorpusCase(
        string name,
        string? input,
        string? expectedViolationCode)
    {
        var row = new HelpAssetUrlAuditRow(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            input);

        var receipt = Classify([row]);

        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.Equal(expectedViolationCode, receipt.Violations.SingleOrDefault()?.ViolationCode);
    }

    [Fact]
    public void Classify_CountsEveryRowAndOmitsValidRowsFromViolations()
    {
        var invalidRow = new HelpAssetUrlAuditRow(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            "http://docs.rvt.test/guide.pdf");
        var validRow = new HelpAssetUrlAuditRow(
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            "/help-assets/guide.pdf");

        var receipt = Classify([invalidRow, validRow]);

        Assert.Equal(2, receipt.RowsScanned);
        var violation = Assert.Single(receipt.Violations);
        Assert.Equal(invalidRow.AssetId, violation.AssetId);
        Assert.Equal(invalidRow.HelpArticleId, violation.HelpArticleId);
        Assert.Equal("absolute_https_required", violation.ViolationCode);
    }

    [Fact]
    public void Classify_OrdersViolationsAndCreatesBlockedReceiptForFindings()
    {
        var firstArticle = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var secondArticle = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var firstAsset = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var secondAsset = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var rows = new[]
        {
            new HelpAssetUrlAuditRow(secondAsset, secondArticle, "http://docs.rvt.test/b.pdf"),
            new HelpAssetUrlAuditRow(secondAsset, firstArticle, "https:// user@docs.rvt.test/a.pdf"),
            new HelpAssetUrlAuditRow(firstAsset, firstArticle, "ftp://docs.rvt.test/a.pdf")
        };

        var receipt = Classify(rows);

        Assert.Equal("blocked", receipt.Outcome);
        Assert.Equal(receipt.Violations.Count, receipt.ViolationCount);
        Assert.Collection(
            receipt.Violations,
            violation => Assert.Equal((firstArticle, firstAsset, "absolute_https_required"),
                (violation.HelpArticleId, violation.AssetId, violation.ViolationCode)),
            violation => Assert.Equal((firstArticle, secondAsset, "unsafe_character"),
                (violation.HelpArticleId, violation.AssetId, violation.ViolationCode)),
            violation => Assert.Equal((secondArticle, secondAsset, "absolute_https_required"),
                (violation.HelpArticleId, violation.AssetId, violation.ViolationCode)));
    }

    [Fact]
    public void Classify_CreatesPassReceiptWhenNoFindingsExist()
    {
        var receipt = Classify(
        [
            new HelpAssetUrlAuditRow(
                Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Guid.Parse("20000000-0000-0000-0000-000000000001"),
                "https://docs.rvt.test/guide.pdf")
        ]);

        Assert.Equal("pass", receipt.Outcome);
        Assert.Equal(0, receipt.ViolationCount);
        Assert.Empty(receipt.Violations);
    }

    [Fact]
    public void SerializeReceipt_UsesStableOrderAndExcludesRawRowUrls()
    {
        const string validRawUrl = "https://private.rvt.test/valid-guide.pdf?token=raw-input";
        const string invalidRawUrl = "http://private.rvt.test/invalid-guide.pdf?token=raw-input";
        var receipt = Classify(
        [
            new HelpAssetUrlAuditRow(
                Guid.Parse("10000000-0000-0000-0000-000000000002"),
                Guid.Parse("20000000-0000-0000-0000-000000000002"), invalidRawUrl),
            new HelpAssetUrlAuditRow(
                Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Guid.Parse("20000000-0000-0000-0000-000000000001"), validRawUrl)
        ]);

        var json = HelpAssetUrlAudit.SerializeReceipt(receipt);

        Assert.Equal(
            """
            {
              "environment": "production",
              "database": "rvt_portal",
              "executedAtUtc": "2026-07-28T12:34:56+00:00",
              "revision": "abc123",
              "auditVersion": "1",
              "rowsScanned": 2,
              "violationCount": 1,
              "outcome": "blocked",
              "violations": [
                {
                  "assetId": "10000000-0000-0000-0000-000000000002",
                  "helpArticleId": "20000000-0000-0000-0000-000000000002",
                  "violationCode": "absolute_https_required"
                }
              ]
            }
            """ + Environment.NewLine,
            json);
        Assert.DoesNotContain(validRawUrl, json, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidRawUrl, json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("blocked", document.RootElement.GetProperty("outcome").GetString());
    }

    private static HelpAssetUrlAuditReceipt Classify(IReadOnlyList<HelpAssetUrlAuditRow> rows) =>
        HelpAssetUrlAudit.Classify(
            rows,
            environment: "production",
            database: "rvt_portal",
            executedAtUtc: ExecutedAtUtc,
            revision: "abc123",
            auditVersion: "1");
}
