// File summary: Parses the fail-closed Help asset URL release-audit command line.
// Major updates:
// - 2026-07-28 Added strict command, label, revision, and receipt-path validation.

using System.Security;

namespace RVT.ReleaseAudit;

internal sealed record ReleaseAuditOptions(
    string Environment,
    string Revision,
    string ReceiptPath)
{
    private const string CommandName = "help-asset-urls";
    private const string EnvironmentFlag = "--environment";
    private const string RevisionFlag = "--revision";
    private const string ReceiptFlag = "--receipt";

    internal static ReleaseAuditOptions? Parse(IReadOnlyList<string> args)
    {
        if (args.Count != 7
            || !string.Equals(args[0], CommandName, StringComparison.Ordinal))
        {
            return null;
        }

        string? environment = null;
        string? revision = null;
        string? receipt = null;

        for (var index = 1; index < args.Count; index += 2)
        {
            var flag = args[index];
            var value = args[index + 1];
            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }

            switch (flag)
            {
                case EnvironmentFlag when environment is null:
                    environment = value;
                    break;
                case RevisionFlag when revision is null:
                    revision = value;
                    break;
                case ReceiptFlag when receipt is null:
                    receipt = value;
                    break;
                default:
                    return null;
            }
        }

        if (environment is null
            || revision is null
            || !IsEnvironmentLabel(environment)
            || !IsRevision(revision)
            || string.IsNullOrWhiteSpace(receipt))
        {
            return null;
        }

        try
        {
            var receiptPath = Path.GetFullPath(receipt);
            return Directory.Exists(receiptPath)
                ? null
                : new ReleaseAuditOptions(environment, revision, receiptPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or NotSupportedException
            or PathTooLongException
            or SecurityException)
        {
            return null;
        }
    }

    private static bool IsEnvironmentLabel(string value) =>
        value is { Length: >= 1 and <= 64 }
        && value.All(character =>
            character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '.'
            or '_'
            or '-');

    private static bool IsRevision(string value) =>
        value is { Length: >= 7 and <= 64 }
        && value.All(character =>
            character is >= 'a' and <= 'f'
            or >= 'A' and <= 'F'
            or >= '0' and <= '9');
}
