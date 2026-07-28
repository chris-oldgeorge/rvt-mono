using System.Text.Json;

namespace Rvt.Reporting.Service.Tests;

/// <summary>
/// Keeps the service test project discoverable while endpoint-level tests are added later.
/// Major updates: 2026-06-24 initial service test project smoke coverage.
/// </summary>
public sealed class ServiceAssemblyTests
{
    private static readonly string[] CredentialMarkers =
    [
        "Password=",
        "Pwd=",
        "Username=",
        "User Name=",
        "UserName=",
        "User ID=",
        "UserId=",
        "User=",
    ];

    [Fact]
    public void ProgramTypeIsAvailable()
    {
        Assert.Equal("Program", typeof(Program).Name);
    }

    [Fact]
    public void CommittedReportingDatabaseDefaultDoesNotContainCredentialMarkers()
    {
        var configurationPath = FindRepositoryFile(
            "services/reporting/src/Rvt.Reporting.Service/appsettings.json");
        using var configuration = JsonDocument.Parse(File.ReadAllText(configurationPath));
        var reportingDatabase = configuration.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("ReportingDatabase")
            .GetString();

        Assert.NotNull(reportingDatabase);
        Assert.False(ContainsCredentialMarker(reportingDatabase));
    }

    [Fact]
    public void NpgsqlCredentialAliasesAreDetected()
    {
        Assert.True(ContainsCredentialMarker("Host=localhost;User=reporter"));
        Assert.True(ContainsCredentialMarker("Host=localhost;Pwd=not-a-secret"));
    }

    private static bool ContainsCredentialMarker(string connectionString) =>
        CredentialMarkers.Any(marker => connectionString.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The committed reporting configuration was not found.");
    }
}
