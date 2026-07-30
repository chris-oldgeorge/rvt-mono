// File summary: Verifies the report-rule list query against the report_rule_search view shape on real PostgreSQL.
// Major updates:
// - 2026-07-30 pending Added when the service's InMemory table fallback was deleted per the provider ruling.

using Npgsql;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;
using RvtPortal.Spa.UseCases.ReportRules;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// The rules list reads the report_rule_search SQL view, which the InMemory suite cannot populate (keyless
/// view entities cannot be seeded). Each test builds a throwaway schema with a table standing in for the view,
/// seeds rows directly, and exercises <see cref="ReportRuleApplicationService.QueryAsync"/> through Npgsql so
/// the search, sort, and paging translation is proven against the real provider.
/// </summary>
public sealed class ReportRuleQueryPostgresTests
{
    [RequiresPostgresFact]
    // Function summary: Verifies the list query filters by search text and sorts by site name in SQL.
    public async Task QueryAsync_FiltersBySearchTextAndSortsBySiteName()
    {
        await using ReportRuleSearchFixture fixture = await ReportRuleSearchFixture.CreateAsync();
        Guid weeklyRuleId = Guid.NewGuid();
        Guid monthlyRuleId = Guid.NewGuid();
        await fixture.SeedAsync(
            (weeklyRuleId, SiteName: "Beta Site", ReportName: "Weekly compliance", Frequency: ReportFrequencyType.Weekly),
            (monthlyRuleId, SiteName: "Alpha Site", ReportName: "Board pack", Frequency: ReportFrequencyType.Monthly));

        await using RVTSearchContext searchContext = fixture.CreateSearchContext();
        await using RVTDbContext domainContext = fixture.CreateDomainContext();
        ReportRuleApplicationService service = new(
            searchContext,
            domainContext,
            new NotSupportedUserDirectory(),
            new NotSupportedReportGenerationGateway());

        UseCaseResult<PagedResult<ReportRuleListModel>> searched = await service.QueryAsync(
            new ReportRuleQuery(null, new PageRequest("weekly", 1, 10, "siteName", "asc")),
            CancellationToken.None);
        UseCaseResult<PagedResult<ReportRuleListModel>> all = await service.QueryAsync(
            new ReportRuleQuery(null, new PageRequest(null, 1, 10, "siteName", "asc")),
            CancellationToken.None);

        ReportRuleListModel match = Assert.Single(searched.Value!.Results);
        Assert.Equal(weeklyRuleId, match.Id);
        Assert.Equal("Beta Site", match.SiteName);
        Assert.Equal(2, all.Value!.Total);
        Assert.Equal(
            ["Alpha Site", "Beta Site"],
            all.Value.Results.Select(rule => rule.SiteName));
    }

    [RequiresPostgresFact]
    // Function summary: Verifies the list query pages in SQL and reports the full total.
    public async Task QueryAsync_PagesResultsAndReportsTotal()
    {
        await using ReportRuleSearchFixture fixture = await ReportRuleSearchFixture.CreateAsync();
        await fixture.SeedAsync(
            (Guid.NewGuid(), SiteName: "Site A", ReportName: "Report A", Frequency: ReportFrequencyType.Monthly),
            (Guid.NewGuid(), SiteName: "Site B", ReportName: "Report B", Frequency: ReportFrequencyType.Monthly),
            (Guid.NewGuid(), SiteName: "Site C", ReportName: "Report C", Frequency: ReportFrequencyType.Monthly));

        await using RVTSearchContext searchContext = fixture.CreateSearchContext();
        await using RVTDbContext domainContext = fixture.CreateDomainContext();
        ReportRuleApplicationService service = new(
            searchContext,
            domainContext,
            new NotSupportedUserDirectory(),
            new NotSupportedReportGenerationGateway());

        UseCaseResult<PagedResult<ReportRuleListModel>> secondPage = await service.QueryAsync(
            new ReportRuleQuery(null, new PageRequest(null, 2, 2, "siteName", "asc")),
            CancellationToken.None);

        Assert.Equal(3, secondPage.Value!.Total);
        Assert.Equal("Site C", Assert.Single(secondPage.Value.Results).SiteName);
    }

