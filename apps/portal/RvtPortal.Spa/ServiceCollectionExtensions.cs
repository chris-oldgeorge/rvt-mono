// File summary: Supports the ASP.NET Core host that serves the RVT React portal and backend API.
// Major updates:
// - 2026-07-22 Registered the framework time provider required by report-generation clients.
// - 2026-07-09 pending Bound TimeZones configuration to the injectable business date-time provider.
// - 2026-07-08 pending Registered hexagonal edge ports, adapters, and report-rule application services.
// - 2026-06-24 pending Wired SPA report generation requests to the containerized reporting service.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-09 pending Registered shared monitor detail summaries and protected picture storage.
// - 2026-06-09 pending Registered MediatR CQRS handlers for critical monitor detail workflows.
// - 2026-06-24 pending Registered deferred report generation request handling for the SPA reporting surface.
// - 2026-06-24 pending Registered customer-logo storage for report branding.
// - 2026-06-25 pending Registered transactional MediatR Unit of Work behavior for command handlers.
// - 2026-06-25 pending Registered database-backed monitor list reader for paged inventory queries.
// - 2026-06-26 pending Registered report-rule recipient reader for CQRS read routing.
// - 2026-07-09 pending Registered user administration read service for detail and site-assignment cleanup.
// - 2026-07-09 pending Registered dashboard overview application service for controller cleanup.
// - 2026-07-09 pending Registered monitor administration read service for controller cleanup.
// - 2026-07-09 pending Registered monitor administration workflow service for controller cleanup.
// - 2026-07-09 pending Registered notification application service for controller cleanup.
// - 2026-07-09 pending Registered installer application service for controller cleanup.
// - 2026-07-09 pending Registered alert-level application service for controller cleanup.
// - 2026-07-09 pending Registered company and contract application services for controller cleanup.
// - 2026-07-09 pending Registered report application service for controller cleanup.
// - 2026-07-09 pending Registered Help application service for controller cleanup.
// - 2026-07-09 pending Registered user account workflow services for controller cleanup.
// - 2026-07-09 pending Registered auth application service for controller cleanup.
// - 2026-07-09 pending Registered data view application service for controller cleanup.
// - 2026-07-09 pending Registered report-content application service for controller cleanup.
// - 2026-07-08 pending Registered user-list and dashboard breach application services for controller cleanup.

using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Infrastructure.Communications;
using Rvt.Monitor.Common.Infrastructure.Email.SendGrid;
using RVT.BusinessLogic;
using RVT.BusinessLogic.Application.Users;
using RVT.DataAccess;
using RVT.Entities.Ports.Persistence;
using RVT.BusinessLogic.Notifications;
using RVT.BusinessLogic.Ports.Notifications;
using RVT.BusinessLogic.Ports.Storage;
using RVT.BusinessLogic.Ports.Vendors;
using RVT.BusinessLogic.Reports;
using RVT.BusinessLogic.Sites;
using RvtPortal.Spa.Application.Sites;
using RvtPortal.Spa.Adapters.Notifications;
using RvtPortal.Spa.Adapters.Reporting;
using RvtPortal.Spa.Adapters.Storage;
using RvtPortal.Spa.Adapters.Vendors;
using RvtPortal.Spa.Adapters.Archive;
using RvtPortal.Spa.Application.AlertLevels;
using RvtPortal.Spa.Application.Auth;
using RvtPortal.Spa.Application.Companies;
using RvtPortal.Spa.Application.Monitors;
using RvtPortal.Spa.Application.Common;
using RvtPortal.Spa.Application.Contracts;
using RvtPortal.Spa.Application.Data;
using RvtPortal.Spa.Application.Lookups;
using RvtPortal.Spa.Application.Dashboard;
using RvtPortal.Spa.Application.Help;
using RvtPortal.Spa.Application.Installers;
using RvtPortal.Spa.Application.Notifications;
using RvtPortal.Spa.Application.ReportContent;
using RvtPortal.Spa.Application.Reports;
using RvtPortal.Spa.Application.ReportRules;
using RvtPortal.Spa.Application.Users;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa;

