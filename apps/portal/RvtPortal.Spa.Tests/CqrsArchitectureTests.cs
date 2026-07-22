// File summary: Verifies CQRS/MediatR boundaries through compiled types and pipeline behavior.
// Major updates:
// - 2026-07-09 pending Replaced the empty mutating-controller mediator guardrail with an all-controller rule.
// - 2026-07-09 pending Added DateExtensions guardrail against static appsettings reads.
// - 2026-07-09 pending Added lookup-service guardrails against full-table caching and sync-over-async reads.
// - 2026-07-09 pending Added monitor write-orchestration guardrail.
// - 2026-07-09 pending Added site write-orchestration guardrail.
// - 2026-07-09 pending Added installer write-orchestration guardrail.
// - 2026-07-09 pending Added alert-level write-orchestration guardrail.
// - 2026-07-09 pending Added Help CMS write-orchestration guardrail.
// - 2026-07-09 pending Added contract write-orchestration guardrail.
// - 2026-07-09 pending Added company write-orchestration guardrail.
// - 2026-07-09 pending Added notification close workflow guardrail for write orchestration cleanup.
// - 2026-07-09 pending Added report-content application-service guardrail for controller cleanup.
// - 2026-07-09 pending Added data view application-service guardrail for controller cleanup.
// - 2026-07-09 pending Added auth workflow guardrail for controller cleanup.
// - 2026-07-09 pending Added user account workflow guardrail for controller cleanup.
// - 2026-07-09 pending Added help application-service guardrail for controller cleanup.
// - 2026-07-09 pending Added report application-service guardrail for controller cleanup.
// - 2026-07-09 pending Added company and contract application-service guardrails for controller cleanup.
// - 2026-07-09 pending Added alert-level application-service guardrail for controller cleanup.
// - 2026-07-09 pending Added installer application-service guardrail for controller cleanup.
// - 2026-07-09 pending Added notification application-service guardrail for controller cleanup.
// - 2026-07-09 pending Added monitor administration read-service guardrail.
// - 2026-07-09 pending Added site detail, monitor, and notification read-service guardrail.
// - 2026-07-09 pending Added user detail and site-assignment read-service guardrail.
// - 2026-07-09 pending Added dashboard overview application-service guardrail.
// - 2026-07-08 pending Added user and dashboard list-query application-service guardrails.
// - 2026-07-08 pending Added a site-query application-service guardrail for controller cleanup.
// - 2026-07-08 pending Added archive-service DI guardrail to prevent configuration-reading constructors in business workflows.
// - 2026-07-08 pending Added hexagonal-at-the-edges guardrails for report orchestration and storage adapters.
// - 2026-07-05 pending Replaced brittle source-text architecture checks with reflection and pipeline behavior tests.
// - 2026-06-25 pending Added transaction pipeline and command-handler guardrails for monitor mutations.
// - 2026-06-09 pending Added architecture guardrails for the first targeted CQRS/MediatR refactor.

using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RVT.BusinessLogic;
using RvtPortal.Spa.Application.Companies;
using RVT.BusinessLogic.Ports.Storage;
using RVT.BusinessLogic.Reports;
using RvtPortal.Spa.Application.ReportRules;
using RvtPortal.Spa.Adapters.Archive;
using RVT.BusinessLogic.Sites;
using RvtPortal.Spa.Application.Sites;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;
using RvtPortal.Spa.Application.Lookups;
using RvtPortal.Spa.Adapters.Reporting;
using RvtPortal.Spa.Adapters.Storage;

namespace RvtPortal.Spa.Tests;

public class CqrsArchitectureTests
{
    private static readonly string[] AllowedNonTransactionalCommands =
    [
        // Uploading stores a file and deliberately manages persistence inside its handler.
        "UploadMonitorPictureCommand"
    ];

