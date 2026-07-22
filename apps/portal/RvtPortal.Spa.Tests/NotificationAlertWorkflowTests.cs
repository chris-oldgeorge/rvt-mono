// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-07-22 pending Covered inactive and exact-boundary alert-level and notification-close authorization.
// - 2026-06-26 pending Added moved-monitor contract-window notification isolation regressions.
// - 2026-06-26 pending Added RC-grade company-user alert close authorization scenario coverage.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public class NotificationAlertWorkflowTests
{
    private const string AdminEmail = "alerts.admin@rvt.test";
    private const string CompanyUserEmail = "alerts.company@rvt.test";
    private const string Password = "P8sSw0rd9$";

    // The dust monitor that raises the alert notification; seeded once and asserted by detail tests.
    private const string AlertMonitorFleetNumber = "P6-ALERT";

    [Fact]
    // Function summary: Handles the notification lists are filtered and scoped workflow for this module.
    public async Task NotificationLists_AreFilteredAndScoped()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedNotificationAlertScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: ids.CompanyId);
        await factory.SeedDomainEntitiesAsync(TestData.SiteUser(siteId: ids.SiteId, userId: Guid.Parse(companyUser.Id), startDate: DateTime.UtcNow.AddDays(-10)));

        var adminClient = CreateClient(factory);
        await LoginAsync(adminClient, AdminEmail, Password);
        var open = await adminClient.GetFromJsonAsync<QueryNotificationsResponse>("/api/notifications?state=open");
        var cautions = await adminClient.GetFromJsonAsync<QueryNotificationsResponse>("/api/notifications?state=cautions");
        var siteScoped = await adminClient.GetFromJsonAsync<QueryNotificationsResponse>($"/api/notifications?siteId={ids.SiteId}");

        var companyClient = CreateClient(factory);
        await LoginAsync(companyClient, CompanyUserEmail, Password);
        var companyOpen = await companyClient.GetFromJsonAsync<QueryNotificationsResponse>("/api/notifications?state=open");
        var hiddenOther = await companyClient.GetAsync($"/api/notifications/{ids.OtherAlertNotificationId}");

        Assert.Contains(open!.Results, notification => notification.Id == ids.AlertNotificationId);
        Assert.Contains(open.Results, notification => notification.Id == ids.OtherAlertNotificationId);
        Assert.Single(cautions!.Results);
        Assert.Equal(ids.CautionNotificationId, cautions.Results[0].Id);
        Assert.Equal(2, siteScoped!.Results.Count);
        Assert.All(companyOpen!.Results, notification => Assert.Equal(ids.SiteId, notification.SiteId));
        Assert.DoesNotContain(companyOpen.Results, notification => notification.Id == ids.OtherAlertNotificationId);
        Assert.Equal(HttpStatusCode.NotFound, hiddenOther.StatusCode);
    }

    [Fact]
    // Function summary: Handles the notification detail close and batch close update visible alerts only workflow for this module.
    public async Task NotificationDetailCloseAndBatchClose_UpdateVisibleAlertsOnly()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedNotificationAlertScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        const string closeNote = "Investigated from SPA";

        var detail = await client.GetFromJsonAsync<EntityResponse<NotificationDetailResponse>>($"/api/notifications/{ids.AlertNotificationId}");
        var close = await client.PostAsJsonAsync($"/api/notifications/{ids.AlertNotificationId}/close", new NotificationCloseRequest
        {
            Note = closeNote
        });
        var closed = await close.Content.ReadFromJsonAsync<EntityResponse<NotificationDetailResponse>>();
        var batch = await client.PostAsJsonAsync("/api/notifications/batch-close", new NotificationBatchCloseRequest
        {
            NotificationIds = [ids.OtherAlertNotificationId, ids.CautionNotificationId, Guid.NewGuid()],
            Note = "Batch close"
        });
        var batchResult = await batch.Content.ReadFromJsonAsync<NotificationBatchCloseResponse>();

        Assert.Equal(ids.AlertNotificationId, detail?.Item?.Id);
        Assert.Equal(ids.MonitorId, detail?.Item?.MonitorId);
        Assert.Equal(AlertMonitorFleetNumber, detail?.Item?.FleetNumber);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Equal(closeNote, closed?.Item?.ClosedNote);
        Assert.NotNull(closed?.Item?.ClosedTime);
        Assert.Equal(HttpStatusCode.OK, batch.StatusCode);
        Assert.Contains(ids.OtherAlertNotificationId, batchResult!.ClosedIds);
        Assert.Contains(ids.CautionNotificationId, batchResult.InvalidIds);
        Assert.Single(batchResult.NotFoundIds);
    }

    [Fact]
    // Function summary: Verifies company users can close their own site alerts but not another site's alert.
    public async Task CompanyUserCloseAlert_RecordsNoteAndRejectsOtherSiteAlerts()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedNotificationAlertScenarioAsync(factory);
        var companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: ids.CompanyId);
        await factory.SeedDomainEntitiesAsync(TestData.SiteUser(siteId: ids.SiteId, userId: Guid.Parse(companyUser.Id), startDate: DateTime.UtcNow.AddDays(-10)));
        var client = CreateClient(factory);
        await LoginAsync(client, CompanyUserEmail, Password);

        const string closeNote = "Called site contact and confirmed dust suppression restarted.";

        var close = await client.PostAsJsonAsync($"/api/notifications/{ids.AlertNotificationId}/close", new NotificationCloseRequest
        {
            Note = closeNote
        });
        var closed = await close.Content.ReadFromJsonAsync<EntityResponse<NotificationDetailResponse>>();
        var otherSiteBatch = await client.PostAsJsonAsync("/api/notifications/batch-close", new NotificationBatchCloseRequest
        {
            NotificationIds = [ids.OtherAlertNotificationId],
            Note = "Not my site"
        });
        var batchResult = await otherSiteBatch.Content.ReadFromJsonAsync<NotificationBatchCloseResponse>();

        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Equal(closeNote, closed?.Item?.ClosedNote);
        Assert.NotNull(closed?.Item?.ClosedTime);
        Assert.Equal(HttpStatusCode.OK, otherSiteBatch.StatusCode);
        Assert.Empty(batchResult!.ClosedIds);
        Assert.Contains(ids.OtherAlertNotificationId, batchResult.ForbiddenIds);
    }

    [Fact]
    // Function summary: Verifies alert-level reads reject inactive assignments and preserve the exact inclusive boundary.
    public async Task CompanyUserAlertLevels_RequireActiveAssignmentWindow()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedNotificationAlertScenarioAsync(factory);
        var futureUser = await SeedCompanyUserAsync(factory, "alerts.future@rvt.test", ids.CompanyId);
        var expiredUser = await SeedCompanyUserAsync(factory, "alerts.expired@rvt.test", ids.CompanyId);
        var boundaryUser = await SeedCompanyUserAsync(factory, "alerts.boundary@rvt.test", ids.CompanyId);
        await factory.SeedDomainEntitiesAsync(
            Assignment(ids.SiteId, futureUser.Id, nowUtc.UtcDateTime.AddTicks(1)),
            Assignment(ids.SiteId, expiredUser.Id, nowUtc.UtcDateTime.AddDays(-1), nowUtc.UtcDateTime.AddTicks(-1)),
            Assignment(ids.SiteId, boundaryUser.Id, nowUtc.UtcDateTime, nowUtc.UtcDateTime));

        using var fixedTimeFactory = WithTimeProvider(factory, nowUtc);
        var futureClient = CreateClient(fixedTimeFactory);
        await LoginAsync(futureClient, "alerts.future@rvt.test", Password);
        var futureResponse = await futureClient.GetAsync($"/api/alert-levels?monitorId={ids.MonitorId}");

        var expiredClient = CreateClient(fixedTimeFactory);
        await LoginAsync(expiredClient, "alerts.expired@rvt.test", Password);
        var expiredResponse = await expiredClient.GetAsync($"/api/alert-levels?monitorId={ids.MonitorId}");

        var boundaryClient = CreateClient(fixedTimeFactory);
        await LoginAsync(boundaryClient, "alerts.boundary@rvt.test", Password);
        var boundaryResponse = await boundaryClient.GetAsync($"/api/alert-levels?monitorId={ids.MonitorId}");

        Assert.Equal(HttpStatusCode.NotFound, futureResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, expiredResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, boundaryResponse.StatusCode);
    }

    [Fact]
    // Function summary: Verifies inactive assignments cannot close notifications while the exact boundary remains authorized.
    public async Task CompanyUserNotificationClose_RequiresActiveAssignmentWindow()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedNotificationAlertScenarioAsync(factory);
        var futureNotificationId = ids.AlertNotificationId;
        var expiredNotificationId = Guid.NewGuid();
        var boundaryNotificationId = Guid.NewGuid();
        var futureUser = await SeedCompanyUserAsync(factory, "close.future@rvt.test", ids.CompanyId);
        var expiredUser = await SeedCompanyUserAsync(factory, "close.expired@rvt.test", ids.CompanyId);
        var boundaryUser = await SeedCompanyUserAsync(factory, "close.boundary@rvt.test", ids.CompanyId);
        await factory.SeedDomainEntitiesAsync(
            Assignment(ids.SiteId, futureUser.Id, nowUtc.UtcDateTime.AddTicks(1)),
            Assignment(ids.SiteId, expiredUser.Id, nowUtc.UtcDateTime.AddDays(-1), nowUtc.UtcDateTime.AddTicks(-1)),
            Assignment(ids.SiteId, boundaryUser.Id, nowUtc.UtcDateTime, nowUtc.UtcDateTime),
            AlertNotification(expiredNotificationId, ids.MonitorId, nowUtc.UtcDateTime.AddMinutes(-20)),
            AlertNotification(boundaryNotificationId, ids.MonitorId, nowUtc.UtcDateTime.AddMinutes(-10)));

        using var fixedTimeFactory = WithTimeProvider(factory, nowUtc);
        var futureClient = CreateClient(fixedTimeFactory);
        await LoginAsync(futureClient, "close.future@rvt.test", Password);
        var futureClose = await CloseAsync(futureClient, futureNotificationId, "future assignment");

        var expiredClient = CreateClient(fixedTimeFactory);
        await LoginAsync(expiredClient, "close.expired@rvt.test", Password);
        var expiredClose = await CloseAsync(expiredClient, expiredNotificationId, "expired assignment");

        var boundaryClient = CreateClient(fixedTimeFactory);
        await LoginAsync(boundaryClient, "close.boundary@rvt.test", Password);
        var boundaryClose = await CloseAsync(boundaryClient, boundaryNotificationId, "boundary assignment");

        Assert.Equal(HttpStatusCode.NotFound, futureClose.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, expiredClose.StatusCode);
        Assert.Equal(HttpStatusCode.OK, boundaryClose.StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        Assert.Null(context.Notifications.Single(item => item.Id == futureNotificationId).ClosedTime);
        Assert.Null(context.Notifications.Single(item => item.Id == expiredNotificationId).ClosedTime);
        Assert.NotNull(context.Notifications.Single(item => item.Id == boundaryNotificationId).ClosedTime);
    }

    [Fact]
    // Function summary: Verifies monitor notifications in a contract gap are not attributed to a later current deployment.
    public async Task MovedMonitorNotifications_DoNotFallBackToCurrentDeployment()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedMovedMonitorNotificationScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: ids.NewCompanyId);
        await factory.SeedDomainEntitiesAsync(TestData.SiteUser(siteId: ids.NewSiteId, userId: Guid.Parse(companyUser.Id), startDate: ids.NewDeploymentStart));

        var adminClient = CreateClient(factory);
        await LoginAsync(adminClient, AdminEmail, Password);
        var adminDetail = await adminClient.GetFromJsonAsync<EntityResponse<NotificationDetailResponse>>($"/api/notifications/{ids.GapNotificationId}");

        var companyClient = CreateClient(factory);
        await LoginAsync(companyClient, CompanyUserEmail, Password);
        var companyOpen = await companyClient.GetFromJsonAsync<QueryNotificationsResponse>("/api/notifications?state=open");
        var companyDetail = await companyClient.GetAsync($"/api/notifications/{ids.GapNotificationId}");
        var companyClose = await companyClient.PostAsJsonAsync($"/api/notifications/{ids.GapNotificationId}/close", new NotificationCloseRequest
        {
            Note = "Should not be allowed"
        });

        Assert.Equal(ids.GapNotificationId, adminDetail?.Item?.Id);
        Assert.Null(adminDetail?.Item?.DeploymentId);
        Assert.Null(adminDetail?.Item?.SiteId);
        Assert.DoesNotContain(companyOpen!.Results, notification => notification.Id == ids.GapNotificationId);
        Assert.Equal(HttpStatusCode.NotFound, companyDetail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, companyClose.StatusCode);
    }

    [Fact]
    // Function summary: Handles the alert level crud validates creates updates and deletes dust noise levels workflow for this module.
    public async Task AlertLevelCrud_ValidatesCreatesUpdatesAndDeletesDustNoiseLevels()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedNotificationAlertScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var invalid = await client.PostAsJsonAsync("/api/alert-levels", new AlertLevelMutationRequest
        {
            MonitorId = ids.MonitorId,
            AlertField = "pm10",
            AlertType = AlertTypeEnum.Alert.ToString(),
            AveragingPeriod = (int)AveragingPeriodsDustEnum._1_hour,
            LimitOn = 10,
            LimitOff = 12,
            Weekdays = true,
            Saturdays = false,
            Sundays = false
        });
        var create = await client.PostAsJsonAsync("/api/alert-levels", new AlertLevelMutationRequest
        {
            MonitorId = ids.MonitorId,
            AlertField = "pm10",
            AlertType = AlertTypeEnum.Alert.ToString(),
            AveragingPeriod = (int)AveragingPeriodsDustEnum._1_hour,
            LimitOn = 55,
            LimitOff = 50,
            Weekdays = true,
            Saturdays = false,
            Sundays = false
        });
        var created = await create.Content.ReadFromJsonAsync<EntityResponse<AlertLevelItem>>();
        var update = await client.PutAsJsonAsync($"/api/alert-levels/{created!.Item!.Id}", new AlertLevelMutationRequest
        {
            MonitorId = ids.MonitorId,
            AlertField = "pm2.5",
            AlertType = AlertTypeEnum.Caution.ToString(),
            AveragingPeriod = (int)AveragingPeriodsDustEnum._15_min,
            LimitOn = 25,
            LimitOff = 20,
            Weekdays = true,
            Saturdays = true,
            Sundays = false
        });
        var updated = await update.Content.ReadFromJsonAsync<EntityResponse<AlertLevelItem>>();
        var delete = await client.DeleteAsync($"/api/alert-levels/{created.Item.Id}");
        var list = await client.GetFromJsonAsync<QueryAlertLevelsResponse>($"/api/alert-levels?monitorId={ids.MonitorId}");

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal("pm10", created.Item.AlertField);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("pm2.5", updated?.Item?.AlertField);
        Assert.Equal("Caution", updated?.Item?.AlertType);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.DoesNotContain(list!.Results, level => level.Id == created.Item.Id);
    }

    [Fact]
    // Function summary: Handles the vibration alert level endpoint upserts alert and caution pair workflow for this module.
    public async Task VibrationAlertLevelEndpoint_UpsertsAlertAndCautionPair()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedNotificationAlertScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var invalid = await client.PutAsJsonAsync($"/api/alert-levels/monitors/{ids.VibrationMonitorId}/vibration", new VibrationAlertLevelMutationRequest
        {
            AlertLevel = 5,
            CautionLevel = 6
        });
        const double createAlertLevel = 12;
        const double updatedAlertLevel = 14;
        const double updatedCautionLevel = 9;

        var create = await client.PutAsJsonAsync($"/api/alert-levels/monitors/{ids.VibrationMonitorId}/vibration", new VibrationAlertLevelMutationRequest
        {
            AlertLevel = createAlertLevel,
            CautionLevel = 8
        });
        var created = await create.Content.ReadFromJsonAsync<VibrationAlertLevelResponse>();
        var update = await client.PutAsJsonAsync($"/api/alert-levels/monitors/{ids.VibrationMonitorId}/vibration", new VibrationAlertLevelMutationRequest
        {
            AlertLevel = updatedAlertLevel,
            CautionLevel = updatedCautionLevel
        });
        var updated = await update.Content.ReadFromJsonAsync<VibrationAlertLevelResponse>();
        var list = await client.GetFromJsonAsync<QueryAlertLevelsResponse>($"/api/alert-levels?monitorId={ids.VibrationMonitorId}");

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.False(created!.ExternalSyncAttempted);
        Assert.Equal(2, created.AlertLevels.Count);
        Assert.All(created.AlertLevels, level => Assert.Equal("", level.AveragingPeriodLabel));
        AssertApproximately(createAlertLevel, Assert.Single(created.AlertLevels, level => level.AlertType == "Alert").LimitOn);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(2, updated!.AlertLevels.Count);
        Assert.All(updated.AlertLevels, level => Assert.Equal("", level.AveragingPeriodLabel));
        Assert.Empty(list!.Options.AveragingPeriods);
        Assert.All(list.Results, level => Assert.Equal("", level.AveragingPeriodLabel));
        AssertApproximately(updatedAlertLevel, Assert.Single(updated.AlertLevels, level => level.AlertType == "Alert").LimitOn);
        AssertApproximately(updatedCautionLevel, Assert.Single(updated.AlertLevels, level => level.AlertType == "Caution").LimitOn);
    }

    // Function summary: Verifies floating-point API values with a small tolerance.
    private static void AssertApproximately(double expected, double actual)
    {
        Assert.InRange(actual, expected - 0.000001, expected + 0.000001);
    }

    // Function summary: Initializes notification alert scenario state required by the application.
    private static async Task<NotificationAlertIds> SeedNotificationAlertScenarioAsync(SpaTestApplicationFactory factory)
    {
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var otherSiteId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var otherContractId = Guid.NewGuid();
        var monitorId = Guid.NewGuid();
        var noiseMonitorId = Guid.NewGuid();
        var vibrationMonitorId = Guid.NewGuid();
        var otherMonitorId = Guid.NewGuid();
        var alertNotificationId = Guid.NewGuid();
        var cautionNotificationId = Guid.NewGuid();
        var otherAlertNotificationId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Notification Company", Contracts = [] },
            new Company { Id = otherCompanyId, CompanyName = "Other Notification Company", Contracts = [] },
            new Site { Id = siteId, SiteName = "Notification Site", CreateDate = now.AddDays(-20), Contracts = [] },
            new Site { Id = otherSiteId, SiteName = "Other Notification Site", CreateDate = now.AddDays(-20), Contracts = [] },
            new Contract { Id = contractId, ContractNumber = "P6-CON-001", CompanyId = companyId, SiteiD = siteId, OnHireDate = now.Date },
            new Contract { Id = otherContractId, ContractNumber = "P6-CON-002", CompanyId = otherCompanyId, SiteiD = otherSiteId, OnHireDate = now.Date },
            Monitor(monitorId, AlertMonitorFleetNumber, "SER-P6-A", MonitorTypeEnum.Dust, now),
            Monitor(noiseMonitorId, "P6-NOISE", "SER-P6-N", MonitorTypeEnum.Noise, now),
            Monitor(vibrationMonitorId, "P6-VIBE", "SER-P6-V", MonitorTypeEnum.Vibration, now),
            Monitor(otherMonitorId, "P6-OTHER", "SER-P6-O", MonitorTypeEnum.Noise, now),
            new Deployment { Id = Guid.NewGuid(), ContractId = contractId, MonitorId = monitorId, StartDate = now.AddDays(-4) },
            new Deployment { Id = Guid.NewGuid(), ContractId = contractId, MonitorId = noiseMonitorId, StartDate = now.AddDays(-4) },
            new Deployment { Id = Guid.NewGuid(), ContractId = contractId, MonitorId = vibrationMonitorId, StartDate = now.AddDays(-4) },
            new Deployment { Id = Guid.NewGuid(), ContractId = otherContractId, MonitorId = otherMonitorId, StartDate = now.AddDays(-4) },
            new Notification
            {
                Id = alertNotificationId,
                MonitorId = monitorId,
                NotificationTime = now.AddMinutes(-30),
                AlertType = AlertTypeEnum.Alert,
                AlertField = "pm10",
                LimitOn = 45,
                Level = 49,
                AveragingPeriod = (int)AveragingPeriodsDustEnum._1_hour
            },
            new Notification
            {
                Id = cautionNotificationId,
                MonitorId = noiseMonitorId,
                NotificationTime = now.AddMinutes(-20),
                AlertType = AlertTypeEnum.Caution,
                AlertField = "LAeq",
                LimitOn = 70,
                Level = 72,
                AveragingPeriod = (int)AveragingPeriodsNoiseEnum._15_min
            },
            new Notification
            {
                Id = otherAlertNotificationId,
                MonitorId = otherMonitorId,
                NotificationTime = now.AddMinutes(-10),
                AlertType = AlertTypeEnum.Alert,
                AlertField = "LAeq",
                LimitOn = 75,
                Level = 80,
                AveragingPeriod = (int)AveragingPeriodsNoiseEnum._15_min
            });

        return new NotificationAlertIds(companyId, siteId, monitorId, vibrationMonitorId, alertNotificationId, cautionNotificationId, otherAlertNotificationId);
    }

    // Function summary: Seeds a monitor that moved sites with a notification outside any valid ownership window.
    private static async Task<MovedMonitorNotificationIds> SeedMovedMonitorNotificationScenarioAsync(SpaTestApplicationFactory factory)
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
        var gapNotificationId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
        var oldDeploymentEnd = baseTime.AddDays(-10);
        var newDeploymentStart = baseTime.AddDays(-4);
        var gapNotificationTime = baseTime.AddDays(-7);

        await factory.SeedDomainEntitiesAsync(
            new Company { Id = oldCompanyId, CompanyName = "Old Owner Company", Contracts = [] },
            new Company { Id = newCompanyId, CompanyName = "New Owner Company", Contracts = [] },
            new Site { Id = oldSiteId, SiteName = "Old Owner Site", CreateDate = baseTime.AddDays(-90), Contracts = [] },
            new Site { Id = newSiteId, SiteName = "New Owner Site", CreateDate = baseTime.AddDays(-90), Contracts = [] },
            new Contract
            {
                Id = oldContractId,
                ContractNumber = "MOVE-OLD",
                CompanyId = oldCompanyId,
                SiteiD = oldSiteId,
                OnHireDate = baseTime.AddDays(-30),
                OffHireDate = oldDeploymentEnd
            },
            new Contract
            {
                Id = newContractId,
                ContractNumber = "MOVE-NEW",
                CompanyId = newCompanyId,
                SiteiD = newSiteId,
                OnHireDate = newDeploymentStart
            },
            Monitor(monitorId, "MOVE-001", "SER-MOVED-001", MonitorTypeEnum.Dust, baseTime),
            new Deployment { Id = oldDeploymentId, ContractId = oldContractId, MonitorId = monitorId, StartDate = baseTime.AddDays(-30), EndDate = oldDeploymentEnd },
            new Deployment { Id = newDeploymentId, ContractId = newContractId, MonitorId = monitorId, StartDate = newDeploymentStart },
            new Notification
            {
                Id = gapNotificationId,
                MonitorId = monitorId,
                NotificationTime = gapNotificationTime,
                AlertType = AlertTypeEnum.Alert,
                AlertField = "pm10",
                LimitOn = 45,
                Level = 55,
                AveragingPeriod = (int)AveragingPeriodsDustEnum._1_hour
            });

        return new MovedMonitorNotificationIds(newCompanyId, newSiteId, gapNotificationId, newDeploymentStart);
    }

    // Function summary: Handles the monitor workflow for this module.
    private static RVT.Entities.Monitor Monitor(Guid id, string fleetNumber, string serialId, MonitorTypeEnum type, DateTime now)
    {
        var monitor = TestData.Monitor(type, id: id, fleetNr: fleetNumber, serialId: serialId);
        monitor.ListedAtTime = now.AddDays(-30);
        monitor.LastDataTime15Min = now.AddMinutes(-5);
        return monitor;
    }

    // Function summary: Creates client data for the current workflow.
    private static Task<ApplicationUser> SeedCompanyUserAsync(
        SpaTestApplicationFactory factory,
        string email,
        Guid companyId)
    {
        return factory.SeedUserAsync(email, Password, RoleNames.CompanyUser, companyId: companyId);
    }

    // Function summary: Creates one assignment with caller-controlled inclusive window boundaries.
    private static SiteUsers Assignment(Guid siteId, string userId, DateTime startDate, DateTime? endDate = null)
    {
        return new SiteUsers
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            UserId = Guid.Parse(userId),
            StartDate = startDate,
            EndDate = endDate
        };
    }

    // Function summary: Creates an open alert notification for close-authorization coverage.
    private static Notification AlertNotification(Guid id, Guid monitorId, DateTime notificationTime)
    {
        return new Notification
        {
            Id = id,
            MonitorId = monitorId,
            NotificationTime = notificationTime,
            AlertType = AlertTypeEnum.Alert,
            AlertField = "pm10",
            LimitOn = 45,
            Level = 50,
            AveragingPeriod = (int)AveragingPeriodsDustEnum._1_hour
        };
    }

    // Function summary: Sends one notification close request through the real API boundary.
    private static Task<HttpResponseMessage> CloseAsync(HttpClient client, Guid notificationId, string note)
    {
        return client.PostAsJsonAsync($"/api/notifications/{notificationId}/close", new NotificationCloseRequest { Note = note });
    }

    // Function summary: Replaces the system clock for one test host.
    private static WebApplicationFactory<Program> WithTimeProvider(
        SpaTestApplicationFactory factory,
        DateTimeOffset nowUtc)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(nowUtc));
            });
        });
    }

    // Function summary: Creates client data for the current workflow.
    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
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

    // Function summary: Supplies a deterministic UTC clock for assignment-window authorization tests.
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    // Function summary: Handles the notification alert ids workflow for this module.
    private sealed record NotificationAlertIds(
        Guid CompanyId,
        Guid SiteId,
        Guid MonitorId,
        Guid VibrationMonitorId,
        Guid AlertNotificationId,
        Guid CautionNotificationId,
        Guid OtherAlertNotificationId);

    // Function summary: Carries ids for moved-monitor notification boundary regressions.
    private sealed record MovedMonitorNotificationIds(
        Guid NewCompanyId,
        Guid NewSiteId,
        Guid GapNotificationId,
        DateTime NewDeploymentStart);
}
