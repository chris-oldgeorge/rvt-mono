// File summary: Verifies Help asset URL release-audit classification, CLI orchestration, and secret-safe deterministic receipts.
// Major updates:
// - 2026-07-28 Added release-audit classifier and receipt contract coverage.
// - 2026-07-28 Added fail-closed option, orchestration, and atomic receipt-writing coverage.
// - 2026-07-28 Added opt-in PostgreSQL transaction and row-reader integration coverage.

using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using RVT.ReleaseAudit;
using RvtPortal.Spa.Tests.Support;
using RvtPortal.Testing.Help;

namespace RvtPortal.Spa.Tests;

public sealed class HelpAssetUrlAuditTests
{
    private const string Usage =
        "Usage: RVT.ReleaseAudit help-asset-urls --environment <label> --revision <git-sha> --receipt <path>"
        + "\nSet RVT_RELEASE_AUDIT_CONNECTION in the process environment.\n";

    private static readonly DateTimeOffset executedAtUtc =
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

    public static TheoryData<string[]> InvalidArgumentCases =>
        new()
        {
            Array.Empty<string>(),
            new[] { "unknown", "--environment", "production", "--revision", "abcdef0", "--receipt", "receipt.json" },
            new[] { "help-asset-urls", "help-asset-urls", "--environment", "production", "--revision", "abcdef0", "--receipt", "receipt.json" },
            new[] { "help-asset-urls", "--revision", "abcdef0", "--receipt", "receipt.json" },
            new[] { "help-asset-urls", "--environment", "production", "--revision", "abcdef0", "--unknown", "receipt.json" },
            new[] { "help-asset-urls", "--environment", "production", "--environment", "staging", "--revision", "abcdef0", "--receipt", "receipt.json" },
            new[] { "help-asset-urls", "--environment", "--revision", "abcdef0", "--receipt", "receipt.json" },
            new[] { "help-asset-urls", "--environment", " ", "--revision", "abcdef0", "--receipt", "receipt.json" },
            new[] { "help-asset-urls", "--environment", "production/eu", "--revision", "abcdef0", "--receipt", "receipt.json" },
            new[] { "help-asset-urls", "--environment", new string('a', 65), "--revision", "abcdef0", "--receipt", "receipt.json" },
            new[] { "help-asset-urls", "--environment", "production", "--revision", "abcdef", "--receipt", "receipt.json" },
            new[] { "help-asset-urls", "--environment", "production", "--revision", "abcdefg", "--receipt", "receipt.json" },
            new[] { "help-asset-urls", "--environment", "production", "--revision", new string('a', 65), "--receipt", "receipt.json" },
            new[] { "help-asset-urls", "--environment", "production", "--revision", "abcdef0", "--receipt", " " },
            new[] { "help-asset-urls", "--environment", "production", "--revision", "abcdef0", "--receipt", "\0receipt.json" }
        };

    [Fact]
    public void Parse_AcceptsEachRequiredFlagOnceAndResolvesReceiptPath()
    {
        var options = ReleaseAuditOptions.Parse(
        [
            "help-asset-urls",
            "--receipt", "artifacts/help-audit.json",
            "--revision", "a1B2c3D",
            "--environment", "production.eu-1"
        ]);

        Assert.NotNull(options);
        Assert.Equal("production.eu-1", options.Environment);
        Assert.Equal("a1B2c3D", options.Revision);
        Assert.Equal(Path.GetFullPath("artifacts/help-audit.json"), options.ReceiptPath);
    }

    [Theory]
    [MemberData(nameof(InvalidArgumentCases))]
    public void Parse_RejectsMissingUnknownDuplicateOrMalformedInput(string[] args)
    {
        Assert.Null(ReleaseAuditOptions.Parse(args));
    }

    [Fact]
    public void Parse_RejectsReceiptPathThatResolvesToDirectory()
    {
        Assert.Null(ReleaseAuditOptions.Parse(
        [
            "help-asset-urls",
            "--environment", "production",
            "--revision", "abcdef0",
            "--receipt", Path.GetTempPath()
        ]));
    }

