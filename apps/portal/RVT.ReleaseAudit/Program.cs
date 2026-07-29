// File summary: Composes the standalone fail-closed Help asset URL release-audit CLI.
// Major updates:
// - 2026-07-28 Added the thin process entry point and secret-safe orchestration boundary.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace RVT.ReleaseAudit;

internal static class ReleaseAuditEntryPoint
{
    private static Task<int> Main(string[] args) =>
        ReleaseAuditProgram.RunAsync(
            args,
            Environment.GetEnvironmentVariable,
            static () => DateTimeOffset.UtcNow,
            static () =>
                typeof(HelpAssetUrlAudit).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion
                ?? typeof(HelpAssetUrlAudit).Assembly.GetName().Version?.ToString()
                ?? "unknown",
            HelpAssetUrlAudit.ReadProductionRowsAsync,
            HelpAssetUrlAudit.WriteReceiptAsync,
            Console.Out,
            Console.Error,
            CancellationToken.None);
}

internal static class ReleaseAuditProgram
{
    internal const int Passed = 0;
    internal const int InvalidInput = 2;
    internal const int AuditFailure = 3;
    internal const int ViolationsFound = 10;

    private const string ConnectionEnvironmentVariable =
        "RVT_RELEASE_AUDIT_CONNECTION";

    private const string Usage =
        "Usage: RVT.ReleaseAudit help-asset-urls --environment <label> "
        + "--revision <git-sha> --receipt <path>\n"
        + "Set RVT_RELEASE_AUDIT_CONNECTION in the process environment.";

    private const string Failure =
        "FAILED: Help asset URL audit did not complete.";

    [SuppressMessage(
        "Maintainability",
        "S107:Methods should not have too many parameters",
        Justification = "The explicit process seams keep the release-audit CLI deterministic and independently testable without global state.")]
    internal static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        Func<string, string?> getEnvironmentVariable,
        Func<DateTimeOffset> getUtcNow,
        Func<string> getAuditVersion,
        Func<string, CancellationToken, Task<HelpAssetUrlAuditReadResult>> readRows,
        Func<string, string, CancellationToken, Task> writeReceipt,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        _ = standardOutput;

        ReleaseAuditOptions? options = ReleaseAuditOptions.Parse(args);
        if (options is null)
        {
            await standardError.WriteLineAsync(Usage);
            return InvalidInput;
        }

        string? connectionString;
        try
        {
            connectionString = getEnvironmentVariable(
                ConnectionEnvironmentVariable);
        }
        catch
        {
            await standardError.WriteLineAsync(Failure);
            return AuditFailure;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await standardError.WriteLineAsync(Usage);
            return InvalidInput;
        }

        HelpAssetUrlAuditReadResult readResult;
        try
        {
            readResult = await readRows(connectionString, cancellationToken);
        }
        catch
        {
            await standardError.WriteLineAsync(Failure);
            return AuditFailure;
        }

        HelpAssetUrlAuditReceipt receipt;
        try
        {
            receipt = HelpAssetUrlAudit.Classify(
                readResult.Rows,
                options.Environment,
                readResult.Database,
                getUtcNow(),
                options.Revision,
                getAuditVersion());
            string receiptJson = HelpAssetUrlAudit.SerializeReceipt(receipt);
            await writeReceipt(
                options.ReceiptPath,
                receiptJson,
                cancellationToken);
        }
        catch
        {
            await standardError.WriteLineAsync(Failure);
            return AuditFailure;
        }

        return receipt.ViolationCount == 0
            ? Passed
            : ViolationsFound;
    }
}
