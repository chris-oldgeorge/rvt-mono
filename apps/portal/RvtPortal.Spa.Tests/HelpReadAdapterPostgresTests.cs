// File summary: Verifies the Help read adapter's ILike search and content-type filters against real PostgreSQL.
// Major updates:
// - 2026-07-30 pending Added when the adapter's InMemory search branches were deleted per the provider ruling.

using Npgsql;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Help;
using RvtPortal.Spa.Adapters.Help;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// The Help read adapter searches with <c>EF.Functions.ILike</c>, which only Npgsql translates - the InMemory
/// suite cannot execute these paths at all. Each test builds a throwaway schema holding the three Help tables,
/// seeds through the domain context, and exercises the adapter against the real provider.
/// </summary>
public sealed class HelpReadAdapterPostgresTests
{
    [RequiresPostgresFact]
    // Function summary: Verifies the published overview search matches case-insensitively across the article fields.
    public async Task PublishedQuery_SearchesCaseInsensitively()
    {
        await using HelpSchemaFixture fixture = await HelpSchemaFixture.CreateAsync();
        await using RVTDbContext seedContext = fixture.CreateDomainContext();
        await SeedAsync(seedContext);
        await using RVTDbContext context = fixture.CreateDomainContext();
        EfHelpReadAdapter adapter = new(context);

        HelpOverviewModel searched = await adapter.QueryPublishedAsync(
            "DUST",
            CancellationToken.None);

        HelpSectionModel searchedSection = Assert.Single(searched.Sections);
        Assert.Equal("Data", searchedSection.Title);
        Assert.Equal("Dust guide", Assert.Single(searchedSection.Articles).Title);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies LIKE wildcards in the search text are escaped and matched literally.
    public async Task PublishedQuery_TreatsLikeWildcardsLiterally()
    {
        await using HelpSchemaFixture fixture = await HelpSchemaFixture.CreateAsync();
        await using RVTDbContext seedContext = fixture.CreateDomainContext();
        await SeedAsync(seedContext);
        await using RVTDbContext context = fixture.CreateDomainContext();
        EfHelpReadAdapter adapter = new(context);

        // "u_t" would match "ust" if the underscore stayed a single-character wildcard; "50%" must only match
        // the literal percent seeded into the dust guide's body.
        HelpOverviewModel wildcardMiss = await adapter.QueryPublishedAsync(
            "u_t",
            CancellationToken.None);
        HelpOverviewModel literalPercent = await adapter.QueryPublishedAsync(
            "50%",
            CancellationToken.None);

        Assert.Empty(wildcardMiss.Sections);
        HelpSectionModel matchedSection = Assert.Single(literalPercent.Sections);
        Assert.Equal("Dust guide", Assert.Single(matchedSection.Articles).Title);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies the admin overview combines search, status, and content-type filtering.
    public async Task AdminQuery_AppliesStatusTypeAndSearchFilters()
    {
        await using HelpSchemaFixture fixture = await HelpSchemaFixture.CreateAsync();
        await using RVTDbContext seedContext = fixture.CreateDomainContext();
        SeededHelpIds seeded = await SeedAsync(seedContext);
        await using RVTDbContext context = fixture.CreateDomainContext();
        EfHelpReadAdapter adapter = new(context);

        HelpAdminOverviewModel overview = await adapter.QueryAdminAsync(
            new HelpAdminQuery("draft", "Draft", "faq"),
            CancellationToken.None);

        HelpArticleModel article = Assert.Single(overview.Articles);
        Assert.Equal(seeded.DraftArticleId, article.Id);
        Assert.Equal("Draft FAQ", article.Title);
        Assert.Equal("Draft", overview.Status);
        Assert.Equal("faq", overview.ContentType);
        Assert.Contains(
            overview.Sections,
            section => section.Id == seeded.DataSectionId &&
                section.Articles.Single().Id == seeded.DraftArticleId);
    }

    private static async Task<SeededHelpIds> SeedAsync(RVTDbContext context)
    {
        Guid alertsSectionId = Guid.NewGuid();
        Guid dataSectionId = Guid.NewGuid();
        Guid draftArticleId = Guid.NewGuid();
        DateTime now = new(2026, 7, 30, 8, 0, 0, DateTimeKind.Utc);

        context.HelpSections.AddRange(
            new HelpSection
            {
                Id = alertsSectionId,
                Title = "Alerts",
                Slug = "alerts",
                SortOrder = 1,
                IsPublished = true,
                Articles =
                [
                    new HelpArticle
                    {
                        Id = Guid.NewGuid(),
                        SectionId = alertsSectionId,
                        Title = "Alert response",
                        Slug = "alert-response",
                        Summary = "Respond to alerts",
                        Body = "Alert body",
                        ContentType = "Article",
                        IsPublished = true,
                        SortOrder = 1,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    }
                ]
            },
            new HelpSection
            {
                Id = dataSectionId,
                Title = "Data",
                Slug = "data",
                SortOrder = 2,
                IsPublished = true,
                Articles =
                [
                    new HelpArticle
                    {
                        Id = draftArticleId,
                        SectionId = dataSectionId,
                        Title = "Draft FAQ",
                        Slug = "draft-faq",
                        Summary = "Draft summary",
                        Body = "Draft body",
                        ContentType = "FAQ",
                        IsPublished = false,
                        SortOrder = 1,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    },
                    new HelpArticle
                    {
                        Id = Guid.NewGuid(),
                        SectionId = dataSectionId,
                        Title = "Dust guide",
                        Slug = "dust-guide",
                        Summary = "Dust readings",
                        Body = "Understand DUST values above the 50% threshold",
                        ContentType = "FAQ",
                        IsPublished = true,
                        SortOrder = 2,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    }
                ]
            });
        await context.SaveChangesAsync();
        return new SeededHelpIds(dataSectionId, draftArticleId);
    }

    private sealed record SeededHelpIds(
        Guid DataSectionId,
        Guid DraftArticleId);

    /// <summary>
    /// A throwaway PostgreSQL schema holding the three Help tables, mirroring the canonical column mapping so
    /// the domain context can seed and the adapter can query through the real provider.
    /// </summary>
    private sealed class HelpSchemaFixture : IAsyncDisposable
    {
        private readonly string _baseConnectionString;
        private readonly string _schema;
        private readonly string _scopedConnectionString;

        private HelpSchemaFixture(string baseConnectionString, string schema)
        {
            _baseConnectionString = baseConnectionString;
            _schema = schema;
            _scopedConnectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                SearchPath = schema
            }.ConnectionString;
        }

        public static async Task<HelpSchemaFixture> CreateAsync()
        {
            string baseConnectionString = Environment.GetEnvironmentVariable(
                RequiresPostgresFactAttribute.ConnectionVariable)!;
            string schema = $"help_read_{Guid.NewGuid():N}";
            HelpSchemaFixture fixture = new(baseConnectionString, schema);

            await using NpgsqlConnection connection = new(baseConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE SCHEMA "{schema}";

                CREATE TABLE "{schema}".help_section
                (
                    id uuid PRIMARY KEY,
                    title character varying(120) NOT NULL,
                    slug character varying(120) NOT NULL,
                    sort_order integer NOT NULL,
                    is_published boolean NOT NULL
                );

                CREATE TABLE "{schema}".help_article
                (
                    id uuid PRIMARY KEY,
                    help_section_id uuid NOT NULL REFERENCES "{schema}".help_section(id),
                    title character varying(160) NOT NULL,
                    slug character varying(160) NOT NULL,
                    summary character varying(512),
                    body text NOT NULL,
                    content_type character varying(40) NOT NULL,
                    is_published boolean NOT NULL,
                    sort_order integer NOT NULL,
                    created_at_utc timestamp with time zone NOT NULL,
                    updated_at_utc timestamp with time zone NOT NULL
                );

                CREATE TABLE "{schema}".help_asset
                (
                    id uuid PRIMARY KEY,
                    help_article_id uuid NOT NULL REFERENCES "{schema}".help_article(id),
                    title character varying(160) NOT NULL,
                    asset_type character varying(40) NOT NULL,
                    url character varying(512) NOT NULL,
                    internal_path character varying(512),
                    sort_order integer NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
            return fixture;
        }

        public RVTDbContext CreateDomainContext() =>
            new(TestDbContexts.Npgsql<RVTDbContext>(_scopedConnectionString));

        public async ValueTask DisposeAsync()
        {
            await using NpgsqlConnection connection = new(_baseConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"""DROP SCHEMA IF EXISTS "{_schema}" CASCADE;""";
            await command.ExecuteNonQueryAsync();
        }
    }
}
