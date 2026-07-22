// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-26 pending Added moved-monitor dashboard and calendar ownership-window regressions.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public class DashboardMapCalendarTests
{
    private const string AdminEmail = "dashboard.admin@rvt.test";
    private const string MasterAdminEmail = "dashboard.master@rvt.test";
    private const string CompanyUserEmail = "dashboard.company@rvt.test";
    private const string Password = "P8sSw0rd9$";

    // Probe values seeded once by SeedDashboardScenarioAsync and asserted by the map/calendar tests.
    private const double MarkerLat = 51.501;
    private const double MarkerLng = -0.141;
    private const string DustAlertField = "pm10";
    private const double DustAlertLevel = 61;
    private const double VibrationAlertLevel = 5.5;

    [Fact]
    // Function summary: Handles the dashboard summary returns role scoped counts and notifications workflow for this module.
    public async Task DashboardSummary_ReturnsRoleScopedCountsAndNotifications()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedDashboardScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: ids.CompanyId);
        await AssignUserToSiteAsync(factory, companyUser.Id, ids.SiteId);

        var adminClient = CreateClient(factory);
        await LoginAsync(adminClient, AdminEmail, Password);
        var adminSummary = await adminClient.GetFromJsonAsync<DashboardSummaryResponse>("/api/dashboard/summary");

        var companyClient = CreateClient(factory);
        await LoginAsync(companyClient, CompanyUserEmail, Password);
        var companySummary = await companyClient.GetFromJsonAsync<DashboardSummaryResponse>("/api/dashboard/summary");

        Assert.Equal(RoleNames.RVTAdmin, adminSummary!.Role);
        Assert.Equal(3, adminSummary.MonitorCounts.Assigned);
        Assert.Equal(2, adminSummary.OpenAlerts);
        Assert.Equal(1, adminSummary.OpenCautions);
        Assert.Contains(adminSummary.Sites, site => site.Value == ids.SiteId.ToString());
        Assert.Contains(adminSummary.Sites, site => site.Value == ids.OtherSiteId.ToString());
        Assert.Equal(RoleNames.CompanyUser, companySummary!.Role);
        Assert.Equal(1, companySummary.MonitorCounts.Assigned);
        Assert.Equal(1, companySummary.OpenAlerts);
        Assert.Equal(0, companySummary.OpenCautions);
        Assert.Single(companySummary.Sites);
        Assert.Equal(ids.SiteId.ToString(), companySummary.Sites[0].Value);
        Assert.Single(companySummary.CalendarDeployments);
        Assert.Equal(ids.DeploymentId.ToString(), companySummary.CalendarDeployments[0].Value);
    }

    [Fact]
    // Function summary: Maps markers are scoped by visible sites into the shape required by callers.
    public async Task MapMarkers_AreScopedByVisibleSites()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedDashboardScenarioAsync(factory);
        var companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: ids.CompanyId);
        await AssignUserToSiteAsync(factory, companyUser.Id, ids.SiteId);

        var client = CreateClient(factory);
        await LoginAsync(client, CompanyUserEmail, Password);

        var visible = await client.GetFromJsonAsync<MapMarkersResponse>($"/api/dashboard/map-markers?siteId={ids.SiteId}");
        var hidden = await client.GetAsync($"/api/dashboard/map-markers?siteId={ids.OtherSiteId}");

        Assert.True(visible!.IsScopedToCurrentUser);
        Assert.Equal(ids.SiteId, visible.SiteId);
        Assert.Single(visible.Markers);
        Assert.Equal(ids.MonitorId, visible.Markers[0].MonitorId);
        Assert.Equal(ids.DeploymentId, visible.Markers[0].DeploymentId);
        AssertApproximately(MarkerLat, visible.Markers[0].Latitude);
        AssertApproximately(MarkerLng, visible.Markers[0].Longitude);
        Assert.True(visible.Markers[0].Alert);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    // Function summary: Handles the calendar month and day return notifications and alert levels workflow for this module.
    public async Task CalendarMonthAndDay_ReturnNotificationsAndAlertLevels()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedDashboardScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var month = await client.GetFromJsonAsync<CalendarMonthResponse>(
            $"/api/dashboard/calendar/month?deploymentId={ids.DeploymentId}&year={ids.Today.Year}&month={ids.Today.Month}");
        var day = await client.GetFromJsonAsync<CalendarDayResponse>(
            $"/api/dashboard/calendar/day?monitorId={ids.MonitorId}&year={ids.Today.Year}&month={ids.Today.Month}&day={ids.Today.Day}");

        Assert.Equal(ids.MonitorId, month!.MonitorId);
        Assert.Equal(ids.DeploymentId, month.DeploymentId);
        Assert.Equal(MonitorTypeEnum.Dust.ToString(), month.TypeOfMonitor);
        Assert.Contains(month.Days, item => item.Date.Date == ids.Today && item.Status == "Alert" && item.NotificationCount == 1);
        Assert.Contains(month.Deployments, deployment => deployment.Value == ids.DeploymentId.ToString());
        Assert.Equal(ids.MonitorId, day!.MonitorId);
        Assert.Equal(ids.Today, day.DisplayDay.Date);
        AssertApproximately(DustAlertLevel, Assert.Single(day.Values, value => value.Label == DustAlertField).Value);
        Assert.Contains(day.AlertLevels, level => level.MonitorId == ids.MonitorId && level.AlertField == DustAlertField);
        Assert.Single(day.Notifications);
        Assert.Equal(ids.AlertNotificationId, day.Notifications[0].Id);
    }

    [Fact]
    // Function summary: Handles the master admin breaches alerts returns vibration rows for the requested day workflow for this module.
    public async Task MasterAdminBreachesAlerts_ReturnsVibrationRowsForTheRequestedDay()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedDashboardScenarioAsync(factory);
        await factory.SeedUserAsync(MasterAdminEmail, Password, RoleNames.RVTMasterAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client, MasterAdminEmail, Password);

        var response = await client.GetFromJsonAsync<BreachesAlertsResponse>(
            $"/api/dashboard/breaches-alerts?date={ids.Today:yyyy-MM-dd}&sort=notificationTime&sortDir=Descending");

        Assert.Equal(ids.Today, response!.Date.Date);
        Assert.Single(response.Results);
        Assert.Equal(ids.VibrationMonitorId, response.Results[0].MonitorId);
        AssertApproximately(VibrationAlertLevel, response.Results[0].Xvtop);
        Assert.Null(response.Results[0].Yvtop);
    }

    [Fact]
    // Function summary: Verifies dashboard current rows ignore monitor notifications outside the active ownership window.
    public async Task DashboardCurrentRows_IgnoreMovedMonitorGapNotifications()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedMovedMonitorDashboardScenarioAsync(factory);
        var companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: ids.NewCompanyId);
        await AssignUserToSiteAsync(factory, companyUser.Id, ids.NewSiteId);

        var client = CreateClient(factory);
        await LoginAsync(client, CompanyUserEmail, Password);

        var summary = await client.GetFromJsonAsync<DashboardSummaryResponse>("/api/dashboard/summary");
        var markers = await client.GetFromJsonAsync<MapMarkersResponse>($"/api/dashboard/map-markers?siteId={ids.NewSiteId}");
        var gapDay = await client.GetAsync(
            $"/api/dashboard/calendar/day?monitorId={ids.MonitorId}&year={ids.GapNotificationTime.Year}&month={ids.GapNotificationTime.Month}&day={ids.GapNotificationTime.Day}");

        Assert.Equal(1, summary!.MonitorCounts.Assigned);
        Assert.Equal(0, summary.OpenAlerts);
        Assert.Empty(summary.RecentNotifications);
        Assert.Single(markers!.Markers);
        Assert.False(markers.Markers[0].Alert);
        Assert.Equal(HttpStatusCode.NotFound, gapDay.StatusCode);
    }

    // Function summary: Initializes dashboard scenario state required by the application.
    private static async Task<DashboardScenarioIds> SeedDashboardScenarioAsync(SpaTestApplicationFactory factory)
    {
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var otherSiteId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var otherContractId = Guid.NewGuid();
        var monitorId = Guid.NewGuid();
        var otherMonitorId = Guid.NewGuid();
        var vibrationMonitorId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        var otherDeploymentId = Guid.NewGuid();
        var vibrationDeploymentId = Guid.NewGuid();
        var alertNotificationId = Guid.NewGuid();
        var today = new DateTime(2026, 5, 24);

        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Dashboard Company", Contracts = [] },
            new Company { Id = otherCompanyId, CompanyName = "Other Dashboard Company", Contracts = [] },
            new Site { Id = siteId, SiteName = "Dashboard Site", CreateDate = today.AddDays(-60), Contracts = [] },
            new Site { Id = otherSiteId, SiteName = "Other Dashboard Site", CreateDate = today.AddDays(-60), Contracts = [] },
            new Contract { Id = contractId, ContractNumber = "P8-CON-001", CompanyId = companyId, SiteiD = siteId, OnHireDate = today.AddDays(-20) },
            new Contract { Id = otherContractId, ContractNumber = "P8-CON-002", CompanyId = otherCompanyId, SiteiD = otherSiteId, OnHireDate = today.AddDays(-20) },
            Monitor(monitorId, "P8-DUST", "SER-P8-D", MonitorTypeEnum.Dust, today),
            Monitor(otherMonitorId, "P8-NOISE", "SER-P8-N", MonitorTypeEnum.Noise, today),
            Monitor(vibrationMonitorId, "P8-VIBE", "SER-P8-V", MonitorTypeEnum.Vibration, today),
            new Deployment
            {
                Id = deploymentId,
                ContractId = contractId,
                MonitorId = monitorId,
                StartDate = today.AddDays(-10),
                Lat = MarkerLat,
                Lng = MarkerLng,
                Location = "Dashboard marker",
                What3words = "filled.count.soap"
            },
            new Deployment
            {
                Id = otherDeploymentId,
                ContractId = otherContractId,
                MonitorId = otherMonitorId,
                StartDate = today.AddDays(-10),
                Lat = 52.1,
                Lng = -1.5,
                Location = "Hidden marker"
            },
            new Deployment
            {
                Id = vibrationDeploymentId,
                ContractId = otherContractId,
                MonitorId = vibrationMonitorId,
                StartDate = today.AddDays(-10),
                Lat = 51.6,
                Lng = -0.2
            },
            new Notification
            {
                Id = alertNotificationId,
                MonitorId = monitorId,
                NotificationTime = today.AddHours(10),
                AlertType = AlertTypeEnum.Alert,
                AlertField = DustAlertField,
                LimitOn = 50,
                Level = DustAlertLevel,
                AveragingPeriod = (int)AveragingPeriodsDustEnum._1_hour
            },
            new Notification
            {
                Id = Guid.NewGuid(),
                MonitorId = otherMonitorId,
                NotificationTime = today.AddHours(11),
                AlertType = AlertTypeEnum.Caution,
                AlertField = "LAeq",
                LimitOn = 70,
                Level = 72,
                AveragingPeriod = (int)AveragingPeriodsNoiseEnum._15_min
            },
            new Notification
            {
                Id = Guid.NewGuid(),
                MonitorId = vibrationMonitorId,
                NotificationTime = today.AddHours(12),
                AlertType = AlertTypeEnum.Alert,
                AlertField = "Xvtop",
                LimitOn = 5,
                Level = VibrationAlertLevel,
                AveragingPeriod = (int)AveragingPeriodsVibrationEnum._1_min
            },
            new Alertlevel
            {
                Id = Guid.NewGuid(),
                MonitorId = monitorId,
                SerialId = "SER-P8-D",
                AlertField = DustAlertField,
                AlertType = AlertTypeEnum.Alert,
                LimitOn = 50,
                LimitOff = 45,
                AveragingPeriod = (int)AveragingPeriodsDustEnum._1_hour,
                IsActive = true,
                Weekdays = true,
                Saturdays = true,
                Sundays = true
            });

        return new DashboardScenarioIds(
            companyId,
            siteId,
            otherSiteId,
            monitorId,
            otherMonitorId,
            vibrationMonitorId,
            deploymentId,
            otherDeploymentId,
            alertNotificationId,
            today);
    }

    // Function summary: Seeds a moved monitor with a current deployment and a stale gap notification.
    private static async Task<MovedMonitorDashboardIds> SeedMovedMonitorDashboardScenarioAsync(SpaTestApplicationFactory factory)
    {
        var oldCompanyId = Guid.NewGuid();
        var newCompanyId = Guid.NewGuid();
        var oldSiteId = Guid.NewGuid();
        var newSiteId = Guid.NewGuid();
        var oldContractId = Guid.NewGuid();
        var newContractId = Guid.NewGuid();
        var monitorId = Guid.NewGuid();
        var oldDeploymentId = Guid.NewGuid();
        var newDeploymentId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
        var oldDeploymentEnd = baseTime.AddDays(-10);
        var newDeploymentStart = baseTime.AddDays(-4);
        var gapNotificationTime = baseTime.AddDays(-7).AddHours(1);

        await factory.SeedDomainEntitiesAsync(
            new Company { Id = oldCompanyId, CompanyName = "Old Dashboard Owner", Contracts = [] },
            new Company { Id = newCompanyId, CompanyName = "New Dashboard Owner", Contracts = [] },
            new Site { Id = oldSiteId, SiteName = "Old Dashboard Site", CreateDate = baseTime.AddDays(-90), Contracts = [] },
            new Site { Id = newSiteId, SiteName = "New Dashboard Site", CreateDate = baseTime.AddDays(-90), Contracts = [] },
            new Contract
            {
                Id = oldContractId,
                ContractNumber = "DASH-OLD",
                CompanyId = oldCompanyId,
                SiteiD = oldSiteId,
                OnHireDate = baseTime.AddDays(-30),
                OffHireDate = oldDeploymentEnd
            },
            new Contract
            {
                Id = newContractId,
                ContractNumber = "DASH-NEW",
                CompanyId = newCompanyId,
                SiteiD = newSiteId,
                OnHireDate = newDeploymentStart
            },
            Monitor(monitorId, "DASH-MOVED", "SER-DASH-MOVED", MonitorTypeEnum.Dust, baseTime),
            new Deployment { Id = oldDeploymentId, ContractId = oldContractId, MonitorId = monitorId, StartDate = baseTime.AddDays(-30), EndDate = oldDeploymentEnd },
            new Deployment
            {
                Id = newDeploymentId,
                ContractId = newContractId,
                MonitorId = monitorId,
                StartDate = newDeploymentStart,
                Lat = 51.501,
                Lng = -0.141
            },
            new Notification
            {
                Id = Guid.NewGuid(),
                MonitorId = monitorId,
                NotificationTime = gapNotificationTime,
                AlertType = AlertTypeEnum.Alert,
                AlertField = "pm10",
                LimitOn = 50,
                Level = 66,
                AveragingPeriod = (int)AveragingPeriodsDustEnum._1_hour
            });

        return new MovedMonitorDashboardIds(newCompanyId, newSiteId, monitorId, gapNotificationTime);
    }

    // Function summary: Handles the assign user to site workflow for this module.
    private static async Task AssignUserToSiteAsync(SpaTestApplicationFactory factory, string userId, Guid siteId)
    {
        await factory.SeedDomainEntitiesAsync(TestData.SiteUser(siteId: siteId, userId: Guid.Parse(userId), startDate: DateTime.UtcNow.AddDays(-1)));
    }

    // Function summary: Handles the monitor workflow for this module.
    private static RVT.Entities.Monitor Monitor(Guid id, string fleetNumber, string serialId, MonitorTypeEnum type, DateTime today)
    {
        var monitor = TestData.Monitor(type, id: id, fleetNr: fleetNumber, serialId: serialId);
        monitor.ListedAtTime = today.AddDays(-90);
        monitor.LastDataTime15Min = DateTime.UtcNow.AddMinutes(-10);
        return monitor;
    }

    // Function summary: Creates client data for the current workflow.
    private static HttpClient CreateClient(SpaTestApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    // Function summary: Verifies floating-point API values with a small tolerance.
    private static void AssertApproximately(double expected, double? actual)
    {
        Assert.NotNull(actual);
        Assert.InRange(actual.Value, expected - 0.000001, expected + 0.000001);
    }

    // Function summary: Handles the login workflow for this module.
    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        return client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password,
            RememberMe = true
        });
    }

    // Function summary: Handles the dashboard scenario ids workflow for this module.
    private sealed record DashboardScenarioIds(
        Guid CompanyId,
        Guid SiteId,
        Guid OtherSiteId,
        Guid MonitorId,
        Guid OtherMonitorId,
        Guid VibrationMonitorId,
        Guid DeploymentId,
        Guid OtherDeploymentId,
        Guid AlertNotificationId,
        DateTime Today);

    // Function summary: Carries ids for moved-monitor dashboard boundary regressions.
    private sealed record MovedMonitorDashboardIds(
        Guid NewCompanyId,
        Guid NewSiteId,
        Guid MonitorId,
        DateTime GapNotificationTime);
}
