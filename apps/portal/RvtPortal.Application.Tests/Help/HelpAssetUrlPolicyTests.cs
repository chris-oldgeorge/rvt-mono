// File summary: Verifies Help asset URL canonicalization and release-audit validation through one shared corpus.
// Major updates:
// - 2026-07-28 Added policy tests for mutation and persisted Help asset URL values.

using RvtPortal.Application.Help;
using RvtPortal.Testing.Help;

namespace RvtPortal.Application.Tests.Help;

public sealed class HelpAssetUrlPolicyTests
{
    public static TheoryData<string, string?, string?, string?, string?, string?> Cases
    {
        get
        {
            var cases = new TheoryData<string, string?, string?, string?, string?, string?>();
            foreach (var @case in HelpAssetUrlPolicyCases.All)
            {
                cases.Add(
                    @case.Name,
                    @case.Input,
                    @case.MutationCanonicalValue,
                    @case.MutationViolation,
                    @case.PersistedCanonicalValue,
                    @case.PersistedViolation);
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ValidateValues_MatchesSharedCorpus(
        string name,
        string? input,
        string? mutationCanonicalValue,
        string? mutationViolation,
        string? persistedCanonicalValue,
        string? persistedViolation)
    {
        Assert.False(string.IsNullOrWhiteSpace(name));

        var mutation = HelpAssetUrlPolicy.ValidateMutationValue(input);
        var persisted = HelpAssetUrlPolicy.ValidatePersistedValue(input);

        Assert.Equal(mutationCanonicalValue, mutation.CanonicalValue);
        Assert.Equal(mutationViolation, mutation.ViolationCode);
        Assert.Equal(persistedCanonicalValue, persisted.CanonicalValue);
        Assert.Equal(persistedViolation, persisted.ViolationCode);
    }

    [Fact]
    public void ValidationResults_ExposeCanonicalValueOnlyForValidInputs()
    {
        var results = HelpAssetUrlPolicyCases.All
            .SelectMany(@case => new[]
            {
                HelpAssetUrlPolicy.ValidateMutationValue(@case.Input),
                HelpAssetUrlPolicy.ValidatePersistedValue(@case.Input)
            })
            .ToArray();

        var validResults = results.Where(result => result.IsValid);
        var invalidResults = results.Where(result => !result.IsValid);

        Assert.All(validResults, result => Assert.NotNull(result.CanonicalValue));
        Assert.All(invalidResults, result => Assert.Null(result.CanonicalValue));
    }

    [Fact]
    public void MutationTrimsButPersistedValidationRejectsNonCanonicalWhitespace()
    {
        Assert.Equal(
            "https://docs.rvt.test/guide.pdf",
            HelpAssetUrlPolicy.ValidateMutationValue(
                "  https://docs.rvt.test/guide.pdf  ").CanonicalValue);
        Assert.Equal(
            "not_canonical",
            HelpAssetUrlPolicy.ValidatePersistedValue(
                "  https://docs.rvt.test/guide.pdf  ").ViolationCode);
    }
}
