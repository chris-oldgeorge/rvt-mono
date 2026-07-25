using Microsoft.Extensions.DependencyInjection;
using ReportingMonitor.Api;
using ReportingMonitor.Api.Db;
using ReportingMonitor.Api.Storage;
using ReportingMonitor.Api.UseCases;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Messaging;
using Rvt.Reporting.Storage;
using Rvt.Storage;
using Rvt.Storage.AzureBlob;
using Rvt.Storage.Local;
using Rvt.Storage.S3;

namespace ReportingMonitorTests.Architecture;

public sealed class ReportingDependencyBoundaryTests
{
    [Fact]
    public void ApplicationCodeOutsideCompositionAndDataAdapters_DoesNotReferenceEfCoreEntitiesOrNpgsql()
    {
        var sourceFiles = Directory
            .GetFiles(Path.Combine(FindMonitorsRoot(), "reportingmonitor", "ReportingMonitor", "api"), "*.cs", SearchOption.AllDirectories)
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
        Assert.IsType<LocalObjectStorageClient>(GetReportClient(provider));
        Assert.IsType<ConfiguredReportObjectUriResolver>(provider.GetRequiredService<IReportObjectUriResolver>());
        Assert.IsType<MonitorBlobReportStorage>(provider.GetRequiredService<IReportStorage>());
        Assert.Equal(
            Path.GetFullPath("/data/rvt/blobs/pdfreports/rvtreports/probe.pdf"),
            ResolveReportUri(provider, "probe.pdf").LocalPath);
        Assert.IsType<ReportMessageSender>(provider.GetRequiredService<IReportMessageSender>());
    }

    [Fact]
    public void Composition_ProviderNeutralKey_SelectsLocalAheadOfRvtAliases()
    {
        using var provider = ReportingServiceProviderFactory.Create(
            configurationValues: new Dictionary<string, string?>
            {
                ["BlobStorage:Provider"] = "Local",
                ["RVT:BLOB_PROVIDER"] = "AzureBlob",
                ["RVT__BLOB_PROVIDER"] = "S3",
            });

        Assert.IsType<LocalObjectStorageClient>(GetReportClient(provider));
    }

    [Fact]
    public void Composition_BlankProviderNeutralKey_UsesRvtAzureAlias()
    {
        using var provider = ReportingServiceProviderFactory.Create(
            configurationValues: new Dictionary<string, string?>
            {
                ["BlobStorage:Provider"] = " ",
                ["RVT:BLOB_PROVIDER"] = "AzureBlob",
                ["RVT__BLOB_PROVIDER"] = "S3",
                ["BlobStorage:AzureServiceUri"] = "https://storage.example.test",
            });

        Assert.IsType<AzureBlobObjectStorageClient>(GetReportClient(provider));
        Assert.Equal(
            "https://storage.example.test/pdfreports/rvtreports/probe.pdf",
            ResolveReportUri(provider, "probe.pdf").AbsoluteUri);
    }

    [Fact]
    public void Composition_LiteralRvtProviderAlias_SelectsS3()
    {
        using var provider = ReportingServiceProviderFactory.Create(
            configurationValues: new Dictionary<string, string?>
            {
                ["RVT__BLOB_PROVIDER"] = "S3",
                ["BlobStorage:S3Bucket"] = "report-bucket",
                ["BlobStorage:S3Region"] = "eu-central-1",
            });

        Assert.IsType<S3ObjectStorageClient>(GetReportClient(provider));
        Assert.Equal(
            "s3://report-bucket/rvtreports/probe.pdf",
            ResolveReportUri(provider, "probe.pdf").AbsoluteUri);
    }

    [Fact]
    public void Composition_LegacyReportContainerAlias_OverridesTheReportingContainerDefault()
    {
        using var provider = ReportingServiceProviderFactory.Create(
            configurationValues: new Dictionary<string, string?>
            {
                ["RVT__BLOB_REPORT_CONTAINER_NAME"] = "legacy-report-container",
            });

        Assert.Equal(
            Path.GetFullPath("/data/rvt/blobs/legacy-report-container/rvtreports/probe.pdf"),
            ResolveReportUri(provider, "probe.pdf").LocalPath);
    }

    [Fact]
    public void Composition_UnsupportedProvider_ThrowsExactSafeMessage()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReportingServiceProviderFactory.Create(
                configurationValues: new Dictionary<string, string?>
                {
                    ["BlobStorage:Provider"] = "GoogleCloud",
                }));

        Assert.Equal(
            "Unsupported blob storage provider 'GoogleCloud'. Allowed values are 'Local', 'AzureBlob', and 'S3'.",
            exception.Message);
    }

    [Fact]
    public void MessagingAssembly_IsProviderNeutral()
    {
        Assert.DoesNotContain(
            typeof(ReportMessageSender).Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.Contains("SendGrid", StringComparison.OrdinalIgnoreCase) == true);

        var messagingProject = File.ReadAllText(Path.Combine(
            FindMonitorsRoot(),
            "reportingmonitor",
            "Rvt.Reporting.Messaging",
            "Rvt.Reporting.Messaging.csproj"));
        Assert.DoesNotContain("SendGrid", messagingProject, StringComparison.OrdinalIgnoreCase);

        var messagingSource = Directory
            .EnumerateFiles(
                Path.Combine(FindMonitorsRoot(), "reportingmonitor", "Rvt.Reporting.Messaging"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        Assert.DoesNotContain(messagingSource, text =>
            text.Contains("using " + "SendGrid", StringComparison.Ordinal) ||
            text.Contains("Rvt.Reporting.Messaging.SendGrid", StringComparison.Ordinal) ||
            text.Contains("PackageReference Include=\"SendGrid\"", StringComparison.Ordinal) ||
            text.Contains(string.Concat("Rvt.Monitor.Common.", "Infrastructure"), StringComparison.Ordinal));
    }

    private static string FindMonitorsRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return Path.Combine(directory.FullName, "apps", "monitors");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root from test output directory.");
    }

    private static IObjectStorageClient GetReportClient(IServiceProvider provider) =>
        provider
            .GetRequiredService<IObjectStorageClientFactory>()
            .GetRequiredClient(ReportingStorageResourceNames.Reports);

    private static Uri ResolveReportUri(IServiceProvider provider, string key) =>
        provider
            .GetRequiredService<IReportObjectUriResolver>()
            .Resolve(StorageObjectKey.Parse(key));
}
