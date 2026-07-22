// File summary: Covers Help/FAQ CMS API behavior for the React SPA migration.
// Major updates:
// - 2026-06-10 pending Covered Help CMS admin listing, editing, and publication controls.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Added Help CMS create and published browsing regression coverage.

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public sealed class HelpCmsOperationsTests
{
    private const string AdminEmail = "help.admin@rvt.test";
    private const string CompanyUserEmail = "help.company@rvt.test";
    private const string Password = "P8sSw0rd9$";

    [Fact]
    // Function summary: Verifies admins can create help content and normal users can browse published content.
    public async Task HelpCms_AllowsAdminPublishingAndUserBrowsing()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser);

        var adminClient = CreateClient(factory);
        await LoginAsync(adminClient, AdminEmail, Password);

        // The submitted article is the fixture: every read-back assertion compares against it, so the
        // published content is proven to round-trip rather than matching re-typed literals.
        var articleRequest = new HelpArticleMutationRequest
        {
            SectionTitle = "Data Readings",
            SectionSlug = "data-readings",
            Title = "Dust reading definitions",
            Slug = "dust-reading-definitions",
            Summary = "Common dust-reading terms used in RVT Cloud.",
            Body = "PM10 and PM2.5 readings represent particulate matter levels captured from site monitors.",
            ContentType = "FAQ",
            IsPublished = true,
            SectionSortOrder = 1,
            SortOrder = 1,
            Assets =
            [
                new() { Title = "Dust monitoring guide", AssetType = "Document", Url = "/help-assets/data-readings/dust-guide.pdf", SortOrder = 1 },
                new() { Title = "Readings overview", AssetType = "Video", Url = "https://video.rvt.test/readings", SortOrder = 2 }
            ]
        };
        var create = await adminClient.PostAsJsonAsync("/api/help/articles", articleRequest);
        var created = await create.Content.ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();

        var userClient = CreateClient(factory);
        await LoginAsync(userClient, CompanyUserEmail, Password);
        var overview = await userClient.GetFromJsonAsync<HelpOverviewResponse>("/api/help?searchText=dust");
        var article = await userClient.GetFromJsonAsync<EntityResponse<HelpArticleResponse>>($"/api/help/articles/{articleRequest.Slug}");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(articleRequest.SectionTitle, created?.Item?.SectionTitle);
        Assert.Equal(articleRequest.Assets.Count, created?.Item?.Assets.Count);
        Assert.Contains(overview!.Sections.Single().Articles, item => item.Slug == articleRequest.Slug);
        Assert.Equal(articleRequest.Title, article!.Item!.Title);
        Assert.Equal(articleRequest.Assets.Count, article.Item.Assets.Count);
    }

    [Fact]
    // Function summary: Verifies admins can manage draft FAQ content before publishing it to users.
    public async Task HelpCms_AllowsAdminDraftEditingAndPublication()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser);

        var adminClient = CreateClient(factory);
        await LoginAsync(adminClient, AdminEmail, Password);

        var create = await adminClient.PostAsJsonAsync("/api/help/articles", new HelpArticleMutationRequest
        {
            SectionTitle = "Platform",
            SectionSlug = "platform",
            Title = "Draft FAQ",
            Slug = "draft-faq",
            Summary = "Draft summary",
            Body = "Draft body",
            ContentType = "FAQ",
            IsPublished = false,
            SectionSortOrder = 2,
            SortOrder = 4
        });
        var created = await create.Content.ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();
        var articleId = created!.Item!.Id;

        var adminOverview = await adminClient.GetFromJsonAsync<HelpAdminOverviewResponse>("/api/help/admin?status=Draft");
        // The publish edit is the fixture: its title/slug/assets drive both the read-back and the URL.
        var publishRequest = new HelpArticleMutationRequest
        {
            SectionTitle = "Platform",
            SectionSlug = "platform",
            Title = "Published FAQ",
            Slug = "published-faq",
            Summary = "Published summary",
            Body = "Published body",
            ContentType = "FAQ",
            IsPublished = true,
            SectionSortOrder = 2,
            SortOrder = 1,
            Assets =
            [
                new() { Title = "FAQ source", AssetType = "Link", Url = "https://rvt.test/help-source", SortOrder = 1 }
            ]
        };
        var update = await adminClient.PutAsJsonAsync($"/api/help/admin/articles/{articleId}", publishRequest);
        var updated = await update.Content.ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();
        var unpublish = await adminClient.PostAsJsonAsync($"/api/help/admin/articles/{articleId}/publication", new HelpPublishRequest
        {
            IsPublished = false
        });
        var unpublished = await unpublish.Content.ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();

        var userClient = CreateClient(factory);
        await LoginAsync(userClient, CompanyUserEmail, Password);
        // Republished-then-unpublished article is hidden from users again.
        var publicDraft = await userClient.GetAsync($"/api/help/articles/{publishRequest.Slug}");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Contains(adminOverview!.Articles, item => item.Id == articleId && !item.IsPublished);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(publishRequest.Title, updated!.Item!.Title);
        Assert.Equal(publishRequest.Assets.Count, updated.Item.Assets.Count);
        Assert.Equal(HttpStatusCode.OK, unpublish.StatusCode);
        Assert.False(unpublished!.Item!.IsPublished);
        Assert.Equal(HttpStatusCode.NotFound, publicDraft.StatusCode);
    }

    // Function summary: Creates client data for the current workflow.
    private static HttpClient CreateClient(SpaTestApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    // Function summary: Handles the login workflow for this module.
    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        return client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password,
            RememberMe = true
        });
    }
}
