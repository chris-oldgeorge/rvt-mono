// File summary: Guards the standalone portal application project and its forbidden dependency set.

using System.Xml.Linq;
using RvtPortal.Spa.Adapters.Help;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public sealed class ApplicationBoundaryArchitectureTests
{
    private static string ApplicationRoot =>
        Path.Combine(RepositoryLayout.Root, "RvtPortal.Application");

    private static string ApplicationProject =>
        Path.Combine(ApplicationRoot, "RvtPortal.Application.csproj");

    [Fact]
    public void ApplicationProject_IsBclOnly()
    {
        Assert.True(File.Exists(ApplicationProject), $"{ApplicationProject} must exist.");

        var project = XDocument.Load(ApplicationProject);
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("ProjectReference"));
    }

    [Fact]
    public void ApplicationSources_DoNotImportForbiddenFrameworksOrAdapters()
    {
        Assert.True(Directory.Exists(ApplicationRoot), $"{ApplicationRoot} must exist.");

        var forbidden = new[]
        {
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "RVT.DataAccess",
            "RvtPortal.Spa",
            "Azure.",
            "SendGrid",
            "IConfiguration",
            "IHttpClientFactory",
            "MediatR",
            "IQueryable",
            "DbContext",
            "DbSet",
            "IFormFile",
            "ClaimsPrincipal"
        };
        var violations = Directory
            .EnumerateFiles(ApplicationRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { path, line, number = index + 1 }))
            .Where(row => forbidden.Any(value =>
                row.line.Contains(value, StringComparison.Ordinal)))
            .Select(row => $"{Path.GetRelativePath(ApplicationRoot, row.path)}:{row.number}: {row.line.Trim()}")
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void HelpReadAdapter_StaysInTheHelpAdapterNamespace()
    {
        Assert.Equal(
            "RvtPortal.Spa.Adapters.Help",
            typeof(EfHelpReadAdapter).Namespace);
    }

    [Fact]
    public void HelpReadAdapter_AppliesQueryFiltersBeforeMaterialization()
    {
        var sourcePath = Path.Combine(
            RepositoryLayout.Root,
            "RvtPortal.Spa",
            "Adapters",
            "Help",
            "EfHelpReadAdapter.cs");
        var source = File.ReadAllText(sourcePath);
        var publishedQuery = MethodSource(
            source,
            "public async Task<HelpOverviewModel> QueryPublishedAsync",
            "public async Task<HelpArticleModel?> GetPublishedArticleAsync");
        var adminQuery = MethodSource(
            source,
            "public async Task<HelpAdminOverviewModel> QueryAdminAsync",
            "public async Task<HelpArticleModel?> GetAdminArticleAsync");

        AssertFilterPrecedesMaterialization(
            publishedQuery,
            "articleQuery = ApplySearch(");
        AssertFilterPrecedesMaterialization(
            adminQuery,
            "articleQuery = ApplySearch(");
        AssertFilterPrecedesMaterialization(
            adminQuery,
            "articleQuery = articleQuery.Where(article => article.IsPublished)");
        AssertFilterPrecedesMaterialization(
            adminQuery,
            "articleQuery = articleQuery.Where(article => !article.IsPublished)");
        AssertFilterPrecedesMaterialization(
            adminQuery,
            "articleQuery = articleQuery.Where(article =>");
    }

    [Fact]
    public void HostApplicationContainsNoHelpUseCaseSources()
    {
        var oldHelpDirectory = Path.Combine(
            RepositoryLayout.Root,
            "RvtPortal.Spa",
            "Application",
            "Help");
        var remainingSources = Directory.Exists(oldHelpDirectory)
            ? Directory.EnumerateFiles(
                oldHelpDirectory,
                "*.cs",
                SearchOption.AllDirectories)
            : [];

        Assert.Empty(remainingSources);
    }

    private static string MethodSource(
        string source,
        string startMarker,
        string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0, $"Could not find {startMarker}.");
        Assert.True(end > start, $"Could not find {endMarker} after {startMarker}.");
        return source[start..end];
    }

    private static void AssertFilterPrecedesMaterialization(
        string methodSource,
        string filterMarker)
    {
        var filter = methodSource.IndexOf(filterMarker, StringComparison.Ordinal);
        var materialization = methodSource.IndexOf(
            ".ToListAsync(",
            StringComparison.Ordinal);

        Assert.True(filter >= 0, $"Could not find filter marker {filterMarker}.");
        Assert.True(materialization >= 0, "Could not find query materialization.");
        Assert.True(
            filter < materialization,
            $"{filterMarker} must occur before ToListAsync.");
    }
}
