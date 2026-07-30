// File summary: Guards the standalone portal application project and its forbidden dependency set.
// Major updates:
// - 2026-07-30 pending Pinned "Application" to the standalone project after the host layer became UseCases.

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

        XDocument project = XDocument.Load(ApplicationProject);
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("ProjectReference"));
    }

    [Fact]
    public void ApplicationSources_DoNotImportForbiddenFrameworksOrAdapters()
    {
        Assert.True(Directory.Exists(ApplicationRoot), $"{ApplicationRoot} must exist.");

        string[] forbidden = new[]
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
        string[] violations = [.. Directory
            .EnumerateFiles(ApplicationRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { path, line, number = index + 1 }))
            .Where(row => forbidden.Any(value =>
                row.line.Contains(value, StringComparison.Ordinal)))
            .Select(row => $"{Path.GetRelativePath(ApplicationRoot, row.path)}:{row.number}: {row.line.Trim()}")];

        Assert.Empty(violations);
    }

    [Fact]
    /// <summary>
    /// PB-4 (2026-07-30 review): "Application" names exactly one thing here - the standalone, BCL-only
    /// <c>RvtPortal.Application</c> project the two tests above guard. The host's use-case layer carried the
    /// same name under <c>RvtPortal.Spa/Application</c> while living by the opposite rule (it imports the HTTP
    /// contracts, EF and MediatR by design), which is why boundary reviews kept re-finding "leaks" there. It is
    /// <c>RvtPortal.Spa/UseCases</c> now, and nothing may put an "Application" directory back inside the host.
    /// </summary>
    public void OnlyTheStandaloneProjectIsCalledApplication()
    {
        string hostRoot = Path.Combine(RepositoryLayout.Root, "RvtPortal.Spa");

        Assert.True(
            Directory.Exists(Path.Combine(hostRoot, "UseCases")),
            "The host use-case layer lives in RvtPortal.Spa/UseCases.");
        Assert.False(
            Directory.Exists(Path.Combine(hostRoot, "Application")),
            "RvtPortal.Spa/Application is the name of the BCL-only project; the host layer is UseCases.");
        Assert.Empty(Directory
            .EnumerateFiles(hostRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadLines(path).Any(line =>
                line.Contains("RvtPortal.Spa.Application", StringComparison.Ordinal))));
    }

    [Fact]
    /// <summary>
    /// The rename made the two layers distinguishable; it did not resolve the dependency underneath. The host
    /// use cases still speak the HTTP contract types, so that count is frozen here as a follow-up marker:
    /// extracting the transport is a large change, and until it happens the number may shrink but never grow.
    /// </summary>
    public void HostUseCases_KeepTheTransportDependencyBaselineFrozen()
    {
        string useCasesRoot = Path.Combine(RepositoryLayout.Root, "RvtPortal.Spa", "UseCases");
        string[] importers = [.. Directory
            .EnumerateFiles(useCasesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadLines(path).Any(line =>
                line.Contains("RvtPortal.Spa.Api", StringComparison.Ordinal)))];

        Assert.InRange(importers.Length, 0, 27);
    }

    // G5 (2026-07-30 review): adapters implement application ports and must not
    // reach back into the API layer. The reporting pair below still maps API
    // DTOs directly, so it is pinned as a frozen two-file baseline: fixing an
    // entry should shrink the list, and no adapter file may join it.
    [Fact]
    public void AdapterSources_KeepTheApiImportBaselineFrozen()
    {
        string adaptersRoot = Path.Combine(RepositoryLayout.Root, "RvtPortal.Spa", "Adapters");
        string[] baseline =
        [
            "Reporting/ReportGenerationClient.cs",
            "Reporting/ReportGenerationGateway.cs"
        ];
        string[] offenders = [.. Directory
            .EnumerateFiles(adaptersRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadLines(path).Any(line =>
                line.Contains("RvtPortal.Spa.Api", StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(adaptersRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

        Assert.Equal(baseline, offenders);
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
        string sourcePath = Path.Combine(
            RepositoryLayout.Root,
            "RvtPortal.Spa",
            "Adapters",
            "Help",
            "EfHelpReadAdapter.cs");
        string source = File.ReadAllText(sourcePath);
        string publishedQuery = MethodSource(
            source,
            "public async Task<HelpOverviewModel> QueryPublishedAsync",
            "public async Task<HelpArticleModel?> GetPublishedArticleAsync");
        string adminQuery = MethodSource(
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
        string oldHelpDirectory = Path.Combine(
            RepositoryLayout.Root,
            "RvtPortal.Spa",
            "UseCases",
            "Help");
        IEnumerable<string> remainingSources = Directory.Exists(oldHelpDirectory)
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
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0, $"Could not find {startMarker}.");
        Assert.True(end > start, $"Could not find {endMarker} after {startMarker}.");
        return source[start..end];
    }

    private static void AssertFilterPrecedesMaterialization(
        string methodSource,
        string filterMarker)
    {
        int filter = methodSource.IndexOf(filterMarker, StringComparison.Ordinal);
        int materialization = methodSource.IndexOf(
            ".ToListAsync(",
            StringComparison.Ordinal);

        Assert.True(filter >= 0, $"Could not find filter marker {filterMarker}.");
        Assert.True(materialization >= 0, "Could not find query materialization.");
        Assert.True(
            filter < materialization,
            $"{filterMarker} must occur before ToListAsync.");
    }
}
