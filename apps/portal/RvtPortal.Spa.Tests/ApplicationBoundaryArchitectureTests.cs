// File summary: Guards the standalone portal application project and its forbidden dependency set.

using System.Xml.Linq;
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
}
