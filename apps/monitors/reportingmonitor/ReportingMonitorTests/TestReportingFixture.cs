using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ReportingMonitor.Api;

namespace ReportingMonitorTests;

public sealed class ReportingIntegrationContractTests
{
    [Fact]
    public void PrerequisiteSql_IsIdempotentAndDocumentsHiddenOneTimeRuleIndex()
    {
        var sql = File.ReadAllText(RepositoryPath("reportingmonitor", "database", "postgres", "reporting_service_prerequisites_20260625.sql"));

        Assert.Contains("create extension if not exists pgcrypto", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("add column if not exists is_hidden_system_rule", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ux_report_rule_hidden_one_time_per_site", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RootSolutionAndCompose_IntegrateReportingMonitorWithPostgreSql()
    {
        var solution = File.ReadAllText(RepositoryPath("rvt-monitors.sln"));
        var compose = File.ReadAllText(RepositoryPath("docker-compose.yml"));

        Assert.Contains("reportingmonitor\\ReportingMonitor\\ReportingMonitor.csproj", solution, StringComparison.Ordinal);
        Assert.Contains("reportingmonitor\\ReportingMonitorTests\\ReportingMonitorTests.csproj", solution, StringComparison.Ordinal);
        Assert.Contains("reportingmonitor-api:", compose, StringComparison.Ordinal);
        Assert.Contains("reportingmonitor/ReportingMonitor/Dockerfile", compose, StringComparison.Ordinal);
        Assert.Contains("8085:8080", compose, StringComparison.Ordinal);
        Assert.Contains("ASPNETCORE_URLS: http://+:8080", compose, StringComparison.Ordinal);
        Assert.Contains("Infrastructure: local", compose, StringComparison.Ordinal);
        Assert.Contains("MonitorApi__Enabled: \"true\"", compose, StringComparison.Ordinal);
        Assert.Contains("MonitorScheduler__Enabled: \"false\"", compose, StringComparison.Ordinal);
        Assert.Contains("RVT__DATABASE_PROVIDER: PostgreSql", compose, StringComparison.Ordinal);
    }

    private static string RepositoryPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return Path.Combine([directory.FullName, .. segments]);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root from test output directory.");
    }
}

internal static class ReportingServiceProviderFactory
{
    public static ServiceProvider Create(Action<IServiceCollection>? configureServices = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=reporting_monitor_tests;Username=reporting",
                ["RVT:DATABASE_PROVIDER"] = "PostgreSql",
                ["RVT:EMAIL_ENABLED"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddLogging();
        services.AddReportingMonitor();
        configureServices?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ReportingMonitorTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
