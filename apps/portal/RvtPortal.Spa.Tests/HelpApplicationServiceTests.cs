// File summary: Covers the transport-neutral Help application policy, validation, and use-case orchestration.
// Major updates:
// - 2026-07-28 Added RED coverage for the standalone Help application boundary.

using RvtPortal.Application.Common;
using RvtPortal.Application.Help;
using RvtPortal.Application.Identity;

namespace RvtPortal.Spa.Tests;

public sealed class HelpApplicationServiceTests
{
    [Fact]
    public void AuthorizationPolicy_PreservesPublishedAndAdminRoleContracts()
    {
        var admin = Actor(isAdmin: true);
        var companyUser = Actor(isCompanyUser: true);
        var installer = Actor(isInstaller: true);

        Assert.True(HelpAuthorizationPolicy.CanReadPublished(admin));
        Assert.True(HelpAuthorizationPolicy.CanReadPublished(companyUser));
        Assert.False(HelpAuthorizationPolicy.CanReadPublished(installer));
        Assert.True(HelpAuthorizationPolicy.CanManage(admin));
        Assert.False(HelpAuthorizationPolicy.CanManage(companyUser));
        Assert.False(HelpAuthorizationPolicy.CanManage(installer));
    }

    [Theory]
    [InlineData("https://docs.rvt.test/guide.pdf")]
    [InlineData("https://docs.rvt.test")]
    [InlineData("/help-assets/guides/guide.pdf")]
    public void MutationValidator_AcceptsSafeAssetUrls(string url)
    {
        var result = HelpMutationValidator.ValidateShape(
            ValidMutation() with
            {
                Assets =
                [
                    new HelpAssetMutation(null, "Guide", "Document", url, 0)
                ]
            });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("http://docs.rvt.test/guide.pdf")]
    [InlineData("//docs.rvt.test/guide.pdf")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("file:///tmp/guide.pdf")]
    [InlineData("/other/path.pdf")]
    [InlineData("/help-assets\\guide.pdf")]
    [InlineData("https://user:password@docs.rvt.test/guide.pdf")]
    [InlineData("https://docs.rvt.test/guide\u0001.pdf")]
    public void MutationValidator_RejectsUnsafeAssetUrls(string url)
    {
        var result = HelpMutationValidator.ValidateShape(
            ValidMutation() with
            {
                Assets =
                [
                    new HelpAssetMutation(null, "Guide", "Document", url, 0)
                ]
            });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Field == "Assets[0].Url");
    }

    [Fact]
    public void MutationValidator_CanonicalizesValuesAndPreservesAssetIds()
    {
        var assetId = Guid.NewGuid();
        var result = HelpMutationValidator.ValidateShape(
            ValidMutation() with
            {
                SectionTitle = " Guides ",
                SectionSlug = " guides ",
                Title = " Dust guide ",
                Slug = " dust-guide ",
                Summary = " Summary ",
                Body = " Body ",
                ContentType = "faq",
                Assets =
                [
                    new HelpAssetMutation(
                        assetId,
                        " Guide ",
                        "document",
                        "https://docs.rvt.test/guide.pdf",
                        2)
                ]
            });

        Assert.True(result.IsValid);
        var mutation = result.Value!.Source;
        Assert.Equal("Guides", mutation.SectionTitle);
        Assert.Equal("guides", mutation.SectionSlug);
        Assert.Equal("Dust guide", mutation.Title);
        Assert.Equal("dust-guide", mutation.Slug);
        Assert.Equal("Summary", mutation.Summary);
        Assert.Equal("Body", mutation.Body);
        Assert.Equal("FAQ", mutation.ContentType);
        var asset = Assert.Single(mutation.Assets);
        Assert.Equal(assetId, asset.Id);
        Assert.Equal("Document", asset.AssetType);
        Assert.Equal("Guide", asset.Title);
    }

    [Fact]
    public void MutationValidator_RejectsRequiredFormatAndRangeViolations()
    {
        var result = HelpMutationValidator.ValidateShape(
            new HelpArticleMutation(
                "",
                "Not a slug",
                "",
                "also_not_a_slug",
                null,
                "",
                "Unknown",
                false,
                -1,
                -2,
                [
                    new HelpAssetMutation(
                        null,
                        "",
                        "Unknown",
                        "",
                        -1)
                ]));

        Assert.False(result.IsValid);
        AssertErrors(
            result,
            nameof(HelpArticleMutation.SectionTitle),
            nameof(HelpArticleMutation.SectionSlug),
            nameof(HelpArticleMutation.Title),
            nameof(HelpArticleMutation.Slug),
            nameof(HelpArticleMutation.Body),
            nameof(HelpArticleMutation.ContentType),
            nameof(HelpArticleMutation.SectionSortOrder),
            nameof(HelpArticleMutation.SortOrder),
            "Assets[0].Title",
            "Assets[0].AssetType",
            "Assets[0].Url",
            "Assets[0].SortOrder");
    }

    [Fact]
    public void MutationValidator_RejectsAllPersistedLengthOverflows()
    {
        var result = HelpMutationValidator.ValidateShape(
            ValidMutation() with
            {
                SectionTitle = new string('a', 121),
                SectionSlug = new string('a', 121),
                Title = new string('a', 161),
                Slug = new string('a', 161),
                Summary = new string('a', 513),
                Body = new string('a', 100_001),
                Assets =
                [
                    new HelpAssetMutation(
                        null,
                        new string('a', 161),
                        "Document",
                        $"https://docs.rvt.test/{new string('a', 500)}",
                        0)
                ]
            });

        Assert.False(result.IsValid);
        AssertErrors(
            result,
            nameof(HelpArticleMutation.SectionTitle),
            nameof(HelpArticleMutation.SectionSlug),
            nameof(HelpArticleMutation.Title),
            nameof(HelpArticleMutation.Slug),
            nameof(HelpArticleMutation.Summary),
            nameof(HelpArticleMutation.Body),
            "Assets[0].Title",
            "Assets[0].Url");
    }

    [Fact]
    public void MutationValidator_RejectsDuplicateSlugAndForeignAssetIds()
    {
        var existingAssetId = Guid.NewGuid();
        var foreignAssetId = Guid.NewGuid();
        var shape = HelpMutationValidator.ValidateShape(
            ValidMutation() with
            {
                Assets =
                [
                    new HelpAssetMutation(
                        existingAssetId,
                        "Existing",
                        "Document",
                        "https://docs.rvt.test/existing.pdf",
                        0),
                    new HelpAssetMutation(
                        foreignAssetId,
                        "Foreign",
                        "Link",
                        "https://docs.rvt.test/foreign",
                        1)
                ]
            });

        var result = HelpMutationValidator.ValidateBusinessRules(
            shape,
            new HelpMutationValidationData(
                ArticleExists: true,
                SlugBelongsToAnotherArticle: true,
                ExistingAssetIds: new HashSet<Guid> { existingAssetId }),
            requireExistingArticle: true);

        Assert.False(result.IsValid);
        AssertErrors(
            result,
            nameof(HelpArticleMutation.Slug),
            "Assets[1].Id");
    }

    [Fact]
    public void MutationValidator_RejectsMissingUpdateTarget()
    {
        var shape = HelpMutationValidator.ValidateShape(ValidMutation());

        var result = HelpMutationValidator.ValidateBusinessRules(
            shape,
            new HelpMutationValidationData(
                ArticleExists: false,
                SlugBelongsToAnotherArticle: false,
                ExistingAssetIds: new HashSet<Guid>()),
            requireExistingArticle: true);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Field == "ArticleId");
    }

    private static PortalUserContext Actor(
        bool isAdmin = false,
        bool isInstaller = false,
        bool isCompanyUser = false) =>
        new(
            Guid.NewGuid(),
            "help.user@rvt.test",
            Guid.NewGuid(),
            isAdmin,
            isInstaller,
            isCompanyUser);

    private static HelpArticleMutation ValidMutation() =>
        new(
            "Guides",
            "guides",
            "Dust guide",
            "dust-guide",
            "Dust summary",
            "Dust body",
            "FAQ",
            false,
            0,
            0,
            []);

    private static void AssertErrors(
        HelpMutationValidationResult result,
        params string[] expectedFields)
    {
        var fields = result.Errors
            .Select(error => error.Field)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var field in expectedFields)
        {
            Assert.Contains(field, fields);
        }
    }
}
