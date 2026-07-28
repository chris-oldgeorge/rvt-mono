using System.Text.Json;
using Npgsql;

namespace ReportingMonitorTests;

/// <summary>
/// Guards the committed reporting configuration against shipped credentials.
/// </summary>
/// <remarks>
/// The retired <c>services/reporting</c> module carried this guard on its own
/// <c>appsettings.json</c>. Consolidating reporting into this module would have
/// removed the protection along with the module, so it is carried over onto the
/// surviving configuration rather than dropped.
/// </remarks>
public sealed class CommittedConfigurationSecurityTests
{
    [Fact]
    public void CommittedDefaultConnectionHasNoParsedCredentials()
    {
        string configurationPath = FindRepositoryFile(
            "apps/monitors/reportingmonitor/ReportingMonitor/appsettings.json");
        using JsonDocument configuration = JsonDocument.Parse(File.ReadAllText(configurationPath));
        string? defaultConnection = configuration.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString();

        Assert.False(HasParsedCredentials(defaultConnection));
    }

    [Theory]
    [InlineData("Host=localhost;User=reporter")]
    [InlineData("Host=localhost;Username=reporter")]
    [InlineData("Host=localhost;User Name=reporter")]
    [InlineData("Host=localhost;UserName=reporter")]
    [InlineData("Host=localhost;User ID=reporter")]
    [InlineData("Host=localhost;UserId=reporter")]
    [InlineData("Host=localhost;Password=not-a-secret")]
    [InlineData("Host=localhost;Pwd=not-a-secret")]
    [InlineData("Host=localhost;UID=reporter")]
    [InlineData("Host=localhost;PSW=not-a-secret")]
    public void NpgsqlCredentialAliasesAreDetected(string connectionString)
    {
        Assert.True(HasParsedCredentials(connectionString));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Host=localhost;Port=5432;Database=rvt")]
    public void CredentialFreeConnectionStringsAreAccepted(string? connectionString)
    {
        Assert.False(HasParsedCredentials(connectionString));
    }

    private static bool HasParsedCredentials(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder(connectionString);
            return !string.IsNullOrEmpty(builder.Username) ||
                   !string.IsNullOrEmpty(builder.Password) ||
                   !string.IsNullOrEmpty(builder.SslPassword);
        }
        catch (ArgumentException)
        {
            // An unparseable connection string is treated as unsafe.
            return true;
        }
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The committed reporting configuration was not found.");
    }
}