    [Fact]
    // Function summary: Verifies application command records participate in the MediatR request pipeline.
    public void ApplicationCommandTypes_AreMediatRRequests()
    {
        var violations = ApplicationCommandTypes()
            .Where(type => !ImplementsGenericInterface(type, typeof(IRequest<>)))
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    // Function summary: Verifies mutating commands opt into the unit-of-work transaction pipeline.
    public void MutatingApplicationCommands_AreTransactional()
    {
        var allowed = new HashSet<string>(AllowedNonTransactionalCommands, StringComparer.Ordinal);
        var violations = ApplicationCommandTypes()
            .Where(type => !allowed.Contains(type.Name))
            .Where(type => !typeof(ITransactionalRequest).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    // Function summary: Verifies API controllers stay as HTTP adapters instead of depending directly on MediatR.
    public void ApiControllers_DoNotDependOnMediator()
    {
        var violations = ApiControllerTypes()
            .Where(type => ConstructorParameters(type).Contains(typeof(IMediator)))
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    // Function summary: Verifies lookup searches stay async and avoid whole-table in-memory caching. Renovated from a
    // brittle source-text scan of LookupService.cs to reflection over the compiled type and its interface.
    public void LookupService_ExposesAsyncSearchesAndDoesNotCacheWholeTables()
    {
        var lookupService = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Lookups.LookupService");
        Assert.NotNull(lookupService);

        // No whole-table caching: the service must not take an IMemoryCache dependency.
        Assert.DoesNotContain(typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache), ConstructorParameters(lookupService!));

        // Async surface: every lookup operation returns a Task, so reads cannot block through sync-over-async.
        Assert.All(
            typeof(ILookupService).GetMethods(),
            method => Assert.True(
                typeof(Task).IsAssignableFrom(method.ReturnType),
                $"ILookupService.{method.Name} must return a Task."));
    }

    [Fact]
    // Function summary: Verifies DateExtensions runs no code at type load and takes its time zone from the injected
    // provider rather than static configuration. Renovated from a brittle source-text scan of DateExtensions.cs.
    public void DateExtensions_RunsNoTypeLoadCodeAndConvertsThroughInjectedProvider()
    {
        var dateExtensions = typeof(RVT.BusinessLogic.DateExtensions);

        // No static constructor => nothing (including an appsettings read) executes when the type loads.
        Assert.Null(dateExtensions.TypeInitializer);

        // Every UTC/local conversion takes its time zone from an injected IRvtDateTimeProvider parameter.
        var conversions = dateExtensions
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is "UtcToLocal" or "LocalToUtc" or "DisplayUtcAsLocal");

        Assert.All(
            conversions,
            method => Assert.Contains(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(IRvtDateTimeProvider)));
    }

    [Fact]
    // Function summary: Verifies report-rule workflows enter the business use-case port instead of owning outbound adapters in the controller.
    public void ReportRulesController_DependsOnBusinessApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(ReportRulesController));

        Assert.Contains(typeof(IReportRuleApplicationService), constructorParameters);
        Assert.DoesNotContain(typeof(IReportGenerationClient), constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTSearchContext), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies site detail, monitor, notification, and mutation workflows enter the site application service instead of owning EF, Identity, or direct command dispatch in the controller.
    public void SitesController_DelegatesDetailMonitorAndNotificationReadsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(SitesController));