public static class ServiceCollectionExtensions
{
    // Function summary: Registers RVT portal business services for the current workflow.
    public static IServiceCollection AddRvtPortalBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<Program>());
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionPipelineBehavior<,>));
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IMonitorService, MonitorService>();
        services.AddOptions<OmnidotsAdapterOptions>().Configure(options =>
        {
            // Preserve the existing configuration keys (ExternalUrls:OmnidotsAdapterUrl / ...Secret) that the
            // former OmnidotsVibrationApiService read directly, now bound once at startup instead of per call.
            var section = configuration.GetSection("ExternalUrls");
            options.Url = section["OmnidotsAdapterUrl"];
            options.Secret = section["OmnidotsAdapterSecret"];
        });
        services.AddHttpClient<IVibrationVendorGateway, OmnidotsVibrationGateway>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddOptions<PortalEmailOptions>().BindConfiguration("EmailConfiguration");
        // Map the portal's existing EmailConfiguration keys onto the shared CommunicationsOptions rather than
        // calling AddMonitorCommunications(), so deployed configuration keys stay exactly as they were.
        services.AddSingleton(provider =>
        {
            var email = provider.GetRequiredService<IOptions<PortalEmailOptions>>().Value;
            return new CommunicationsOptions
            {
                EmailProvider = EmailProvider.SendGrid,
                EmailEnabled = true,
                SendGridApiKey = email.SENDGRID_API_KEY,
                FromEmail = email.Sending_Email_Address,
                FromName = "RVT Cloud"
            };
        });
        services.AddSingleton<ISendGridClientFactory, SendGridClientFactory>();
        services.AddScoped<IEmailDeliveryPort, SendGridEmailAdapter>();
        services.AddScoped<IEmailDelivery, RvtCommonEmailDelivery>();
        services.AddScoped<IAccountMessenger, AccountMessenger>();
        services.AddScoped<IApiResultMapper, ApiResultMapper>();
        services.AddScoped<ICurrentUserContextFactory, CurrentUserContextFactory>();
        services.AddScoped<IPortalUserDirectory, PortalUserDirectory>();
        services.AddScoped<ISiteApplicationService, SiteApplicationService>();
        services.AddScoped<IReportRuleApplicationService, ReportRuleApplicationService>();
        services.AddScoped<IUserAdministrationReadService, UserAdministrationReadService>();
        services.AddScoped<IUserListApplicationService, UserListApplicationService>();
        services.AddScoped<IDashboardApplicationService, DashboardApplicationService>();
        services.AddScoped<IDashboardBreachApplicationService, DashboardBreachApplicationService>();
        services.AddScoped<IMonitorAdministrationReadService, MonitorAdministrationReadService>();
        services.AddScoped<IMonitorAdministrationWorkflowService, MonitorAdministrationWorkflowService>();
        services.AddScoped<INotificationApplicationService, NotificationApplicationService>();
        services.AddScoped<IInstallerApplicationService, InstallerApplicationService>();
        services.AddScoped<IAlertLevelApplicationService, AlertLevelApplicationService>();
        services.AddScoped<ICompanyApplicationService, CompanyApplicationService>();
        services.AddScoped<IContractApplicationService, ContractApplicationService>();
        services.AddScoped<IReportApplicationService, ReportApplicationService>();
        services.AddScoped<IHelpApplicationService, HelpApplicationService>();
        services.AddScoped<IUserAccountWorkflowService, UserAccountWorkflowService>();
        services.AddScoped<IUserAccountNotificationService, UserAccountNotificationService>();
        services.AddScoped<IAuthApplicationService, AuthApplicationService>();
        services.AddScoped<IDataApplicationService, DataApplicationService>();
        services.AddScoped<IReportContentApplicationService, ReportContentApplicationService>();
        services.AddScoped<IReportGenerationGateway, ReportGenerationGateway>();
        services.AddScoped<IMonitorDataSource, MonitorDataSource>();
        services.AddScoped<IMonitorDetailSummaryService, MonitorDetailSummaryService>();
        services.AddScoped<IMonitorPictureStorage, MonitorPictureStorage>();
        services.AddSingleton<IBlobStorageClientFactory, BlobStorageClientFactory>();
        services.AddScoped<ICustomerLogoStorage, CustomerLogoStorage>();
        services.AddScoped<ISiteArchiveQueryCatalog, SiteArchiveQueryCatalog>();
        services.AddScoped<ISiteArchiveQueryExecutor, SiteArchiveQueryExecutor>();
        services.AddScoped<ISiteArchiveCsvWriter, SiteArchiveCsvWriter>();
        services.AddScoped<ISiteArchiveWorkspaceFactory, SiteArchiveWorkspaceFactory>();
        services.AddScoped<ISiteArchiveService, SiteArchiveService>();
        services.AddScoped<IMonitorDetailReader, MonitorDetailReader>();
        services.AddScoped<IMonitorListReader, MonitorListReader>();
        services.AddScoped<IMonitorRemovalImpactReader, MonitorRemovalImpactReader>();
        services.AddScoped<IMonitorReadAuthorizationService, MonitorReadAuthorizationService>();
        services.AddScoped<IReportRuleRecipientReader, ReportRuleRecipientReader>();
        services.AddOptions<ReportGenerationServiceOptions>().BindConfiguration("ReportGenerationService");
        services.AddHttpClient<IReportGenerationClient, ReportingServiceReportGenerationClient>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton(TimeProvider.System);
        services.Configure<RvtTimeZoneOptions>(configuration.GetSection("TimeZones"));

        // Business-layer + data-access service registrations (formerly InitBusinessLogic).
        services.AddOptions<RvtTimeZoneOptions>();
        services.AddSingleton<IRvtDateTimeProvider, RvtDateTimeProvider>();
        // The EF contexts are registered by ConfigureDatabases in Program.cs (AddDbContext, with the shared
        // connection and provider options). InitDataAccess used to TryAddScoped them here, which never won the
        // registration and only resolved at all via a parameterless-constructor fallback that read appsettings
        // from the process working directory. Both are gone.
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IDeploymentRepository, DeploymentRepository>();
        services.AddScoped<IMonitorRepository, MonitorRepository>();
        services.AddScoped<IAlertlevelRepository, AlertlevelRepository>();
        services.AddScoped<ISearchQueryReader, SearchQueryReader>();
        services.AddScoped<IOmnidotsSensorRepository, OmnidotsSensorRepository>();
        services.AddScoped<ISvantekMonitorStatusRepository, SvantekMonitorStatusRepository>();
        services.AddScoped<IOmnidotsBreachesAndAlertsRepository, OmnidotsBreachesAndAlertsRepository>();
        return services;
    }
}
