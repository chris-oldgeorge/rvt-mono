// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-07-22 pending Covered shared-site contract isolation in installer monitor options.
// - 2026-06-26 pending Added moved-monitor monitor list/detail ownership-window regressions.
// - 2026-06-26 pending Added latest-reading request ownership-window coverage.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-09 pending Added legacy monitor-detail parity coverage for deployment, picture, and latest reading summaries.
// - 2026-06-09 pending Covered monitor picture upload and coordinate validation quality fixes.
// - 2026-06-09 pending Covered latest average and battery monitor-detail parity data.
// - 2026-06-09 pending Covered installer detail metric parity and protected legacy picture paths.
// - 2026-06-26 pending Covered installer monitor and deployment access scoped to the installer company.
// - 2026-07-09 pending Added installer what3words configuration behavior coverage.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RVT.BusinessLogic;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public class MonitorWorkflowTests
{
    private const string AdminEmail = "monitor.admin@rvt.test";
    private const string CompanyUserEmail = "monitor.company@rvt.test";
    private const string InstallerEmail = "monitor.installer@rvt.test";
    private const string Password = "P8sSw0rd9$";

    // Values seeded once by SeedMonitorScenarioAsync and asserted by the monitor-detail/edit tests, so
    // the seed and the assertions read from a single named source rather than repeating literals.
    private const string CompanyName = "Monitor Company";
    private const string SiteName = "Monitor Site";
    private const string ContractNumber = "MON-CON-001";
    private const string OnlineFleetNumber = "MON-ONLINE";
    private const string LatestAlertField = "pm10";
    private const double LatestAlertLevel = 48;
    private const double OnlineDeploymentLat = 51.5;
    private const double OnlineDeploymentLng = -0.12;
    private const string OnlineDeploymentLocation = "North boundary";
    private const string OnlineDeploymentWhat3Words = "filled.count.soap";

    [Fact]
    // Function summary: Handles the monitor inventory states are filtered by state and role workflow for this module.
    public async Task MonitorInventoryStates_AreFilteredByStateAndRole()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        ApplicationUser companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: ids.CompanyId);
        await factory.SeedUserAsync(InstallerEmail, Password, RoleNames.RVTInstaller, companyId: ids.CompanyId);
        await factory.SeedDomainEntitiesAsync(TestData.SiteUser(siteId: ids.SiteId, userId: Guid.Parse(companyUser.Id), startDate: DateTime.UtcNow.AddDays(-10)));

        HttpClient adminClient = CreateClient(factory);
        await LoginAsync(adminClient, AdminEmail, Password);
        QueryMonitorsResponse? newList = await adminClient.GetFromJsonAsync<QueryMonitorsResponse>("/api/monitors?state=new");
        QueryMonitorsResponse? notInUse = await adminClient.GetFromJsonAsync<QueryMonitorsResponse>("/api/monitors?state=not-in-use");
        QueryMonitorsResponse? online = await adminClient.GetFromJsonAsync<QueryMonitorsResponse>("/api/monitors?state=online");
        QueryMonitorsResponse? offline = await adminClient.GetFromJsonAsync<QueryMonitorsResponse>("/api/monitors?state=offline");

        HttpClient companyClient = CreateClient(factory);
        await LoginAsync(companyClient, CompanyUserEmail, Password);
        QueryMonitorsResponse? scoped = await companyClient.GetFromJsonAsync<QueryMonitorsResponse>("/api/monitors?state=all");
        HttpResponseMessage forbiddenNew = await companyClient.GetAsync("/api/monitors?state=new");

        HttpClient installerClient = CreateClient(factory);
        await LoginAsync(installerClient, InstallerEmail, Password);
        QueryMonitorsResponse? installerList = await installerClient.GetFromJsonAsync<QueryMonitorsResponse>("/api/installer/monitors");

        Assert.Single(newList!.Results);
        Assert.Equal(ids.NewMonitorId, newList.Results[0].Id);
        Assert.Single(notInUse!.Results);
        Assert.Equal(ids.AvailableMonitorId, notInUse.Results[0].Id);
        Assert.Contains(online!.Results, monitor => monitor.Id == ids.OnlineMonitorId);
        Assert.Contains(offline!.Results, monitor => monitor.Id == ids.OfflineMonitorId);
        Assert.Equal(2, scoped!.Results.Count);
        Assert.All(scoped.Results, monitor => Assert.Equal(ids.SiteId, monitor.SiteId));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenNew.StatusCode);
        Assert.Equal(2, installerList!.Results.Count);
        Assert.All(installerList.Results, monitor => Assert.True(monitor.CanInstallerEdit));
        Assert.DoesNotContain(installerList.Results, monitor => monitor.Id == ids.OtherMonitorId);
    }

    [Fact]
    // Function summary: Handles the fleet and monitor edit validate duplicates and create default alert levels workflow for this module.
    public async Task FleetAndMonitorEdit_ValidateDuplicatesAndCreateDefaultAlertLevels()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        const string newFleetNumber = "MON-NEW";
        const string editedFleetNumber = "MON-ONLINE-EDIT";

        // Reusing an existing monitor's fleet number is rejected as a duplicate.
        HttpResponseMessage duplicate = await client.PutAsJsonAsync($"/api/monitors/{ids.NewMonitorId}/fleet-number", new FleetNumberMutationRequest
        {
            FleetNumber = OnlineFleetNumber
        });
        HttpResponseMessage assignFleet = await client.PutAsJsonAsync($"/api/monitors/{ids.NewMonitorId}/fleet-number", new FleetNumberMutationRequest
        {
            FleetNumber = newFleetNumber
        });
        EntityResponse<MonitorDetailResponse>? assignedFleet = await assignFleet.Content.ReadFromJsonAsync<EntityResponse<MonitorDetailResponse>>();

        MonitorMutationRequest editRequest = new MonitorMutationRequest
        {
            FleetNumber = editedFleetNumber,
            CalibrationDate = new DateTime(2026, 5, 1),
            CalibrationDue = new DateTime(2027, 5, 1),
            DeploymentId = ids.OnlineDeploymentId,
            Location = OnlineDeploymentLocation,
            What3words = OnlineDeploymentWhat3Words,
            Lat = OnlineDeploymentLat,
            Lng = OnlineDeploymentLng
        };
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/monitors/{ids.OnlineMonitorId}", editRequest);
        EntityResponse<MonitorDetailResponse>? updated = await update.Content.ReadFromJsonAsync<EntityResponse<MonitorDetailResponse>>();

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, assignFleet.StatusCode);
        Assert.Equal(newFleetNumber, assignedFleet?.Item?.FleetNumber);
        Assert.Equal(2, assignedFleet?.Item?.AlertLevels.Count);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(editRequest.FleetNumber, updated?.Item?.FleetNumber);
        Assert.Equal(editRequest.Location, updated?.Item?.Location);
        Assert.Equal(editRequest.What3words, updated?.Item?.What3words);
    }

    [Fact]
    // Function summary: Verifies monitor detail exposes legacy summary data used by the React detail page.
    public async Task MonitorDetail_ExposesLegacySummaryData()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        EntityResponse<MonitorDetailResponse>? detail = await client.GetFromJsonAsync<EntityResponse<MonitorDetailResponse>>($"/api/monitors/{ids.OnlineMonitorId}");

        Assert.Equal(ids.OnlineDeploymentId, detail?.Item?.DeploymentId);
        Assert.Equal(ContractNumber, detail?.Item?.DeploymentSummary?.ContractNumber);
        Assert.Equal(SiteName, detail?.Item?.DeploymentSummary?.SiteName);
        Assert.Equal(CompanyName, detail?.Item?.DeploymentSummary?.CompanyName);
        Assert.Equal("No notes for this monitor", detail?.Item?.MonitorNotes);
        Assert.Equal("Latest Breach", detail?.Item?.LatestReading?.Label);
        Assert.Equal(LatestAlertField, detail?.Item?.LatestReading?.Field);
        Assert.Equal(LatestAlertLevel, detail?.Item?.LatestReading?.Value);
        Assert.Equal($"/api/monitors/{ids.OnlineMonitorId}/picture", detail?.Item?.PictureLink);
        Assert.Equal(ids.OnlineMonitorId, detail?.Item?.RecentNotifications[0].MonitorId);
    }

    [Fact]
    // Function summary: Verifies current monitor list and detail ignore notifications outside the active ownership window.
    public async Task MonitorListAndDetail_IgnoreMovedMonitorGapNotifications()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MovedMonitorDetailIds ids = await SeedMovedMonitorDetailScenarioAsync(factory);
        ApplicationUser companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: ids.NewCompanyId);
        await factory.SeedDomainEntitiesAsync(TestData.SiteUser(siteId: ids.NewSiteId, userId: Guid.Parse(companyUser.Id), startDate: ids.NewDeploymentStart));

        HttpClient client = CreateClient(factory);
        await LoginAsync(client, CompanyUserEmail, Password);

        QueryMonitorsResponse? list = await client.GetFromJsonAsync<QueryMonitorsResponse>("/api/monitors?state=all");
        EntityResponse<MonitorDetailResponse>? detail = await client.GetFromJsonAsync<EntityResponse<MonitorDetailResponse>>($"/api/monitors/{ids.MonitorId}");

        MonitorListItem monitor = Assert.Single(list!.Results, item => item.Id == ids.MonitorId);
        Assert.False(monitor.HasAlerts);
        Assert.False(detail!.Item!.HasAlerts);
        Assert.Empty(detail.Item.RecentNotifications);
        Assert.Null(detail.Item.LatestReading);
    }

    [Fact]
    // Function summary: Verifies monitor picture upload rejects monitors without a current deployment.
    public async Task MonitorPictureUpload_RequiresCurrentDeployment()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);
        using MultipartFormDataContent form = new MultipartFormDataContent();
        using ByteArrayContent image = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(image, "picture", "monitor.png");

        HttpResponseMessage response = await client.PostAsync($"/api/monitors/{ids.AvailableMonitorId}/picture", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    // Function summary: Verifies blank monitor coordinate edits keep existing deployment coordinates.
    public async Task MonitorEdit_BlankCoordinatesKeepExistingValues()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        // Blank coordinates on edit must preserve the seeded deployment coordinates.
        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/monitors/{ids.OnlineMonitorId}", new MonitorMutationRequest
        {
            FleetNumber = OnlineFleetNumber,
            DeploymentId = ids.OnlineDeploymentId,
            Location = OnlineDeploymentLocation,
            What3words = OnlineDeploymentWhat3Words,
            Lat = null,
            Lng = null
        });
        EntityResponse<MonitorDetailResponse>? updated = await response.Content.ReadFromJsonAsync<EntityResponse<MonitorDetailResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertApproximately(OnlineDeploymentLat, updated?.Item?.Lat);
        AssertApproximately(OnlineDeploymentLng, updated?.Item?.Lng);
    }

    [Fact]
    // Function summary: Verifies monitor coordinate edits reject out-of-range deployment coordinates.
    public async Task MonitorEdit_RejectsOutOfRangeCoordinates()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        // Latitude 100 is outside the valid [-90, 90] range and must be rejected.
        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/monitors/{ids.OnlineMonitorId}", new MonitorMutationRequest
        {
            FleetNumber = OnlineFleetNumber,
            DeploymentId = ids.OnlineDeploymentId,
            Lat = 100,
            Lng = OnlineDeploymentLng
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    // Function summary: Verifies monitor detail exposes latest average data from the monitor data source.
    public async Task MonitorDetail_ExposesLatestAverageFromDataSource()
    {
        FakeMonitorDataSource dataSource = new FakeMonitorDataSource();
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        using WebApplicationFactory<Program> clientFactory = WithMonitorDataSource(factory, dataSource);
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        RVT.Entities.Monitor monitor = Monitor(ids.OnlineMonitorId, "MON-ONLINE", "SER-ONLINE", MonitorTypeEnum.Dust, DateTime.UtcNow, DateTime.UtcNow);
        DateTime databaseTimestamp = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Unspecified);
        dataSource.AddDustData(ids.OnlineDeploymentId, monitor, databaseTimestamp);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(clientFactory);
        await LoginAsync(client, AdminEmail, Password);

        HttpResponseMessage response = await client.GetAsync($"/api/monitors/{ids.OnlineMonitorId}");
        JsonDocument json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        JsonElement latestAverage = json.RootElement.GetProperty("item").GetProperty("latestAverage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Latest 15 Min Average", latestAverage.GetProperty("label").GetString());
        Assert.Equal("pm10", latestAverage.GetProperty("field").GetString());
        AssertApproximately(FakeMonitorDataSource.PeakDustPm10, latestAverage.GetProperty("value").GetDouble());
        Assert.Equal("2026-07-01T08:30:00Z", latestAverage.GetProperty("sampleTime").GetString());
    }

    [Fact]
    // Function summary: Verifies latest reading prefers live measurement data over notification fallback data.
    public async Task MonitorDetail_ExposesLatestReadingFromDataSourceWhenAvailable()
    {
        FakeMonitorDataSource dataSource = new FakeMonitorDataSource();
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        using WebApplicationFactory<Program> clientFactory = WithMonitorDataSource(factory, dataSource);
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        RVT.Entities.Monitor monitor = Monitor(ids.OnlineMonitorId, "MON-ONLINE", "SER-ONLINE", MonitorTypeEnum.Dust, DateTime.UtcNow, DateTime.UtcNow);
        dataSource.AddDustData(ids.OnlineDeploymentId, monitor, DateTime.UtcNow.Date.AddHours(8));
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(clientFactory);
        await LoginAsync(client, AdminEmail, Password);

        EntityResponse<MonitorDetailResponse>? detail = await client.GetFromJsonAsync<EntityResponse<MonitorDetailResponse>>($"/api/monitors/{ids.OnlineMonitorId}");

        Assert.Equal("Latest Reading", detail?.Item?.LatestReading?.Label);
        Assert.Equal("pm10", detail?.Item?.LatestReading?.Field);
        AssertApproximately(FakeMonitorDataSource.PeakDustPm10, detail?.Item?.LatestReading?.Value);
        Assert.Equal(ids.ContractStart, dataSource.LastDeploymentRequest?.FromDate);
    }

    [Fact]
    // Function summary: Verifies monitor detail exposes latest vibration battery status when available.
    public async Task MonitorDetail_ExposesLatestBatteryStatus()
    {
        const int batteryCharge = 87;
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        await factory.SeedSearchEntitiesAsync(new OmnidotsSensor
        {
            Id = Guid.NewGuid(),
            SerialId = "SER-OFFLINE",
            Name = "MON-OFFLINE",
            Lastseen = DateTime.UtcNow.AddMinutes(-12),
            BatteryCharge = batteryCharge,
            ConnectedUsing = "4G",
            Online = true
        });
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        EntityResponse<MonitorDetailResponse>? detail = await client.GetFromJsonAsync<EntityResponse<MonitorDetailResponse>>($"/api/monitors/{ids.OfflineMonitorId}");

        Assert.Equal("Battery Charge", detail?.Item?.LatestBattery?.Label);
        Assert.Equal("batteryCharge", detail?.Item?.LatestBattery?.Field);
        Assert.Equal(batteryCharge, detail?.Item?.LatestBattery?.Value);
        Assert.Equal("%", detail?.Item?.LatestBattery?.Unit);
    }

    [Fact]
    // Function summary: Verifies installer monitor detail uses the same legacy metric summaries as the main monitor detail endpoint.
    public async Task InstallerMonitorDetail_ExposesLegacyMetricSummaries()
    {
        const int batteryCharge = 91;
        FakeMonitorDataSource dataSource = new FakeMonitorDataSource();
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        using WebApplicationFactory<Program> clientFactory = WithMonitorDataSource(factory, dataSource);
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        RVT.Entities.Monitor monitor = Monitor(ids.OnlineMonitorId, "MON-ONLINE", "SER-ONLINE", MonitorTypeEnum.Dust, DateTime.UtcNow, DateTime.UtcNow);
        dataSource.AddDustData(ids.OnlineDeploymentId, monitor, DateTime.UtcNow.Date.AddHours(8));
        await factory.SeedSearchEntitiesAsync(new OmnidotsSensor
        {
            Id = Guid.NewGuid(),
            SerialId = "SER-ONLINE",
            Name = "MON-ONLINE",
            Lastseen = DateTime.UtcNow.AddMinutes(-10),
            BatteryCharge = batteryCharge,
            ConnectedUsing = "4G",
            Online = true
        });
        await factory.SeedUserAsync(InstallerEmail, Password, RoleNames.RVTInstaller, companyId: ids.CompanyId);
        HttpClient client = CreateClient(clientFactory);
        await LoginAsync(client, InstallerEmail, Password);

        EntityResponse<MonitorDetailResponse>? detail = await client.GetFromJsonAsync<EntityResponse<MonitorDetailResponse>>($"/api/installer/monitors/{ids.OnlineMonitorId}");

        Assert.Equal("Latest Reading", detail?.Item?.LatestReading?.Label);
        Assert.Equal("pm10", detail?.Item?.LatestReading?.Field);
        Assert.Equal("Dust PM10 live reading", detail?.Item?.LatestReading?.Detail);
        Assert.Equal("Latest 15 Min Average", detail?.Item?.LatestAverage?.Label);
        Assert.Equal("Battery Charge", detail?.Item?.LatestBattery?.Label);
        Assert.Equal(batteryCharge, detail?.Item?.LatestBattery?.Value);
        Assert.Equal(ids.OnlineDeploymentId, detail?.Item?.DeploymentSummary?.DeploymentId);
    }

    [Fact]
    // Function summary: Verifies legacy monitor picture paths are not exposed through unauthenticated static file fallback routes.
    public async Task LegacyMonitorPicturePath_IsNotPubliclyServed()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await client.GetAsync("/monitor-pictures/online-monitor.jpg");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    // Function summary: Handles the contract assignment adds and removes current deployment workflow for this module.
    public async Task ContractAssignment_AddsAndRemovesCurrentDeployment()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        HttpResponseMessage assign = await client.PostAsJsonAsync($"/api/monitors/{ids.AvailableMonitorId}/contract-assignment", new MonitorAssignmentRequest
        {
            ContractId = ids.ContractId
        });
        EntityResponse<MonitorDetailResponse>? assigned = await assign.Content.ReadFromJsonAsync<EntityResponse<MonitorDetailResponse>>();
        HttpResponseMessage duplicate = await client.PostAsJsonAsync($"/api/monitors/{ids.AvailableMonitorId}/contract-assignment", new MonitorAssignmentRequest
        {
            ContractId = ids.ContractId
        });
        HttpResponseMessage remove = await client.DeleteAsync($"/api/monitors/{ids.AvailableMonitorId}/contract-assignment");

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        Assert.Equal(ids.ContractId, assigned?.Item?.ContractId);
        Assert.Equal(ids.SiteId, assigned?.Item?.SiteId);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        using IServiceScope scope = factory.Services.CreateScope();
        RVTDbContext context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        Assert.False(context.Deployments.Any(deployment => deployment.MonitorId == ids.AvailableMonitorId && deployment.EndDate == null));
    }

    [Fact]
    // Function summary: Handles the installer can update deployment and read status workflow for this module.
    public async Task InstallerCanUpdateDeploymentAndReadStatus()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        await factory.SeedUserAsync(InstallerEmail, Password, RoleNames.RVTInstaller, companyId: ids.CompanyId);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, InstallerEmail, Password);

        EntityResponse<MonitorDetailResponse>? detail = await client.GetFromJsonAsync<EntityResponse<MonitorDetailResponse>>($"/api/installer/monitors/{ids.OnlineMonitorId}");
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/installer/deployments/{ids.OnlineDeploymentId}", new InstallerDeploymentMutationRequest
        {
            Location = "Installer verified point",
            What3words = "index.home.raft",
            Lat = 52.1,
            Lng = -0.2
        });
        EntityResponse<MonitorDetailResponse>? updated = await update.Content.ReadFromJsonAsync<EntityResponse<MonitorDetailResponse>>();
        InstallerMonitorStatusResponse? status = await client.GetFromJsonAsync<InstallerMonitorStatusResponse>($"/api/installer/monitors/{ids.OnlineMonitorId}/status");

        Assert.Equal(ids.OnlineDeploymentId, detail?.Item?.DeploymentId);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Installer verified point", updated?.Item?.Location);
        Assert.False(status!.IsOffline);
        Assert.Equal("Online", status.Status);
    }

    [Fact]
    // Function summary: Verifies installers cannot address monitors or deployments outside their assigned company.
    public async Task InstallerEndpoints_AreScopedToInstallerCompany()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        await factory.SeedUserAsync(InstallerEmail, Password, RoleNames.RVTInstaller, companyId: ids.CompanyId);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, InstallerEmail, Password);

        HttpResponseMessage detail = await client.GetAsync($"/api/installer/monitors/{ids.OtherMonitorId}");
        HttpResponseMessage status = await client.GetAsync($"/api/installer/monitors/{ids.OtherMonitorId}/status");
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/installer/deployments/{ids.OtherDeploymentId}", new InstallerDeploymentMutationRequest
        {
            Location = "Cross-company overwrite",
            What3words = "blocked.access.test",
            Lat = 52.1,
            Lng = -0.2
        });

        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    // Function summary: Verifies protected monitor pictures enforce the same installer-company boundary as installer detail reads.
    public async Task InstallerMonitorPicture_IsScopedToInstallerCompany()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedUserAsync(InstallerEmail, Password, RoleNames.RVTInstaller, companyId: ids.CompanyId);

        HttpClient adminClient = CreateClient(factory);
        await LoginAsync(adminClient, AdminEmail, Password);
        HttpResponseMessage ownUpload = await UploadPictureAsync(adminClient, ids.OnlineMonitorId);
        HttpResponseMessage otherUpload = await UploadPictureAsync(adminClient, ids.OtherMonitorId);

        HttpClient installerClient = CreateClient(factory);
        await LoginAsync(installerClient, InstallerEmail, Password);
        HttpResponseMessage ownPicture = await installerClient.GetAsync($"/api/monitors/{ids.OnlineMonitorId}/picture");
        HttpResponseMessage otherPicture = await installerClient.GetAsync($"/api/monitors/{ids.OtherMonitorId}/picture");

        Assert.Equal(HttpStatusCode.OK, ownUpload.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherUpload.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownPicture.StatusCode);
        Assert.Equal("image/png", ownPicture.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, otherPicture.StatusCode);
    }

    [Fact]
    // Function summary: Verifies monitor option metadata is restricted to each non-admin actor's authorized tenant graph.
    public async Task MonitorOptions_AreScopedToInstallerCompanyAndCompanyUserSites()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        MonitorWorkflowIds ids = await SeedMonitorScenarioAsync(factory);
        ApplicationUser companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: ids.CompanyId);
        await factory.SeedUserAsync(InstallerEmail, Password, RoleNames.RVTInstaller, companyId: ids.CompanyId);
        await factory.SeedDomainEntitiesAsync(TestData.SiteUser(
            siteId: ids.SiteId,
            userId: Guid.Parse(companyUser.Id),
            startDate: DateTime.UtcNow.AddDays(-1)));

        HttpClient installerClient = CreateClient(factory);
        await LoginAsync(installerClient, InstallerEmail, Password);
        MonitorOptionsResponse? installerOptions = await installerClient.GetFromJsonAsync<MonitorOptionsResponse>("/api/monitors/options");

        HttpClient companyClient = CreateClient(factory);
        await LoginAsync(companyClient, CompanyUserEmail, Password);
        MonitorOptionsResponse? companyOptions = await companyClient.GetFromJsonAsync<MonitorOptionsResponse>("/api/monitors/options");

        Assert.Equal(ids.ContractId.ToString(), Assert.Single(installerOptions!.Contracts).Value);
        Assert.Equal(ids.SiteId.ToString(), Assert.Single(installerOptions.Sites).Value);
        Assert.Equal(ids.ContractId.ToString(), Assert.Single(companyOptions!.Contracts).Value);
        Assert.Equal(ids.SiteId.ToString(), Assert.Single(companyOptions.Sites).Value);
    }

    [Fact]
    // Function summary: Verifies an installer sees only its own company's contract when multiple companies share a site.
    public async Task InstallerMonitorOptions_DoNotLeakAnotherCompanyContractOnVisibleSite()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        Guid installerCompanyId = Guid.NewGuid();
        Guid otherCompanyId = Guid.NewGuid();
        Guid sharedSiteId = Guid.NewGuid();
        Guid installerContractId = Guid.NewGuid();
        await factory.SeedUserAsync(InstallerEmail, Password, RoleNames.RVTInstaller, companyId: installerCompanyId);
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = installerCompanyId, CompanyName = "Installer Company", Contracts = [] },
            new Company { Id = otherCompanyId, CompanyName = "Other Shared-Site Company", Contracts = [] },
            new Site { Id = sharedSiteId, SiteName = "Shared Contract Site", CreateDate = DateTime.UtcNow.AddDays(-10), Contracts = [] },
            new Contract
            {
                Id = installerContractId,
                ContractNumber = "INSTALLER-OWN",
                CompanyId = installerCompanyId,
                SiteiD = sharedSiteId,
                OnHireDate = DateTime.UtcNow.Date
            },
            new Contract
            {
                Id = Guid.NewGuid(),
                ContractNumber = "OTHER-LEAK",
                CompanyId = otherCompanyId,
                SiteiD = sharedSiteId,
                OnHireDate = DateTime.UtcNow.Date
            });

        HttpClient client = CreateClient(factory);
        await LoginAsync(client, InstallerEmail, Password);
        MonitorOptionsResponse? options = await client.GetFromJsonAsync<MonitorOptionsResponse>("/api/monitors/options");

        Assert.Equal(installerContractId.ToString(), Assert.Single(options!.Contracts).Value);
        Assert.Equal(sharedSiteId.ToString(), Assert.Single(options.Sites).Value);
    }

    [Fact]
    // Function summary: Verifies the installer what3words endpoint returns a service problem when the API key is not configured.
    public async Task InstallerWhat3WordsConvert_ReturnsServiceUnavailableWhenApiKeyMissing()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(InstallerEmail, Password, RoleNames.RVTInstaller);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, InstallerEmail, Password);

        HttpResponseMessage response = await client.GetAsync("/api/installer/what3words/convert?what3words=filled.count.soap");
        string problem = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("What3words API key is not configured", problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    // Function summary: Handles the default monitors adds default levels where missing workflow for this module.
    public async Task DefaultMonitors_AddsDefaultLevelsWhereMissing()
    {
        using SpaTestApplicationFactory factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        Guid monitorId = Guid.NewGuid();
        await factory.SeedDomainEntitiesAsync(
            TestData.Monitor(MonitorTypeEnum.Dust, id: monitorId, fleetNr: "MON-DEFAULT", serialId: "SER-DEFAULT"));

        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);
        HttpResponseMessage response = await client.PostAsync("/api/monitors/default-alert-levels", null);
        DefaultMonitorsResponse? result = await response.Content.ReadFromJsonAsync<DefaultMonitorsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, result?.Processed);
        Assert.Equal(2, result?.CreatedAlertLevels);
        Assert.Contains(monitorId, result!.MonitorIds);
    }

    // Function summary: Initializes monitor scenario state required by the application.
    private static async Task<MonitorWorkflowIds> SeedMonitorScenarioAsync(SpaTestApplicationFactory factory)
    {
        Guid companyId = Guid.NewGuid();
        Guid otherCompanyId = Guid.NewGuid();
        Guid siteId = Guid.NewGuid();
        Guid otherSiteId = Guid.NewGuid();
        Guid contractId = Guid.NewGuid();
        Guid otherContractId = Guid.NewGuid();
        Guid newMonitorId = Guid.NewGuid();
        Guid availableMonitorId = Guid.NewGuid();
        Guid onlineMonitorId = Guid.NewGuid();
        Guid offlineMonitorId = Guid.NewGuid();
        Guid otherMonitorId = Guid.NewGuid();
        Guid onlineDeploymentId = Guid.NewGuid();
        Guid offlineDeploymentId = Guid.NewGuid();
        Guid otherDeploymentId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;
        DateTime contractStart = now.Date;

        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = CompanyName, Contracts = [] },
            new Company { Id = otherCompanyId, CompanyName = "Other Monitor Company", Contracts = [] },
            new Site { Id = siteId, SiteName = SiteName, CreateDate = now.AddDays(-20), Contracts = [] },
            new Site { Id = otherSiteId, SiteName = "Other Monitor Site", CreateDate = now.AddDays(-20), Contracts = [] },
            new Contract { Id = contractId, ContractNumber = ContractNumber, CompanyId = companyId, SiteiD = siteId, OnHireDate = contractStart },
            new Contract { Id = otherContractId, ContractNumber = "MON-CON-002", CompanyId = otherCompanyId, SiteiD = otherSiteId, OnHireDate = contractStart },
            Monitor(newMonitorId, null, "SER-NEW", MonitorTypeEnum.Dust, now, null),
            Monitor(availableMonitorId, "MON-AVAILABLE", "SER-AVAILABLE", MonitorTypeEnum.Noise, now, null),
            Monitor(onlineMonitorId, OnlineFleetNumber, "SER-ONLINE", MonitorTypeEnum.Dust, now, now.AddMinutes(-5)),
            Monitor(offlineMonitorId, "MON-OFFLINE", "SER-OFFLINE", MonitorTypeEnum.Vibration, now, now.AddHours(-3)),
            Monitor(otherMonitorId, "MON-OTHER", "SER-OTHER", MonitorTypeEnum.Noise, now, now.AddMinutes(-5)),
            new Deployment
            {
                Id = onlineDeploymentId,
                ContractId = contractId,
                MonitorId = onlineMonitorId,
                StartDate = now.AddDays(-5),
                Lat = OnlineDeploymentLat,
                Lng = OnlineDeploymentLng,
                Location = OnlineDeploymentLocation,
                What3words = OnlineDeploymentWhat3Words,
                PictureLink = "/monitor-pictures/online-monitor.jpg"
            },
            new Deployment { Id = offlineDeploymentId, ContractId = contractId, MonitorId = offlineMonitorId, StartDate = now.AddDays(-5) },
            new Deployment { Id = otherDeploymentId, ContractId = otherContractId, MonitorId = otherMonitorId, StartDate = now.AddDays(-5) },
            new Notification
            {
                Id = Guid.NewGuid(),
                MonitorId = onlineMonitorId,
                NotificationTime = now.AddMinutes(-2),
                AlertType = AlertTypeEnum.Alert,
                AlertField = LatestAlertField,
                LimitOn = 45,
                Level = LatestAlertLevel,
                AveragingPeriod = (int)AveragingPeriodsDustEnum._1_hour
            });

        return new MonitorWorkflowIds(
            companyId,
            siteId,
            contractId,
            newMonitorId,
            availableMonitorId,
            onlineMonitorId,
            offlineMonitorId,
            otherMonitorId,
            onlineDeploymentId,
            offlineDeploymentId,
            otherDeploymentId,
            contractStart);
    }

    // Function summary: Seeds a moved monitor whose only notification is outside the current deployment ownership window.
    private static async Task<MovedMonitorDetailIds> SeedMovedMonitorDetailScenarioAsync(SpaTestApplicationFactory factory)
    {
        Guid oldCompanyId = Guid.NewGuid();
        Guid newCompanyId = Guid.NewGuid();
        Guid oldSiteId = Guid.NewGuid();
        Guid newSiteId = Guid.NewGuid();
        Guid oldContractId = Guid.NewGuid();
        Guid newContractId = Guid.NewGuid();
        Guid monitorId = Guid.NewGuid();
        DateTime baseTime = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
        DateTime oldDeploymentEnd = baseTime.AddDays(-10);
        DateTime newDeploymentStart = baseTime.AddDays(-4);

        await factory.SeedDomainEntitiesAsync(
            new Company { Id = oldCompanyId, CompanyName = "Old Monitor Owner", Contracts = [] },
            new Company { Id = newCompanyId, CompanyName = "New Monitor Owner", Contracts = [] },
            new Site { Id = oldSiteId, SiteName = "Old Monitor Site", CreateDate = baseTime.AddDays(-90), Contracts = [] },
            new Site { Id = newSiteId, SiteName = "New Monitor Site", CreateDate = baseTime.AddDays(-90), Contracts = [] },
            new Contract
            {
                Id = oldContractId,
                ContractNumber = "MON-MOVE-OLD",
                CompanyId = oldCompanyId,
                SiteiD = oldSiteId,
                OnHireDate = baseTime.AddDays(-30),
                OffHireDate = oldDeploymentEnd
            },
            new Contract
            {
                Id = newContractId,
                ContractNumber = "MON-MOVE-NEW",
                CompanyId = newCompanyId,
                SiteiD = newSiteId,
                OnHireDate = newDeploymentStart
            },
            Monitor(monitorId, "MON-MOVED", "SER-MON-MOVED", MonitorTypeEnum.Dust, baseTime, baseTime.AddMinutes(-5)),
            new Deployment { Id = Guid.NewGuid(), ContractId = oldContractId, MonitorId = monitorId, StartDate = baseTime.AddDays(-30), EndDate = oldDeploymentEnd },
            new Deployment { Id = Guid.NewGuid(), ContractId = newContractId, MonitorId = monitorId, StartDate = newDeploymentStart },
            new Notification
            {
                Id = Guid.NewGuid(),
                MonitorId = monitorId,
                NotificationTime = baseTime.AddDays(-7),
                AlertType = AlertTypeEnum.Alert,
                AlertField = "pm10",
                LimitOn = 45,
                Level = 58,
                AveragingPeriod = (int)AveragingPeriodsDustEnum._1_hour
            });

        return new MovedMonitorDetailIds(newCompanyId, newSiteId, monitorId, newDeploymentStart);
    }

    // Function summary: Handles the monitor workflow for this module.
    private static RVT.Entities.Monitor Monitor(
        Guid id,
        string? fleetNumber,
        string serialId,
        MonitorTypeEnum type,
        DateTime listedAt,
        DateTime? lastDataTime)
    {
        // FleetNr and LastDataTime15Min are set explicitly (not passed to TestData) because null is meaningful
        // here - a null fleet number is what makes a monitor "new", and a null last-data time makes it offline -
        // and the factory would substitute a realistic default for a null argument.
        RVT.Entities.Monitor monitor = TestData.Monitor(type, id: id, serialId: serialId);
        monitor.FleetNr = fleetNumber;
        monitor.ListedAtTime = listedAt;
        monitor.LastDataTime15Min = lastDataTime;
        return monitor;
    }

    // Function summary: Builds a test host with a supplied monitor data source.
    private static WebApplicationFactory<Program> WithMonitorDataSource(SpaTestApplicationFactory factory, IMonitorDataSource dataSource)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMonitorDataSource>();
                services.AddSingleton(dataSource);
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

    // Function summary: Uploads a minimal valid PNG to a monitor's current deployment.
    private static async Task<HttpResponseMessage> UploadPictureAsync(HttpClient client, Guid monitorId)
    {
        using MultipartFormDataContent form = new MultipartFormDataContent();
        using ByteArrayContent image = new ByteArrayContent(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/lwGfVwAAAABJRU5ErkJggg=="));
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(image, "picture", "monitor.png");
        return await client.PostAsync($"/api/monitors/{monitorId}/picture", form);
    }

    // Function summary: Handles the monitor workflow ids workflow for this module.
    private sealed record MonitorWorkflowIds(
        Guid CompanyId,
        Guid SiteId,
        Guid ContractId,
        Guid NewMonitorId,
        Guid AvailableMonitorId,
        Guid OnlineMonitorId,
        Guid OfflineMonitorId,
        Guid OtherMonitorId,
        Guid OnlineDeploymentId,
        Guid OfflineDeploymentId,
        Guid OtherDeploymentId,
        DateTime ContractStart);

    // Function summary: Carries ids for moved-monitor list/detail boundary regressions.
    private sealed record MovedMonitorDetailIds(
        Guid NewCompanyId,
        Guid NewSiteId,
        Guid MonitorId,
        DateTime NewDeploymentStart);
}