        Assert.Contains(typeof(ISiteApplicationService), constructorParameters);
        Assert.Contains(typeof(ICurrentUserContextFactory), constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
        Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.UserManager<RvtPortal.Spa.Data.ApplicationUser>), constructorParameters);
        Assert.DoesNotContain(typeof(IMediator), constructorParameters);
    }

    [Theory]
    [InlineData(typeof(UsersController), "RvtPortal.Spa.Application.Users.IUserListApplicationService")]
    [InlineData(typeof(DashboardController), "RvtPortal.Spa.Application.Dashboard.IDashboardBreachApplicationService")]
    // Function summary: Verifies high-volume list endpoints enter application services instead of owning query pipelines in controllers.
    public void ListControllers_DependOnApplicationQueryServices(Type controllerType, string serviceInterfaceName)
    {
        var serviceInterface = typeof(Program).Assembly.GetType(serviceInterfaceName);

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, ConstructorParameters(controllerType));
    }

    [Fact]
    // Function summary: Verifies dashboard overview workflows enter an application service instead of owning EF and Identity reads in the controller.
    public void DashboardController_DelegatesOverviewWorkflowsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(DashboardController));
        var serviceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Dashboard.IDashboardApplicationService");

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
        Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.UserManager<RvtPortal.Spa.Data.ApplicationUser>), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies user detail and site-assignment reads enter an application service instead of owning EF and role reads in the controller.
    public void UsersController_DelegatesDetailAndSiteAssignmentReadsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(UsersController));
        var serviceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Users.IUserAdministrationReadService");

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
        Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies user account lifecycle workflows enter an application service instead of owning Identity, business-service, and email dependencies in the controller.
    public void UsersController_DelegatesAccountWorkflowsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(UsersController));
        var serviceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Users.IUserAccountWorkflowService");

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, constructorParameters);
        Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.UserManager<RvtPortal.Spa.Data.ApplicationUser>), constructorParameters);
        Assert.DoesNotContain(typeof(ICompanyService), constructorParameters);
        Assert.DoesNotContain(typeof(ILookupService), constructorParameters);
        Assert.DoesNotContain(typeof(IConfiguration), constructorParameters);
        Assert.DoesNotContain(typeof(RVT.BusinessLogic.Notifications.IAccountMessenger), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies authentication workflows enter an application service instead of owning Identity, company, and email dependencies in the controller.
    public void AuthController_DelegatesIdentityWorkflowsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(AuthController));
        var serviceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Auth.IAuthApplicationService");

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, constructorParameters);
        Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.SignInManager<RvtPortal.Spa.Data.ApplicationUser>), constructorParameters);
        Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.UserManager<RvtPortal.Spa.Data.ApplicationUser>), constructorParameters);
        Assert.DoesNotContain(typeof(ICompanyService), constructorParameters);
        Assert.DoesNotContain(typeof(IConfiguration), constructorParameters);
        Assert.DoesNotContain(typeof(RVT.BusinessLogic.Notifications.IAccountMessenger), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies data view workflows enter an application service instead of owning EF and monitor data reads in the controller.
    public void DataController_DelegatesMonitorDataWorkflowsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(DataController));
        var serviceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Data.IDataApplicationService");

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
        Assert.DoesNotContain(typeof(IMonitorDataSource), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies monitor option, assignment, detail, and mutation workflows enter application services instead of owning EF, Identity, storage, or direct command dispatch in the controller.
    public void MonitorsController_DelegatesAdministrationReadsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(MonitorsController));
        var readServiceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Monitors.IMonitorAdministrationReadService");
        var workflowServiceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Monitors.IMonitorAdministrationWorkflowService");

        Assert.NotNull(readServiceInterface);
        Assert.NotNull(workflowServiceInterface);
        Assert.Contains(readServiceInterface, constructorParameters);
        Assert.Contains(workflowServiceInterface, constructorParameters);
        Assert.Contains(typeof(ICurrentUserContextFactory), constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
        Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.UserManager<RvtPortal.Spa.Data.ApplicationUser>), constructorParameters);
        Assert.DoesNotContain(typeof(RVT.BusinessLogic.Ports.Storage.IMonitorPictureStorage), constructorParameters);
        Assert.DoesNotContain(typeof(RvtPortal.Spa.Application.Monitors.IMonitorDetailReader), constructorParameters);
        Assert.DoesNotContain(typeof(RvtPortal.Spa.Application.Monitors.IMonitorListReader), constructorParameters);
        Assert.DoesNotContain(typeof(RvtPortal.Spa.Application.Monitors.IMonitorRemovalImpactReader), constructorParameters);
        Assert.DoesNotContain(typeof(IMediator), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies notification reads and close workflows enter an application service instead of owning EF, Identity, or MediatR dispatch in the controller.
    public void NotificationsController_DelegatesReadsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(NotificationsController));
        var serviceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Notifications.INotificationApplicationService");

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, constructorParameters);
        Assert.Contains(typeof(ICurrentUserContextFactory), constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
        Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.UserManager<RvtPortal.Spa.Data.ApplicationUser>), constructorParameters);
        Assert.DoesNotContain(typeof(IMediator), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies installer workflows enter an application service instead of owning EF, Identity, config, external-client, or direct command/query dispatch in the controller.
    public void InstallerApiController_DelegatesReadsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(InstallerApiController));
        var serviceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Installers.IInstallerApplicationService");

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, constructorParameters);
        Assert.Contains(typeof(ICurrentUserContextFactory), constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
        Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.UserManager<RvtPortal.Spa.Data.ApplicationUser>), constructorParameters);
        Assert.DoesNotContain(typeof(RvtPortal.Spa.Application.Monitors.IMonitorDetailReader), constructorParameters);
        Assert.DoesNotContain(typeof(IConfiguration), constructorParameters);
        Assert.DoesNotContain(typeof(IHttpClientFactory), constructorParameters);
        Assert.DoesNotContain(typeof(IMediator), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies alert-level query, option, detail, and mutation workflows enter an application service instead of owning EF, Identity, or direct command dispatch in the controller.
    public void AlertLevelsController_DelegatesReadsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(AlertLevelsController));
        var serviceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.AlertLevels.IAlertLevelApplicationService");

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, constructorParameters);
        Assert.Contains(typeof(ICurrentUserContextFactory), constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
        Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.UserManager<RvtPortal.Spa.Data.ApplicationUser>), constructorParameters);
        Assert.DoesNotContain(typeof(IMediator), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies company and contract reads enter application services and write orchestration no longer dispatches directly from these controllers.
    public void CompanyAndContractControllers_DelegateReadsToApplicationServices()
    {
        var companyParameters = ConstructorParameters(typeof(CompaniesController));
        var companyServiceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Companies.ICompanyApplicationService");
        var contractParameters = ConstructorParameters(typeof(ContractsController));
        var contractServiceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Contracts.IContractApplicationService");

        Assert.NotNull(companyServiceInterface);
        Assert.Contains(companyServiceInterface, companyParameters);
        Assert.DoesNotContain(typeof(ICompanyService), companyParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), companyParameters);
        Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.UserManager<RvtPortal.Spa.Data.ApplicationUser>), companyParameters);
        Assert.DoesNotContain(typeof(IMediator), companyParameters);

        Assert.NotNull(contractServiceInterface);
        Assert.Contains(contractServiceInterface, contractParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), contractParameters);
        Assert.DoesNotContain(typeof(IMediator), contractParameters);
    }

    [Fact]
    // Function summary: Verifies report list/detail reads enter an application service instead of owning search-context queries in the controller.
    public void ReportsController_DelegatesReadsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(ReportsController));
        var serviceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Reports.IReportApplicationService");

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTSearchContext), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies report-content asset fetches enter an application service instead of owning EF, storage, or configuration in the controller.
    public void ReportContentController_DelegatesAssetFetchesToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(ReportContentController));
        var serviceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.ReportContent.IReportContentApplicationService");

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
        Assert.DoesNotContain(typeof(ICustomerLogoStorage), constructorParameters);
        Assert.DoesNotContain(typeof(IConfiguration), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies Help CMS reads and writes enter an application service instead of owning EF reads or direct command dispatch in the controller.
    public void HelpController_DelegatesReadsToApplicationService()
    {
        var constructorParameters = ConstructorParameters(typeof(HelpController));
        var serviceInterface = typeof(Program).Assembly.GetType("RvtPortal.Spa.Application.Help.IHelpApplicationService");

        Assert.NotNull(serviceInterface);
        Assert.Contains(serviceInterface, constructorParameters);
        Assert.DoesNotContain(typeof(IMediator), constructorParameters);
        Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
    }

    [Fact]
    // Function summary: Verifies storage abstractions are business-layer ports while concrete storage remains a host adapter.
    public void StoragePorts_LiveOutsideApiAdapters()
    {
        Assert.Equal("RVT.BusinessLogic.Ports.Storage", typeof(ICustomerLogoStorage).Namespace);
        Assert.Equal("RVT.BusinessLogic.Ports.Storage", typeof(IMonitorPictureStorage).Namespace);
        Assert.Equal("RVT.BusinessLogic.Ports.Storage", typeof(IUploadedContent).Namespace);
        Assert.Equal("RvtPortal.Spa.Adapters.Storage", typeof(CustomerLogoStorage).Namespace);
        Assert.Equal("RvtPortal.Spa.Adapters.Storage", typeof(MonitorPictureStorage).Namespace);
    }

    [Fact]
    // Function summary: Verifies site archiving is injected through a business port instead of creating configuration-bound services directly.
    public void SiteApplicationService_DependsOnArchivePort()
    {
        // ArchiveAsync is the live archive path; it must reach the export through the ISiteArchiveService port,
        // not by newing up the configuration-bound SiteArchiveService itself.
        var constructorParameters = ConstructorParameters(typeof(SiteApplicationService));
        var archiveServiceType = typeof(ISiteArchiveService).Assembly.GetType(
            "RvtPortal.Spa.Adapters.Archive.SiteArchiveService",
            throwOnError: true) ?? throw new InvalidOperationException("SiteArchiveService type not found.");

        Assert.Contains(constructorParameters, type => type.FullName == "RvtPortal.Spa.Adapters.Archive.ISiteArchiveService");
        Assert.DoesNotContain(
            archiveServiceType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            constructor => constructor.GetParameters().Length == 0);
    }

    [Fact]
    // Function summary: Verifies the business-logic core does not reference the data-access adapter assembly.
    public void BusinessLogicCore_DoesNotReferenceDataAccessAdapter()
    {
        var referenced = typeof(IRvtDateTimeProvider).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("RVT.DataAccess", referenced);
    }

    [Fact]
    // Function summary: Verifies the transaction pipeline saves changes only for transactional MediatR requests.
    public async Task TransactionPipeline_SavesOnlyTransactionalRequests()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var transactionalBehavior = new TransactionPipelineBehavior<TestTransactionalRequest, int>(unitOfWork);
        var queryBehavior = new TransactionPipelineBehavior<TestQueryRequest, int>(unitOfWork);

        var transactionalResult = await transactionalBehavior.Handle(
            new TestTransactionalRequest(),
            _ => Task.FromResult(42),
            CancellationToken.None);
        var queryResult = await queryBehavior.Handle(
            new TestQueryRequest(),
            _ => Task.FromResult(17),
            CancellationToken.None);

        Assert.Equal(42, transactionalResult);
        Assert.Equal(17, queryResult);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    // Function summary: Verifies the business layer owns no direct HTTP-client infrastructure; outbound vendor calls must cross an adapter port.
    public void BusinessLogicTypes_DoNotDependOnHttpClientFactory()
    {
        var violations = BusinessLogicTypes()
            .Where(type => ConstructorParameters(type).Contains(typeof(IHttpClientFactory)))
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    // Function summary: Verifies the email vendor SDK stays in the host adapters; the business and utility layers must not reference SendGrid.
    public void BusinessAndUtilityAssemblies_DoNotReferenceSendGrid()
    {
        var offenders = new[]
        {
            typeof(IRvtDateTimeProvider).Assembly,           // RVT.BusinessLogic
            typeof(RVT.Utilities.AzureBlobService).Assembly  // RVT.Utilities
        }
        .Where(assembly => assembly.GetReferencedAssemblies()
            .Any(reference => reference.Name?.Contains("SendGrid", StringComparison.OrdinalIgnoreCase) == true))
        .Select(assembly => assembly.GetName().Name)
        .Order(StringComparer.Ordinal)
        .ToArray();

        Assert.Empty(offenders);
    }

    // Function summary: Enumerates concrete types compiled into the RVT.BusinessLogic assembly.
    private static IEnumerable<Type> BusinessLogicTypes()
    {
        return typeof(IRvtDateTimeProvider).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
    }

    // Function summary: Enumerates concrete API controllers from the compiled SPA assembly.
    private static IEnumerable<Type> ApiControllerTypes()
    {
        return typeof(Program).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.Namespace == "RvtPortal.Spa.Api")
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
    }

    // Function summary: Enumerates application command records from the compiled SPA assembly.
    private static IEnumerable<Type> ApplicationCommandTypes()
    {
        return typeof(Program).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.Namespace?.StartsWith("RvtPortal.Spa.Application.", StringComparison.Ordinal) == true)
            .Where(type => type.Name.EndsWith("Command", StringComparison.Ordinal))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
    }

    // Function summary: Returns public constructor dependency types for an API controller.
    private static IReadOnlyCollection<Type> ConstructorParameters(Type controllerType)
    {
        return controllerType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();
    }

    // Function summary: Checks whether a type implements a specific open generic interface.
    private static bool ImplementsGenericInterface(Type type, Type genericInterfaceType)
    {
        return type.GetInterfaces().Any(interfaceType =>
            interfaceType.IsGenericType &&
            interfaceType.GetGenericTypeDefinition() == genericInterfaceType);
    }

    private sealed record TestTransactionalRequest : IRequest<int>, ITransactionalRequest;

    private sealed record TestQueryRequest : IRequest<int>;

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int TransactionCount { get; private set; }

        public int SaveChangesCount { get; private set; }

        // Function summary: Records transaction pipeline usage and invokes the supplied operation.
        public Task<TResponse> ExecuteInTransactionAsync<TResponse>(
            Func<CancellationToken, Task<TResponse>> operation,
            CancellationToken cancellationToken)
        {
            TransactionCount++;
            return operation(cancellationToken);
        }

        // Function summary: Records save requests made by the transaction pipeline.
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }
    }
}
