using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReportingMonitor.Api.Db;
using ReportingMonitor.Api.Db.EntityFramework;
using ReportingMonitor.Api.UseCases;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Infrastructure.Communications;
using Rvt.Monitor.Common.Storage;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Messaging;
using Rvt.Reporting.Pdf.Documents;
using Rvt.Reporting.Storage;
using Rvt.Reporting.Storage.PortalContent;
using Rvt.Reporting.Storage.ReportInsights;

namespace ReportingMonitor.Api;

public static class ReportingMonitorServices
{
    public static IServiceCollection AddReportingMonitor(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            var options = ReportingMonitorOptions.Bind(provider.GetRequiredService<IConfiguration>());
            options.Validate();
            return options;
        });

        services.AddSingleton(provider =>
        {
            MonitorDatabaseProviderGuard.EnsureSupported();
            var configuration = provider.GetRequiredService<IConfiguration>();
            var databaseProvider = MonitorDb.ResolveProvider(
                configuration["RVT:DATABASE_PROVIDER"],
                configuration["DatabaseProvider"]);
            if (databaseProvider != MonitorDatabaseProvider.PostgreSql)
            {
                throw new NotSupportedException("ReportingMonitor requires the PostgreSql monitor database provider.");
            }

            return new MonitorDbOptions(databaseProvider, new Dictionary<string, string>(StringComparer.Ordinal));
        });

        services.AddScoped(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for ReportingMonitor.");
            }

            var monitorOptions = provider.GetRequiredService<MonitorDbOptions>();
            var dbContextOptions = MonitorDbContextOptionsFactory.CreateOptions<ReportingMonitorContext>(connectionString, monitorOptions);
            return new ReportingMonitorContext(dbContextOptions, monitorOptions);
        });
        services.AddScoped<ReportingDbClient>();
        services.AddScoped<IReportingRuleQueries>(provider => provider.GetRequiredService<ReportingDbClient>());
        services.AddScoped<IReportingDataQueries>(provider => provider.GetRequiredService<ReportingDbClient>());
        services.AddScoped<IReportingGenerationLocks>(provider => provider.GetRequiredService<ReportingDbClient>());
        services.AddScoped<IReportingGenerationCommands>(provider => provider.GetRequiredService<ReportingDbClient>());
        services.AddScoped<IReportingHealthQueries>(provider => provider.GetRequiredService<ReportingDbClient>());

        services.AddMonitorBlobStorage(configuration => BlobStorageOptions.Bind(
            configuration,
            defaultContainer: "pdfreports",
            defaultPrefix: "rvtreports",
            legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME"));
        services.AddMonitorCommunications();
        services.AddSingleton<IOptions<ReportMessageSenderOptions>>(provider =>
        {
            var options = provider.GetRequiredService<ReportingMonitorOptions>();
            return Options.Create(new ReportMessageSenderOptions
            {
                EmailEnabled = options.EmailEnabled,
                EmailTestMode = options.EmailTestMode,
                TestReportToEmail = options.EmailTestReportToEmail
            });
        });
        services.AddSingleton<IOptions<SpaCustomerLogoClientOptions>>(provider =>
        {
            var options = provider.GetRequiredService<ReportingMonitorOptions>();
            return Options.Create(new SpaCustomerLogoClientOptions
            {
                BaseUrl = options.SpaBackendBaseUrl,
                InternalApiKey = options.SpaReportContentApiKey
            });
        });
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<ReportingMonitorOptions>();
            return new OllamaReportNarrativeOptions
            {
                Enabled = options.AiSummaryEnabled,
                BaseUrl = options.AiSummaryBaseUrl,
                Model = options.AiSummaryModel,
                TimeoutSeconds = options.AiSummaryTimeoutSeconds
            };
        });

        services.AddSingleton<IReportPdfRenderer, QuestPdfReportRenderer>();
        services.AddSingleton<IReportStorage, MonitorBlobReportStorage>();
        services.AddSingleton<IReportMessageSender, ReportMessageSender>();
        services.AddHttpClient<SpaCustomerLogoClient>();
        services.AddTransient<ICustomerLogoProvider>(provider => provider.GetRequiredService<SpaCustomerLogoClient>());
        services.AddHttpClient<OllamaReportNarrativeProvider>();
        services.AddTransient<IReportNarrativeProvider>(provider => provider.GetRequiredService<OllamaReportNarrativeProvider>());
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IReportGenerationService, ReportGenerationService>();
        services.AddScoped<GenerateScheduledReportsHandler>();
        services.AddScoped<GenerateRuleReportHandler>();
        services.AddScoped<GenerateOneTimeReportHandler>();
        services.AddSingleton<ReportingMonitorJobDispatcher>();
        return services;
    }

    public static IEndpointRouteBuilder MapReportingMonitorApi(this IEndpointRouteBuilder endpoints) =>
        ReportingMonitorApi.Map(endpoints);
}
