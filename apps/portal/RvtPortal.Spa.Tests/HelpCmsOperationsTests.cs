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
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser);

        HttpClient adminClient = CreateClient(factory);
        await LoginAsync(adminClient, AdminEmail, Password);

        // The submitted article is the fixture: every read-back assertion compares against it, so the
        // published content is proven to round-trip rather than matching re-typed literals.
        HelpArticleMutationRequest articleRequest = new HelpArticleMutationRequest
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
        HttpResponseMessage create = await adminClient.PostAsJsonAsync("/api/help/admin/articles", articleRequest);
        EntityResponse<HelpArticleResponse>? created = await create.Content.ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();

        HttpClient userClient = CreateClient(factory);
        await LoginAsync(userClient, CompanyUserEmail, Password);
        HelpOverviewResponse? overview = await userClient.GetFromJsonAsync<HelpOverviewResponse>("/api/help?searchText=dust");
        EntityResponse<HelpArticleResponse>? article = await userClient.GetFromJsonAsync<EntityResponse<HelpArticleResponse>>($"/api/help/articles/{articleRequest.Slug}");

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
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser);

        HttpClient adminClient = CreateClient(factory);
        await LoginAsync(adminClient, AdminEmail, Password);

        HttpResponseMessage create = await adminClient.PostAsJsonAsync("/api/help/admin/articles", new HelpArticleMutationRequest
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
        EntityResponse<HelpArticleResponse>? created = await create.Content.ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();
        Guid articleId = created!.Item!.Id;

        HelpAdminOverviewResponse? adminOverview = await adminClient.GetFromJsonAsync<HelpAdminOverviewResponse>("/api/help/admin?status=Draft");
        // The publish edit is the fixture: its title/slug/assets drive both the read-back and the URL.
        HelpArticleMutationRequest publishRequest = new HelpArticleMutationRequest
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
        HttpResponseMessage update = await adminClient.PutAsJsonAsync($"/api/help/admin/articles/{articleId}", publishRequest);
        EntityResponse<HelpArticleResponse>? updated = await update.Content.ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();
        HttpResponseMessage unpublish = await adminClient.PostAsJsonAsync($"/api/help/admin/articles/{articleId}/publication", new HelpPublishRequest
        {
            IsPublished = false
        });
        EntityResponse<HelpArticleResponse>? unpublished = await unpublish.Content.ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();

        HttpClient userClient = CreateClient(factory);
        await LoginAsync(userClient, CompanyUserEmail, Password);
        // Republished-then-unpublished article is hidden from users again.
        HttpResponseMessage publicDraft = await userClient.GetAsync($"/api/help/articles/{publishRequest.Slug}");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Contains(adminOverview!.Articles, item => item.Id == articleId && !item.IsPublished);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(publishRequest.Title, updated!.Item!.Title);
        Assert.Equal(publishRequest.Assets.Count, updated.Item.Assets.Count);
        Assert.Equal(HttpStatusCode.OK, unpublish.StatusCode);
        Assert.False(unpublished!.Item!.IsPublished);
        Assert.Equal(HttpStatusCode.NotFound, publicDraft.StatusCode);
    }

    [Fact]
    public async Task HelpCms_UsesCanonicalCreateRouteAndPreservesAssetIdentity()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient adminClient = CreateClient(factory);
        await LoginAsync(adminClient, AdminEmail, Password);
        HelpArticleMutationRequest request = ArticleRequest(
            "stable-asset",
            [
                new HelpAssetMutationRequest
                {
                    Title = "Guide",
                    AssetType = "Document",
                    Url = "/help-assets/guide.pdf",
                    SortOrder = 1
                }
            ]);

        HttpResponseMessage legacy = await adminClient.PostAsJsonAsync(
            "/api/help/articles",
            request);
        HttpResponseMessage unsafeUrl = await adminClient.PostAsJsonAsync(
            "/api/help/admin/articles",
            ArticleRequest(
                "unsafe-url",
                [
                    new HelpAssetMutationRequest
                    {
                        Title = "Unsafe",
                        AssetType = "Link",
                        Url = "javascript:alert(1)",
                        SortOrder = 0
                    }
                ]));
        HttpResponseMessage create = await adminClient.PostAsJsonAsync(
            "/api/help/admin/articles",
            request);
        EntityResponse<HelpArticleResponse>? created = await create.Content
            .ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();
        Guid articleId = created!.Item!.Id;
        Guid assetId = Assert.Single(created.Item.Assets).Id;
        HelpArticleMutationRequest updateRequest = ArticleRequest(
            "stable-asset",
            [
                new HelpAssetMutationRequest
                {
                    Id = assetId,
                    Title = "Updated guide",
                    AssetType = "Document",
                    Url = "https://docs.rvt.test/guide",
                    SortOrder = 2
                }
            ]);
        HttpResponseMessage update = await adminClient.PutAsJsonAsync(
            $"/api/help/admin/articles/{articleId}",
            updateRequest);
        EntityResponse<HelpArticleResponse>? updated = await update.Content
            .ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();
        HttpResponseMessage foreign = await adminClient.PutAsJsonAsync(
            $"/api/help/admin/articles/{articleId}",
            ArticleRequest(
                "stable-asset",
                [
                    new HelpAssetMutationRequest
                    {
                        Id = Guid.NewGuid(),
                        Title = "Foreign",
                        AssetType = "Link",
                        Url = "https://docs.rvt.test/foreign",
                        SortOrder = 0
                    }
                ]));

        Assert.Equal(HttpStatusCode.NotFound, legacy.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unsafeUrl.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotEqual(Guid.Empty, assetId);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(assetId, Assert.Single(updated!.Item!.Assets).Id);
        Assert.Equal("Updated guide", updated.Item.Assets[0].Title);
        Assert.Null(updated.Item.Assets[0].InternalPath);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.RVTAdmin)]
    [InlineData(RoleNames.RVTMasterAdmin)]
    public async Task HelpCms_AllowsBothAdministratorRolesToUseEveryAdminEndpoint(
        string role)
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        string email = $"help.{role.ToLowerInvariant()}@rvt.test";
        await factory.SeedUserAsync(email, Password, role);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, email, Password);
        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/help/admin/articles",
            ArticleRequest($"admin-{role.ToLowerInvariant()}"));
        EntityResponse<HelpArticleResponse>? created = await create.Content
            .ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();
        Guid articleId = created!.Item!.Id;

        HttpResponseMessage query = await client.GetAsync("/api/help/admin");
        HttpResponseMessage get = await client.GetAsync($"/api/help/admin/articles/{articleId}");
        HttpResponseMessage update = await client.PutAsJsonAsync(
            $"/api/help/admin/articles/{articleId}",
            ArticleRequest($"admin-{role.ToLowerInvariant()}"));
        HttpResponseMessage publication = await client.PostAsJsonAsync(
            $"/api/help/admin/articles/{articleId}/publication",
            new HelpPublishRequest { IsPublished = false });
        HttpResponseMessage delete = await client.DeleteAsync(
            $"/api/help/admin/articles/{articleId}");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, publication.StatusCode);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.CompanyUser)]
    [InlineData(RoleNames.RVTInstaller)]
    public async Task HelpCms_DeniesNonAdministratorsFromEveryAdminEndpoint(
        string role)
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        string deniedEmail = $"help.denied.{role.ToLowerInvariant()}@rvt.test";
        await factory.SeedUserAsync(deniedEmail, Password, role);
        HttpClient adminClient = CreateClient(factory);
        await LoginAsync(adminClient, AdminEmail, Password);
        HttpResponseMessage seed = await adminClient.PostAsJsonAsync(
            "/api/help/admin/articles",
            ArticleRequest($"denied-{role.ToLowerInvariant()}"));
        EntityResponse<HelpArticleResponse>? seeded = await seed.Content
            .ReadFromJsonAsync<EntityResponse<HelpArticleResponse>>();
        Guid articleId = seeded!.Item!.Id;
        HttpClient deniedClient = CreateClient(factory);
        await LoginAsync(deniedClient, deniedEmail, Password);

        HttpResponseMessage[] responses =
        [
            await deniedClient.GetAsync("/api/help/admin"),
            await deniedClient.GetAsync($"/api/help/admin/articles/{articleId}"),
            await deniedClient.PostAsJsonAsync(
                "/api/help/admin/articles",
                ArticleRequest("denied-create")),
            await deniedClient.PutAsJsonAsync(
                $"/api/help/admin/articles/{articleId}",
                ArticleRequest($"denied-{role.ToLowerInvariant()}")),
            await deniedClient.PostAsJsonAsync(
                $"/api/help/admin/articles/{articleId}/publication",
                new HelpPublishRequest { IsPublished = false }),
            await deniedClient.DeleteAsync($"/api/help/admin/articles/{articleId}")
        ];

        Assert.All(
            responses,
            response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }

    [Fact]
    public async Task HelpCms_AdminNotFoundResultsRemain404()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);
        Guid missingId = Guid.NewGuid();

        HttpResponseMessage get = await client.GetAsync($"/api/help/admin/articles/{missingId}");
        HttpResponseMessage update = await client.PutAsJsonAsync(
            $"/api/help/admin/articles/{missingId}",
            ArticleRequest("missing"));
        HttpResponseMessage publication = await client.PostAsJsonAsync(
            $"/api/help/admin/articles/{missingId}/publication",
            new HelpPublishRequest { IsPublished = true });
        HttpResponseMessage delete = await client.DeleteAsync(
            $"/api/help/admin/articles/{missingId}");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, publication.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    private static HelpArticleMutationRequest ArticleRequest(
        string slug,
        List<HelpAssetMutationRequest>? assets = null) =>
        new()
        {
            SectionTitle = "Platform",
            SectionSlug = "platform",
            Title = $"Article {slug}",
            Slug = slug,
            Summary = "Summary",
            Body = "Body",
            ContentType = "FAQ",
            IsPublished = true,
            SectionSortOrder = 1,
            SortOrder = 1,
            Assets = assets ?? []
        };

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
