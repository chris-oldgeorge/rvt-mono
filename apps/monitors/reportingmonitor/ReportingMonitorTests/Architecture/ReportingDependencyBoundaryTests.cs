using Microsoft.Extensions.DependencyInjection;
using ReportingMonitor.Api;
using ReportingMonitor.Api.Db;
using ReportingMonitor.Api.UseCases;
using Rvt.Monitor.Common.Storage;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Messaging;
using Rvt.Reporting.Storage;

namespace ReportingMonitorTests.Architecture;

public sealed class ReportingDependencyBoundaryTests
{
    [Fact]
    public void ApplicationCodeOutsideCompositionAndDataAdapters_DoesNotReferenceEfCoreEntitiesOrNpgsql()
    {
        var sourceFiles = Directory
            .GetFiles(Path.Combine(FindRepositoryRoot(), "reportingmonitor", "ReportingMonitor", "api"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}api{Path.DirectorySeparatorChar}db{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.EndsWith("ReportingMonitorServices.cs", StringComparison.Ordinal));

        Assert.All(sourceFiles, path =>
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("ReportingMonitorContext", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Npgsql", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ReportRuleEntity", text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Composition_ResolvesEveryNarrowReportingPortAndScheduledJobGraph()
    {
        using var provider = ReportingServiceProviderFactory.Create();
        using var scope = provider.CreateScope();

        var dbClient = scope.ServiceProvider.GetRequiredService<ReportingDbClient>();
        Assert.Same(dbClient, scope.ServiceProvider.GetRequiredService<IReportingRuleQueries>());
        Assert.Same(dbClient, scope.ServiceProvider.GetRequiredService<IReportingDataQueries>());
        Assert.Same(dbClient, scope.ServiceProvider.GetRequiredService<IReportingGenerationLocks>());
        Assert.Same(dbClient, scope.ServiceProvider.GetRequiredService<IReportingGenerationCommands>());
        Assert.Same(dbClient, scope.ServiceProvider.GetRequiredService<IReportingHealthQueries>());
        Assert.IsType<GenerateScheduledReportsHandler>(scope.ServiceProvider.GetRequiredService<GenerateScheduledReportsHandler>());
        Assert.IsType<ReportGenerationService>(scope.ServiceProvider.GetRequiredService<IReportGenerationService>());
        Assert.IsType<ReportingMonitorJobDispatcher>(provider.GetRequiredService<ReportingMonitorJobDispatcher>());
        Assert.IsType<LocalFileBlobStorageService>(provider.GetRequiredService<IBlobStorageService>());
        Assert.IsType<MonitorBlobReportStorage>(provider.GetRequiredService<IReportStorage>());
        var blobOptions = provider.GetRequiredService<BlobStorageOptions>();
        Assert.Equal("pdfreports", blobOptions.Container);
        Assert.Equal("rvtreports", blobOptions.Prefix);
        Assert.IsType<ReportMessageSender>(provider.GetRequiredService<IReportMessageSender>());
    }

    [Fact]
    public void MessagingAssembly_IsProviderNeutral()
    {
        Assert.DoesNotContain(
            typeof(ReportMessageSender).Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.Contains("SendGrid", StringComparison.OrdinalIgnoreCase) == true);

        var messagingProject = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "reportingmonitor",
            "Rvt.Reporting.Messaging",
            "Rvt.Reporting.Messaging.csproj"));
        Assert.DoesNotContain("SendGrid", messagingProject, StringComparison.OrdinalIgnoreCase);

        var messagingSource = Directory
            .EnumerateFiles(
                Path.Combine(FindRepositoryRoot(), "reportingmonitor", "Rvt.Reporting.Messaging"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        Assert.DoesNotContain(messagingSource, text =>
            text.Contains("using " + "SendGrid", StringComparison.Ordinal) ||
            text.Contains("Rvt.Reporting.Messaging.SendGrid", StringComparison.Ordinal) ||
            text.Contains("PackageReference Include=\"SendGrid\"", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root from test output directory.");
    }
}
