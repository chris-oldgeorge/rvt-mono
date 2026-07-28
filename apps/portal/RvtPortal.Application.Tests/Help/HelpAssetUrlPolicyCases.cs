// File summary: Defines the shared Help asset URL validation corpus used by mutation and release-audit tests.
// Major updates:
// - 2026-07-28 Added the initial policy-parity corpus for Help asset URLs.

namespace RvtPortal.Testing.Help;

public sealed record HelpAssetUrlCase(
    string Name,
    string? Input,
    string? MutationCanonicalValue,
    string? MutationViolation,
    string? PersistedCanonicalValue,
    string? PersistedViolation);

public static class HelpAssetUrlPolicyCases
{
    public static IReadOnlyList<HelpAssetUrlCase> All { get; } =
    [
        new("null", null, null, "required", null, "required"),
        new("empty", "", null, "required", null, "required"),
        new("whitespace-only", " \t ", null, "required", null, "not_canonical"),
        new(
            "maximum-length",
            "https://docs.rvt.test/" + new string('a', 490),
            "https://docs.rvt.test/" + new string('a', 490),
            null,
            "https://docs.rvt.test/" + new string('a', 490),
            null),
        new(
            "over-maximum-length",
            "https://docs.rvt.test/" + new string('a', 491),
            null,
            "too_long",
            null,
            "too_long"),
        new(
            "leading-and-trailing-whitespace",
            "  https://docs.rvt.test/guide.pdf  ",
            "https://docs.rvt.test/guide.pdf",
            null,
            null,
            "not_canonical"),
        new(
            "embedded-space",
            "https://docs.rvt.test/guide file.pdf",
            null,
            "unsafe_character",
            null,
            "unsafe_character"),
        new(
            "tab",
            "https://docs.rvt.test/guide\tfile.pdf",
            null,
            "unsafe_character",
            null,
            "unsafe_character"),
        new(
            "control-character",
            "https://docs.rvt.test/guide\u0001file.pdf",
            null,
            "unsafe_character",
            null,
            "unsafe_character"),
        new(
            "backslash",
            "https://docs.rvt.test\\guide.pdf",
            null,
            "unsafe_character",
            null,
            "unsafe_character"),
        new(
            "protocol-relative",
            "//docs.rvt.test/guide.pdf",
            null,
            "unsupported_relative_path",
            null,
            "unsupported_relative_path"),
        new(
            "help-assets-relative-path",
            "/help-assets/guide.pdf",
            "/help-assets/guide.pdf",
            null,
            "/help-assets/guide.pdf",
            null),
        new(
            "help-assets-path-without-trailing-slash",
            "/help-assets",
            null,
            "unsupported_relative_path",
            null,
            "unsupported_relative_path"),
        new(
            "disallowed-relative-path",
            "/assets/guide.pdf",
            null,
            "unsupported_relative_path",
            null,
            "unsupported_relative_path"),
        new(
            "http",
            "http://docs.rvt.test/guide.pdf",
            null,
            "absolute_https_required",
            null,
            "absolute_https_required"),
        new(
            "other-scheme",
            "ftp://docs.rvt.test/guide.pdf",
            null,
            "absolute_https_required",
            null,
            "absolute_https_required"),
        new(
            "user-info",
            "https://user@docs.rvt.test/guide.pdf",
            null,
            "user_info_forbidden",
            null,
            "user_info_forbidden"),
        new(
            "uppercase-https",
            "HTTPS://docs.rvt.test/guide.pdf",
            "HTTPS://docs.rvt.test/guide.pdf",
            null,
            "HTTPS://docs.rvt.test/guide.pdf",
            null),
        new(
            "malformed-https-host",
            "https://:443/guide.pdf",
            null,
            "malformed_uri",
            null,
            "malformed_uri"),
        new(
            "https-without-host",
            "https://",
            null,
            "host_required",
            null,
            "host_required"),
        new(
            "ipv4",
            "https://192.0.2.1/guide.pdf",
            "https://192.0.2.1/guide.pdf",
            null,
            "https://192.0.2.1/guide.pdf",
            null),
        new(
            "bracketed-ipv6",
            "https://[2001:db8::1]/guide.pdf",
            "https://[2001:db8::1]/guide.pdf",
            null,
            "https://[2001:db8::1]/guide.pdf",
            null),
        new(
            "idn",
            "https://münich.example/guide.pdf",
            "https://münich.example/guide.pdf",
            null,
            "https://münich.example/guide.pdf",
            null),
        new(
            "query",
            "https://docs.rvt.test/guide.pdf?download=1",
            "https://docs.rvt.test/guide.pdf?download=1",
            null,
            "https://docs.rvt.test/guide.pdf?download=1",
            null),
        new(
            "fragment",
            "https://docs.rvt.test/guide.pdf#details",
            "https://docs.rvt.test/guide.pdf#details",
            null,
            "https://docs.rvt.test/guide.pdf#details",
            null)
    ];
}
