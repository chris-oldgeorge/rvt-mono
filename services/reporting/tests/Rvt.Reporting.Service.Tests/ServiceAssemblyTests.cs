using System.Text.Json;
using Npgsql;

namespace Rvt.Reporting.Service.Tests;

/// <summary>
/// Keeps the service test project discoverable while endpoint-level tests are added later.
/// Major updates: 2026-06-24 initial service test project smoke coverage.
/// </summary>
public sealed class ServiceAssemblyTests
{
    [Fact]
    public void ProgramTypeIsAvailable()
    {
        Assert.Equal("Program", typeof(Program).Name);
    }

    [Fact]
    public void CommittedReportingDatabaseDefaultHasNoParsedCredentials()
    {
        var configurationPath = FindRepositoryFile(
            "services/reporting/src/Rvt.Reporting.Service/appsettings.json");
        using var configuration = JsonDocument.Parse(File.ReadAllText(configurationPath));
        var reportingDatabase = configuration.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("ReportingDatabase")
            .GetString();

        var connectionString = Assert.IsType<string>(reportingDatabase);
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.True(string.IsNullOrEmpty(builder.Username));
        Assert.True(string.IsNullOrEmpty(builder.Password));
        Assert.True(string.IsNullOrEmpty(builder.SslPassword));
    }

    [Fact]
    public void NpgsqlCredentialAliasesAreDetected()
    {
        Assert.True(HasParsedCredentials("Host=localhost;User=reporter"));
        Assert.True(HasParsedCredentials("Host=localhost;Username=reporter"));
        Assert.True(HasParsedCredentials("Host=localhost;User Name=reporter"));
        Assert.True(HasParsedCredentials("Host=localhost;UserName=reporter"));
        Assert.True(HasParsedCredentials("Host=localhost;User ID=reporter"));
        Assert.True(HasParsedCredentials("Host=localhost;UserId=reporter"));
        Assert.True(HasParsedCredentials("Host=localhost;Password=not-a-secret"));
        Assert.True(HasParsedCredentials("Host=localhost;Pwd=not-a-secret"));
        Assert.True(HasParsedCredentials("Host=localhost;UID=reporter"));
        Assert.True(HasParsedCredentials("Host=localhost;PSW=not-a-secret"));
        Assert.True(HasParsedCredentials("Host=localhost;SSL Password=not-a-secret"));
    }

    private static bool HasParsedCredentials(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return !string.IsNullOrEmpty(builder.Username) ||
                   !string.IsNullOrEmpty(builder.Password) ||
                   !string.IsNullOrEmpty(builder.SslPassword);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

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