    [Fact]
    public async Task RunAsync_NonexistentDirectoryFormReceiptIsInvalidWithoutReadingEnvironmentOrCreatingPath()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"rvt-release-audit-directory-form-{Guid.NewGuid():N}");
        var receiptArgument = directoryPath + Path.DirectorySeparatorChar;
        var environmentRead = false;

        var result = await RunProgramAsync(
            args: ValidArguments(receiptArgument),
            getEnvironmentVariable: _ =>
            {
                environmentRead = true;
                return "Host=database.test;Database=rvt;Password=not-real";
            });

        Assert.Equal(ReleaseAuditProgram.InvalidInput, result.ExitCode);
        Assert.Null(ReleaseAuditOptions.Parse(ValidArguments(receiptArgument)));
        Assert.False(environmentRead);
        Assert.False(Directory.Exists(directoryPath));
        Assert.Empty(result.StandardOutput);
        Assert.Equal(Usage, NormalizeNewLines(result.StandardError));
    }

    [Fact]
    public void Parse_RejectsFlagTokenUsedAsMissingValue()
    {
        Assert.Null(ReleaseAuditOptions.Parse(
        [
            "help-asset-urls",
            "--receipt", "--environment",
            "--revision", "abcdef0",
            "--environment", "production"
        ]));
    }

    [Fact]
    public async Task RunAsync_InvalidInputReturnsUsageWithoutReadingEnvironmentOrEchoingArguments()
    {
        const string secretMarker = "secret-marker-invalid-input";
        var result = await RunProgramAsync(
            args:
            [
                "help-asset-urls",
                "--environment", secretMarker,
                "--revision", "not-a-git-sha",
                "--receipt", "receipt.json"
            ],
            getEnvironmentVariable: _ => throw new InvalidOperationException("environment must not be read"));

        Assert.Equal(ReleaseAuditProgram.InvalidInput, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Equal(Usage, NormalizeNewLines(result.StandardError));
        Assert.DoesNotContain(secretMarker, result.StandardOutput + result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_MissingConnectionReturnsUsageAndReadsEnvironmentOnce()
    {
        var lookupCount = 0;
        var result = await RunProgramAsync(
            getEnvironmentVariable: variableName =>
            {
                lookupCount++;
                Assert.Equal("RVT_RELEASE_AUDIT_CONNECTION", variableName);
                return null;
            });

        Assert.Equal(ReleaseAuditProgram.InvalidInput, result.ExitCode);
        Assert.Equal(1, lookupCount);
        Assert.Empty(result.StandardOutput);
        Assert.Equal(Usage, NormalizeNewLines(result.StandardError));
    }

    [Fact]
    public async Task RunAsync_CompleteAuditWithoutFindingsWritesReceiptAndReturnsPassed()
    {
        const string rawUrl = "https://private.rvt.test/guide.pdf?token=raw-input";
        string? receiptPath = null;
        string? receiptJson = null;
        var row = new HelpAssetUrlAuditRow(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            rawUrl);
        var result = await RunProgramAsync(
            readRows: (_, _) => Task.FromResult(
                new HelpAssetUrlAuditReadResult("rvt_portal", [row])),
            writeReceipt: (path, json, _) =>
            {
                receiptPath = path;
                receiptJson = json;
                return Task.CompletedTask;
            });

        Assert.Equal(ReleaseAuditProgram.Passed, result.ExitCode);
        Assert.Equal(Path.GetFullPath("receipt.json"), receiptPath);
        Assert.NotNull(receiptJson);
        Assert.DoesNotContain(rawUrl, receiptJson, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(receiptJson);
        var receipt = document.RootElement;
        Assert.Equal("production", receipt.GetProperty("environment").GetString());
        Assert.Equal("rvt_portal", receipt.GetProperty("database").GetString());
        Assert.Equal("2026-07-28T12:34:56+00:00", receipt.GetProperty("executedAtUtc").GetString());
        Assert.Equal("abcdef0", receipt.GetProperty("revision").GetString());
        Assert.Equal("test-version", receipt.GetProperty("auditVersion").GetString());
        Assert.Equal("pass", receipt.GetProperty("outcome").GetString());
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public async Task RunAsync_CompleteAuditWithFindingsWritesUrlFreeReceiptAndReturnsViolationsFound()
    {
        const string rawRejectedUrl = "http://private.rvt.test/guide.pdf?credential=raw-input";
        string? receiptJson = null;
        var row = new HelpAssetUrlAuditRow(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            rawRejectedUrl);
        var result = await RunProgramAsync(
            readRows: (_, _) => Task.FromResult(
                new HelpAssetUrlAuditReadResult("rvt_portal", [row])),
            writeReceipt: (_, json, _) =>
            {
                receiptJson = json;
                return Task.CompletedTask;
            });

        Assert.Equal(ReleaseAuditProgram.ViolationsFound, result.ExitCode);
        Assert.NotNull(receiptJson);
        Assert.Contains("\"violationCode\": \"absolute_https_required\"", receiptJson, StringComparison.Ordinal);
        Assert.DoesNotContain(rawRejectedUrl, receiptJson, StringComparison.Ordinal);
        Assert.DoesNotContain(rawRejectedUrl, result.StandardOutput + result.StandardError, StringComparison.Ordinal);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public async Task RunAsync_DatabaseExceptionReturnsAuditFailureWithoutLeakingExceptionText()
    {
        const string secretMarker = "Password=secret-marker-database";
        const string rawRejectedUrl = "http://private.rvt.test/rejected.pdf";
        var expectedConnection =
            $"Host=database.test;Database=rvt;{secretMarker}";
        var result = await RunProgramAsync(
            getEnvironmentVariable: _ => expectedConnection,
            readRows: (connectionString, _) =>
            {
                if (!string.Equals(
                    expectedConnection,
                    connectionString,
                    StringComparison.Ordinal))
                {
                    return Task.FromResult(
                        new HelpAssetUrlAuditReadResult(
                            "unexpected",
                            []));
                }

                throw new InvalidOperationException(
                    $"database exception contained {connectionString} and {rawRejectedUrl}");
            });

        Assert.Equal(ReleaseAuditProgram.AuditFailure, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Equal(
            "FAILED: Help asset URL audit did not complete.\n",
            NormalizeNewLines(result.StandardError));
        Assert.DoesNotContain(secretMarker, result.StandardOutput + result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain(rawRejectedUrl, result.StandardOutput + result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_CancellationReturnsAuditFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await RunProgramAsync(
            readRows: (_, cancellationToken) => Task.FromCanceled<HelpAssetUrlAuditReadResult>(
                cancellationToken),
            cancellationToken: cancellation.Token);

        Assert.Equal(ReleaseAuditProgram.AuditFailure, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Equal(
            "FAILED: Help asset URL audit did not complete.\n",
            NormalizeNewLines(result.StandardError));
    }

    [Fact]
    public async Task RunAsync_ReceiptWriterExceptionReturnsAuditFailureWithoutLeakingExceptionText()
    {
        const string secretMarker = "secret-marker-receipt";
        const string rawRejectedUrl = "http://private.rvt.test/rejected.pdf";
        var result = await RunProgramAsync(
            writeReceipt: (_, _, _) => throw new IOException(
                $"receipt exception contained {secretMarker} and {rawRejectedUrl}"));

        Assert.Equal(ReleaseAuditProgram.AuditFailure, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Equal(
            "FAILED: Help asset URL audit did not complete.\n",
            NormalizeNewLines(result.StandardError));
        Assert.DoesNotContain(secretMarker, result.StandardOutput + result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain(rawRejectedUrl, result.StandardOutput + result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ReceiptDirectoryCreationFailureReturnsAuditFailureAndDoesNotPublishReceipt()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"rvt-release-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var blockingFile = Path.Combine(testDirectory, "not-a-directory");
        await File.WriteAllTextAsync(blockingFile, "block");
        var receiptPath = Path.Combine(blockingFile, "receipt.json");

        try
        {
            var result = await RunProgramAsync(
                args: ValidArguments(receiptPath),
                writeReceipt: HelpAssetUrlAudit.WriteReceiptAsync);

            Assert.Equal(ReleaseAuditProgram.AuditFailure, result.ExitCode);
            Assert.False(File.Exists(receiptPath));
            Assert.Empty(result.StandardOutput);
            Assert.Equal(
                "FAILED: Help asset URL audit did not complete.\n",
                NormalizeNewLines(result.StandardError));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteReceiptAsync_WritesUtf8WithoutBomAndAtomicallyReplacesExistingReceipt()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"rvt-release-audit-{Guid.NewGuid():N}");
        var receiptPath = Path.Combine(testDirectory, "nested", "receipt.json");

        try
        {
            await HelpAssetUrlAudit.WriteReceiptAsync(
                receiptPath,
                /*lang=json,strict*/ "{\"outcome\":\"old\"}\n",
                CancellationToken.None);
            await HelpAssetUrlAudit.WriteReceiptAsync(
                receiptPath,
                /*lang=json,strict*/ "{\"outcome\":\"pass\"}\n",
                CancellationToken.None);

            var bytes = await File.ReadAllBytesAsync(receiptPath);
            Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.Equal(/*lang=json,strict*/ "{\"outcome\":\"pass\"}\n", System.Text.Encoding.UTF8.GetString(bytes));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(receiptPath)!, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
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

#pragma warning disable JSON002 // Raw JSON verifies the stable serialized property order.
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
#pragma warning restore JSON002
        Assert.DoesNotContain(validRawUrl, json, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidRawUrl, json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("blocked", document.RootElement.GetProperty("outcome").GetString());
    }

    [RequiresPostgresFact]
    public async Task RowReader_ReadsCompleteCorpusInsideReadOnlyRepeatableReadTransaction()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            RequiresPostgresFactAttribute.ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var expectedRows = HelpAssetUrlPolicyCases.All
            .Select((@case, index) => new HelpAssetUrlAuditRow(
                Guid.Parse($"10000000-0000-0000-0000-{index + 1:D12}"),
                Guid.Parse($"20000000-0000-0000-0000-{index + 1:D12}"),
                @case.Input))
            .ToArray();
        var expectedViolations = HelpAssetUrlPolicyCases.All
            .Select((@case, index) => (@case, index))
            .Where(item => item.@case.PersistedViolation is not null)
            .Select(item => new HelpAssetUrlViolation(
                expectedRows[item.index].AssetId,
                expectedRows[item.index].HelpArticleId,
                item.@case.PersistedViolation!))
            .OrderBy(violation => violation.HelpArticleId)
            .ThenBy(violation => violation.AssetId)
            .ThenBy(violation => violation.ViolationCode, StringComparer.Ordinal)
            .ToArray();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand(
            """
            CREATE TEMP TABLE help_asset (
                id uuid PRIMARY KEY,
                help_article_id uuid NOT NULL,
                url text NULL
            ) ON COMMIT PRESERVE ROWS;
            """,
            connection))
        {
            await create.ExecuteNonQueryAsync();
        }

        foreach (var row in expectedRows)
        {
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO pg_temp.help_asset (id, help_article_id, url)
                VALUES ($1, $2, $3);
                """,
                connection);
            insert.Parameters.AddWithValue(NpgsqlDbType.Uuid, row.AssetId);
            insert.Parameters.AddWithValue(NpgsqlDbType.Uuid, row.HelpArticleId);
            insert.Parameters.AddWithValue(
                NpgsqlDbType.Text,
                row.Url is null ? DBNull.Value : row.Url);
            await insert.ExecuteNonQueryAsync();
        }

        await using var transaction = await HelpAssetUrlAudit.BeginReadOnlyTransactionAsync(
            connection,
            CancellationToken.None);
        try
        {
            var rows = await HelpAssetUrlAudit.ReadRowsAsync(
                connection,
                transaction,
                HelpAssetRelation.Temporary,
                CancellationToken.None);

            await using var readOnlyCommand = new NpgsqlCommand(
                "SHOW transaction_read_only;",
                connection,
                transaction);
            await using var isolationCommand = new NpgsqlCommand(
                "SHOW transaction_isolation;",
                connection,
                transaction);
            var readOnly = (string?)await readOnlyCommand.ExecuteScalarAsync();
            var isolation = (string?)await isolationCommand.ExecuteScalarAsync();

            Assert.Equal("on", readOnly);
            Assert.Equal("repeatable read", isolation);
            Assert.Equal(
                expectedRows.Select(row => row.AssetId).Order(),
                rows.Select(row => row.AssetId).Order());
            Assert.Equal(expectedViolations, Classify(rows).Violations);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static HelpAssetUrlAuditReceipt Classify(IReadOnlyList<HelpAssetUrlAuditRow> rows) =>
        HelpAssetUrlAudit.Classify(
            rows,
            environment: "production",
            database: "rvt_portal",
            executedAtUtc: executedAtUtc,
            revision: "abc123",
            auditVersion: "1");

    private static string[] ValidArguments(string receiptPath = "receipt.json") =>
    [
        "help-asset-urls",
        "--environment", "production",
        "--revision", "abcdef0",
        "--receipt", receiptPath
    ];

    private static async Task<ProgramRunResult> RunProgramAsync(
        IReadOnlyList<string>? args = null,
        Func<string, string?>? getEnvironmentVariable = null,
        Func<string, CancellationToken, Task<HelpAssetUrlAuditReadResult>>? readRows = null,
        Func<string, string, CancellationToken, Task>? writeReceipt = null,
        CancellationToken cancellationToken = default)
    {
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();
        var exitCode = await ReleaseAuditProgram.RunAsync(
            args ?? ValidArguments(),
            getEnvironmentVariable ?? (_ => "Host=database.test;Database=rvt;Password=not-real"),
            () => executedAtUtc,
            () => "test-version",
            readRows ?? ((_, _) => Task.FromResult(
                new HelpAssetUrlAuditReadResult("rvt_portal", []))),
            writeReceipt ?? ((_, _, _) => Task.CompletedTask),
            standardOutput,
            standardError,
            cancellationToken);

        return new ProgramRunResult(
            exitCode,
            standardOutput.ToString(),
            standardError.ToString());
    }

    private static string NormalizeNewLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private sealed record ProgramRunResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
