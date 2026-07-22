// File summary: Locks frontend-facing API route templates and DTO JSON property names before backend layering refactors.
// Major updates:
// - 2026-07-05 pending Added route and DTO stability guardrails for business-layer refactoring.

using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Tests;

public class ApiContractStabilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    // Function summary: Verifies monitor routes consumed by the SPA remain stable while controller logic is refactored.
    public void MonitorRoutes_RemainStable()
    {
        var routes = ApiRouteSnapshot.ForController<MonitorsController>();

        AssertRoute(routes, "GET", "api/monitors");
        AssertRoute(routes, "GET", "api/monitors/options");
        AssertRoute(routes, "GET", "api/monitors/assignment");
        AssertRoute(routes, "GET", "api/monitors/{id:guid}");
        AssertRoute(routes, "GET", "api/monitors/deployments/{deploymentId:guid}");
        AssertRoute(routes, "PUT", "api/monitors/{id:guid}");
        AssertRoute(routes, "POST", "api/monitors/{id:guid}/picture");
        AssertRoute(routes, "GET", "api/monitors/{id:guid}/picture");
        AssertRoute(routes, "PUT", "api/monitors/{id:guid}/fleet-number");
        AssertRoute(routes, "POST", "api/monitors/{id:guid}/contract-assignment");
        AssertRoute(routes, "DELETE", "api/monitors/{id:guid}/contract-assignment");
        AssertRoute(routes, "GET", "api/monitors/unattached");
        AssertRoute(routes, "GET", "api/monitors/{id:guid}/removal-impact");
        AssertRoute(routes, "DELETE", "api/monitors/{id:guid}/unattached");
        AssertRoute(routes, "POST", "api/monitors/default-alert-levels");
    }

    [Fact]
    // Function summary: Verifies site routes consumed by the SPA remain stable while controller logic is refactored.
    public void SiteRoutes_RemainStable()
    {
        var routes = ApiRouteSnapshot.ForController<SitesController>();

        AssertRoute(routes, "GET", "api/sites");
        AssertRoute(routes, "GET", "api/sites/options");
        AssertRoute(routes, "GET", "api/sites/{id:guid}");
        AssertRoute(routes, "POST", "api/sites/{id:guid}/customer-logo");
        AssertRoute(routes, "DELETE", "api/sites/{id:guid}/customer-logo");
        AssertRoute(routes, "GET", "api/sites/{id:guid}/customer-logo");
        AssertRoute(routes, "POST", "api/sites");
        AssertRoute(routes, "PUT", "api/sites/{id:guid}");
        AssertRoute(routes, "POST", "api/sites/{id:guid}/archive");
        AssertRoute(routes, "GET", "api/sites/{id:guid}/monitors");
        AssertRoute(routes, "GET", "api/sites/{id:guid}/notifications/open");
        AssertRoute(routes, "GET", "api/sites/{id:guid}/notification-settings");
        AssertRoute(routes, "PUT", "api/sites/{siteId:guid}/notification-settings/{siteUserId:guid}");
    }

    [Fact]
    // Function summary: Verifies report routes consumed by the SPA remain stable while controller logic is refactored.
    public void ReportRoutes_RemainStable()
    {
        var reportRuleRoutes = ApiRouteSnapshot.ForController<ReportRulesController>();
        AssertRoute(reportRuleRoutes, "GET", "api/report-rules");
        AssertRoute(reportRuleRoutes, "GET", "api/report-rules/options");
        AssertRoute(reportRuleRoutes, "GET", "api/report-rules/{id:guid}");
        AssertRoute(reportRuleRoutes, "POST", "api/report-rules");
        AssertRoute(reportRuleRoutes, "PUT", "api/report-rules/{id:guid}");
        AssertRoute(reportRuleRoutes, "DELETE", "api/report-rules/{id:guid}");
        AssertRoute(reportRuleRoutes, "GET", "api/report-rules/{id:guid}/users");
        AssertRoute(reportRuleRoutes, "GET", "api/report-rules/{id:guid}/available-users");
        AssertRoute(reportRuleRoutes, "GET", "api/report-rules/{id:guid}/assigned-users");
        AssertRoute(reportRuleRoutes, "POST", "api/report-rules/{id:guid}/users");
        AssertRoute(reportRuleRoutes, "DELETE", "api/report-rules/{id:guid}/users/{userId:guid}");
        AssertRoute(reportRuleRoutes, "POST", "api/report-rules/{id:guid}/generation-requests");

        var reportRoutes = ApiRouteSnapshot.ForController<ReportsController>();
        AssertRoute(reportRoutes, "GET", "api/reports");
        AssertRoute(reportRoutes, "GET", "api/reports/{id:guid}");

        var contentRoutes = ApiRouteSnapshot.ForController<ReportContentController>();
        AssertRoute(contentRoutes, "GET", "api/report-content/sites/{siteId:guid}/customer-logo");
    }

    [Fact]
    // Function summary: Verifies ancillary API routes remain stable while controller logic is refactored.
    public void AncillaryApiRoutes_RemainStable()
    {
        AssertRoute(ApiRouteSnapshot.ForController<AlertLevelsController>(), "GET", "api/alert-levels");
        AssertRoute(ApiRouteSnapshot.ForController<AlertLevelsController>(), "GET", "api/alert-levels/options");
        AssertRoute(ApiRouteSnapshot.ForController<AlertLevelsController>(), "GET", "api/alert-levels/{id:guid}");
        AssertRoute(ApiRouteSnapshot.ForController<AlertLevelsController>(), "POST", "api/alert-levels");
        AssertRoute(ApiRouteSnapshot.ForController<AlertLevelsController>(), "PUT", "api/alert-levels/{id:guid}");
        AssertRoute(ApiRouteSnapshot.ForController<AlertLevelsController>(), "PUT", "api/alert-levels/monitors/{monitorId:guid}/vibration");
        AssertRoute(ApiRouteSnapshot.ForController<AlertLevelsController>(), "DELETE", "api/alert-levels/{id:guid}");

        AssertRoute(ApiRouteSnapshot.ForController<DashboardController>(), "GET", "api/dashboard/summary");
        AssertRoute(ApiRouteSnapshot.ForController<DashboardController>(), "GET", "api/dashboard/breaches-alerts");
        AssertRoute(ApiRouteSnapshot.ForController<DashboardController>(), "GET", "api/dashboard/map-markers");
        AssertRoute(ApiRouteSnapshot.ForController<DashboardController>(), "GET", "api/dashboard/calendar/month");
        AssertRoute(ApiRouteSnapshot.ForController<DashboardController>(), "GET", "api/dashboard/calendar/day");

        AssertRoute(ApiRouteSnapshot.ForController<DataController>(), "GET", "api/data/deployments/{deploymentId:guid}/grid");
        AssertRoute(ApiRouteSnapshot.ForController<DataController>(), "GET", "api/data/deployments/{deploymentId:guid}/download");
        AssertRoute(ApiRouteSnapshot.ForController<DataController>(), "GET", "api/data/deployments/{deploymentId:guid}/graph");
        AssertRoute(ApiRouteSnapshot.ForController<DataController>(), "GET", "api/data/deployments/{deploymentId:guid}/traces");
        AssertRoute(ApiRouteSnapshot.ForController<DataController>(), "GET", "api/data/deployments/{deploymentId:guid}/traces/{traceId:guid}");
        AssertRoute(ApiRouteSnapshot.ForController<DataController>(), "GET", "api/data/deployments/{deploymentId:guid}/traces/{traceId:guid}/download");

        AssertRoute(ApiRouteSnapshot.ForController<NotificationsController>(), "GET", "api/notifications");
        AssertRoute(ApiRouteSnapshot.ForController<NotificationsController>(), "GET", "api/notifications/{id:guid}");
        AssertRoute(ApiRouteSnapshot.ForController<NotificationsController>(), "POST", "api/notifications/{id:guid}/close");
        AssertRoute(ApiRouteSnapshot.ForController<NotificationsController>(), "POST", "api/notifications/batch-close");
    }

    [Fact]
    // Function summary: Verifies common management routes remain stable while controller logic is refactored.
    public void ManagementApiRoutes_RemainStable()
    {
        AssertCrudRoutes<CompaniesController>("api/companies", idTemplate: "{id:guid}", hasOptions: false);
        AssertCrudRoutes<ContractsController>("api/contracts", idTemplate: "{id:guid}", hasOptions: true);

        var users = ApiRouteSnapshot.ForController<UsersController>();
        AssertRoute(users, "GET", "api/users");
        AssertRoute(users, "GET", "api/users/options");
        AssertRoute(users, "GET", "api/users/{id}");
        AssertRoute(users, "POST", "api/users");
        AssertRoute(users, "PUT", "api/users/{id}");
        AssertRoute(users, "POST", "api/users/{id}/resend-confirmation");
        AssertRoute(users, "POST", "api/users/{id}/reset-password-link");
        AssertRoute(users, "POST", "api/users/{id}/disable");
        AssertRoute(users, "POST", "api/users/{id}/enable");
        AssertRoute(users, "DELETE", "api/users/{id}");
        AssertRoute(users, "GET", "api/users/site-assignments/{siteId:guid}");
        AssertRoute(users, "POST", "api/users/site-assignments");
        AssertRoute(users, "POST", "api/users/site-assignments/contact");
        AssertRoute(users, "DELETE", "api/users/site-assignments/contact/{siteId:guid}/{userId:guid}");
        AssertRoute(users, "DELETE", "api/users/site-assignments/{siteId:guid}/{userId:guid}");
    }

    [Fact]
    // Function summary: Verifies monitor list response JSON remains compatible with the handed-over React client.
    public void QueryMonitorsResponse_JsonShape_RemainsStable()
    {
        var response = new QueryMonitorsResponse
        {
            Results =
            [
                new MonitorListItem
                {
                    Id = Guid.Empty,
                    SerialId = "SER-001",
                    TypeOfMonitor = "Dust",
                    FirmwareVersion = "1.0",
                    Manufacturer = "RVT",
                    Model = "Monitor"
                }
            ],
            Page = 1,
            PageSize = 20,
            Total = 1,
            TotalPages = 1,
            Sort = "fleetNumber",
            SortDir = SortDirections.Ascending,
            State = MonitorListStates.All
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, JsonOptions));
        var root = document.RootElement;

        AssertJsonProperties(root, "results", "total", "page", "pageSize", "totalPages", "hasPreviousPage", "hasNextPage", "searchText", "sort", "sortDir", "state", "isScopedToCurrentUser", "canManage", "canUseInstallerTools");
        AssertJsonProperties(root.GetProperty("results")[0], "id", "deploymentId", "fleetNumber", "serialId", "manufacturer", "model", "firmwareVersion", "typeOfMonitor", "contractId", "siteId", "lastDataTime", "isAssigned", "isOffline", "hasAlerts", "hasCautions", "canEdit", "canAssign", "canInstallerEdit");
    }

    [Fact]
    // Function summary: Verifies site detail response JSON remains compatible with the handed-over React client.
    public void SiteDetailResponse_JsonShape_RemainsStable()
    {
        var response = new EntityResponse<SiteDetailResponse>
        {
            Item = new SiteDetailResponse
            {
                Id = Guid.Empty,
                SiteName = "Site",
                OperatingHours = [new SiteOperatingHoursResponse { DayOfWeek = 1, DayName = "Monday" }],
                ContractList = [new ContractListItem { Id = Guid.Empty, ContractNumber = "C-1" }],
                Monitors = [new SiteMonitorItem { Id = Guid.Empty, DeploymentId = Guid.Empty, TypeOfMonitor = "Dust", ContractId = Guid.Empty, ContractNumber = "C-1" }],
                OpenNotifications = [new SiteNotificationItem { Id = Guid.Empty, MonitorId = Guid.Empty, TypeOfMonitor = "Dust", AlertType = "Alert", NotificationTime = DateTime.UtcNow, ContractId = Guid.Empty, ContractNumber = "C-1" }]
            }
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, JsonOptions));
        var item = document.RootElement.GetProperty("item");

        AssertJsonProperties(item, "id", "siteName", "customerLogoUrl", "startTime", "endTime", "satStartTime", "satEndTime", "sunStartTime", "sunEndTime", "operatingHours", "contractList", "monitors", "openNotifications", "archive", "companies", "availableContracts", "canManage");
        AssertJsonProperties(item.GetProperty("operatingHours")[0], "dayOfWeek", "dayName", "startTime", "endTime", "isClosed");
    }

    [Fact]
    // Function summary: Verifies report rule response JSON remains compatible with the handed-over React client.
    public void ReportRuleResponse_JsonShape_RemainsStable()
    {
        var response = new EntityResponse<ReportRuleDetailResponse>
        {
            Item = new ReportRuleDetailResponse
            {
                Id = Guid.Empty,
                SiteId = Guid.Empty,
                SiteName = "Site",
                Frequency = ReportFrequencyType.Weekly,
                FrequencyLabel = "Weekly",
                ReportName = "Weekly report",
                Sites = [new OptionItem { Value = Guid.Empty.ToString(), Label = "Site" }],
                Frequencies = [new OptionItem { Value = "2", Label = "Weekly" }],
                DaysOfWeek = [new OptionItem { Value = "1", Label = "Monday" }],
                AlertRuleGuidelines = [new ReportAlertRuleGuidelineItem { MonitorType = "Dust", Title = "Dust", Body = "Guideline" }]
            }
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, JsonOptions));
        var item = document.RootElement.GetProperty("item");

        AssertJsonProperties(item, "id", "siteId", "siteName", "frequency", "frequencyLabel", "dayOfWeek", "dayOfMonth", "reportName", "lastGenerated", "canManage", "sites", "frequencies", "daysOfWeek", "alertRuleGuidelines", "assignedUserCount");
        AssertJsonProperties(item.GetProperty("alertRuleGuidelines")[0], "monitorType", "title", "summary", "body", "articleSlug");
    }

    private static void AssertCrudRoutes<TController>(string prefix, string idTemplate, bool hasOptions)
    {
        var routes = ApiRouteSnapshot.ForController<TController>();
        AssertRoute(routes, "GET", prefix);
        if (hasOptions)
        {
            AssertRoute(routes, "GET", $"{prefix}/options");
        }

        AssertRoute(routes, "GET", $"{prefix}/{idTemplate}");
        AssertRoute(routes, "POST", prefix);
        AssertRoute(routes, "PUT", $"{prefix}/{idTemplate}");
        AssertRoute(routes, "DELETE", $"{prefix}/{idTemplate}");
    }

    private static void AssertRoute(IReadOnlyCollection<ApiRoute> routes, string method, string template)
    {
        Assert.Contains(routes, route => route.HttpMethod == method && route.Template == template);
    }

    private static void AssertJsonProperties(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            Assert.True(element.TryGetProperty(propertyName, out _), $"Expected JSON property '{propertyName}' was not found in {element}.");
        }
    }

    private sealed record ApiRoute(string HttpMethod, string Template);

    private static class ApiRouteSnapshot
    {
        public static IReadOnlyCollection<ApiRoute> ForController<TController>()
        {
            var controllerType = typeof(TController);
            var controllerTemplate = controllerType.GetCustomAttribute<RouteAttribute>()?.Template ?? "";
            return controllerType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>(), (method, attribute) => new { method, attribute })
                .SelectMany(item => item.attribute.HttpMethods.Select(httpMethod => new ApiRoute(httpMethod.ToUpperInvariant(), Combine(controllerTemplate, item.attribute.Template))))
                .ToList();
        }

        private static string Combine(string controllerTemplate, string? actionTemplate)
        {
            if (string.IsNullOrWhiteSpace(actionTemplate))
            {
                return controllerTemplate.Trim('/');
            }

            return $"{controllerTemplate.TrimEnd('/')}/{actionTemplate.TrimStart('/')}".Trim('/');
        }
    }
}
