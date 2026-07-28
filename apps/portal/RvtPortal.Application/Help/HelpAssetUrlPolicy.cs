// File summary: Canonicalizes and validates Help asset URLs for mutations and persisted release-audit data.
// Major updates:
// - 2026-07-28 Added the shared BCL-only Help asset URL policy.

namespace RvtPortal.Application.Help;

public sealed record HelpAssetUrlValidationResult(
    string? CanonicalValue,
    string? ViolationCode)
{
    public bool IsValid => ViolationCode is null;
}

public static class HelpAssetUrlPolicy
{
    public const int MaximumLength = 512;

    public static HelpAssetUrlValidationResult ValidateMutationValue(
        string? value) =>
        Validate(value?.Trim(), requireCanonicalValue: false);

    public static HelpAssetUrlValidationResult ValidatePersistedValue(
        string? value) =>
        Validate(value, requireCanonicalValue: true);

    private static HelpAssetUrlValidationResult Validate(
        string? value,
        bool requireCanonicalValue)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Invalid("required");
        }

        if (requireCanonicalValue && !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return Invalid("not_canonical");
        }

        if (value.Length > MaximumLength)
        {
            return Invalid("too_long");
        }

        if (value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
            value.Contains('\\'))
        {
            return Invalid("unsafe_character");
        }

        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return Invalid("unsupported_relative_path");
        }

        if (value.StartsWith('/'))
        {
            return value.StartsWith("/help-assets/", StringComparison.Ordinal) &&
                Uri.TryCreate(value, UriKind.Relative, out _)
                ? Valid(value)
                : Invalid("unsupported_relative_path");
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return Invalid("absolute_https_required");
            }

            if (string.IsNullOrWhiteSpace(uri.Host))
            {
                return Invalid("host_required");
            }

            return string.IsNullOrEmpty(uri.UserInfo)
                ? Valid(value)
                : Invalid("user_info_forbidden");
        }

        if (value.Equals("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid("host_required");
        }

        return value.StartsWith("https:", StringComparison.OrdinalIgnoreCase)
            ? Invalid("malformed_uri")
            : Invalid("unsupported_relative_path");
    }

    private static HelpAssetUrlValidationResult Valid(string value) =>
        new(value, null);

    private static HelpAssetUrlValidationResult Invalid(string violationCode) =>
        new(null, violationCode);
}