    // QueryAsync never touches the user directory or the reporting gateway; these fail loudly if that changes.
    private sealed class NotSupportedUserDirectory : IPortalUserDirectory
    {
        public Task<IReadOnlyList<PortalUserProfile>> ListUsersAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PortalUserProfile?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NotSupportedReportGenerationGateway : IReportGenerationGateway
    {
        public Task<UseCaseResult<ReportGenerationResponseModel>> RequestGenerationAsync(
            Guid reportRuleId,
            ReportGenerationRequestModel request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// A throwaway PostgreSQL schema with a table shaped like the report_rule_search view. The query under test
    /// only reads the relation, so a directly seeded table pins the same SQL the view serves in production.
    /// </summary>
    private sealed class ReportRuleSearchFixture : IAsyncDisposable
    {
        private readonly string baseConnectionString;
        private readonly string schema;
        private readonly string scopedConnectionString;

        private ReportRuleSearchFixture(string baseConnectionString, string schema)
        {
            this.baseConnectionString = baseConnectionString;
            this.schema = schema;
            scopedConnectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                SearchPath = schema
            }.ConnectionString;
        }

        public static async Task<ReportRuleSearchFixture> CreateAsync()
        {
            string baseConnectionString = Environment.GetEnvironmentVariable(
                RequiresPostgresFactAttribute.ConnectionVariable)!;
            string schema = $"report_rule_query_{Guid.NewGuid():N}";
            ReportRuleSearchFixture fixture = new(baseConnectionString, schema);

            await using NpgsqlConnection connection = new(baseConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE SCHEMA "{schema}";

                CREATE TABLE "{schema}".report_rule_search
                (
                    id uuid NOT NULL,
                    site_name text NOT NULL,
                    site_id uuid NOT NULL,
                    frequency integer NOT NULL,
                    day_of_week integer,
                    day_of_month integer,
                    report_name character varying(128),
                    last_generated timestamp with time zone
                );
                """;
            await command.ExecuteNonQueryAsync();
            return fixture;
        }

        public async Task SeedAsync(params (Guid Id, string SiteName, string ReportName, ReportFrequencyType Frequency)[] rules)
        {
            await using NpgsqlConnection connection = new(scopedConnectionString);
            await connection.OpenAsync();
            foreach ((Guid id, string siteName, string reportName, ReportFrequencyType frequency) in rules)
            {
                await using NpgsqlCommand insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO report_rule_search
                        (id, site_name, site_id, frequency, day_of_week, day_of_month, report_name, last_generated)
                    VALUES
                        (@id, @site_name, @site_id, @frequency, NULL, NULL, @report_name, NULL);
                    """;
                insert.Parameters.AddWithValue("id", id);
                insert.Parameters.AddWithValue("site_name", siteName);
                insert.Parameters.AddWithValue("site_id", Guid.NewGuid());
                insert.Parameters.AddWithValue("frequency", (int)frequency);
                insert.Parameters.AddWithValue("report_name", reportName);
                await insert.ExecuteNonQueryAsync();
            }
        }

        public RVTSearchContext CreateSearchContext() =>
            new(TestDbContexts.Npgsql<RVTSearchContext>(scopedConnectionString));

        public RVTDbContext CreateDomainContext() =>
            new(TestDbContexts.Npgsql<RVTDbContext>(scopedConnectionString));

        public async ValueTask DisposeAsync()
        {
            await using NpgsqlConnection connection = new(baseConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"""DROP SCHEMA IF EXISTS "{schema}" CASCADE;""";
            await command.ExecuteNonQueryAsync();
        }
    }
}
