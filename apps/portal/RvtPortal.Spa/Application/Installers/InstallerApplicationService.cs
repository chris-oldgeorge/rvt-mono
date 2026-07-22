// File summary: Provides installer-scoped monitor inventory, monitor detail, deployment mutation, and status workflows.
// Major updates:
// - 2026-07-09 pending Moved installer detail and deployment-location orchestration out of the API controller.
// - 2026-07-09 pending Moved installer what3words conversion configuration and HTTP access out of the API controller.
// - 2026-07-09 pending Moved installer monitor read and deployment visibility logic out of the API controller.

using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RVT.BusinessLogic.Application;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Monitors;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Installers;

public interface IInstallerApplicationService
{
    // Function summary: Returns the installer monitor inventory using installer state and fixed fleet-number sort semantics.
    Task<MonitorInventoryResult> QueryMonitorsAsync(
        PortalUserContext actor,
        InstallerMonitorQuery query,
        CancellationToken cancellationToken);

    // Function summary: Evaluates deployment access for update preflight checks.
    Task<bool> CanAccessDeploymentAsync(
        PortalUserContext actor,
        Guid deploymentId,
        CancellationToken cancellationToken);

    // Function summary: Builds visible monitor detail for installer monitor routes.
    Task<MonitorDetailResponse?> GetMonitorDetailAsync(
        PortalUserContext actor,
        ClaimsPrincipal principal,
        Guid monitorId,
        CancellationToken cancellationToken);

    // Function summary: Rebuilds a visible deployment detail after installer deployment mutations.
    Task<MonitorDetailResponse?> GetDeploymentDetailAsync(
        PortalUserContext actor,
        ClaimsPrincipal principal,
        Guid deploymentId,
        CancellationToken cancellationToken);

    // Function summary: Updates visible installer deployment location data and returns refreshed detail.
    Task<InstallerDeploymentWorkflowResult> UpdateDeploymentAsync(
        PortalUserContext actor,
        ClaimsPrincipal principal,
        Guid deploymentId,
        InstallerDeploymentMutationRequest request,
        CancellationToken cancellationToken);

    // Function summary: Returns the visible current monitor status for installer status polling.
    Task<InstallerMonitorStatusModel?> GetMonitorStatusAsync(
        PortalUserContext actor,
        Guid monitorId,
        CancellationToken cancellationToken);

    // Function summary: Converts a what3words address to coordinates through the configured external service.
    Task<What3WordsConversionResult> ConvertWhat3WordsAsync(
        string what3words,
        CancellationToken cancellationToken);
}

public sealed record InstallerMonitorQuery(
    MonitorTypeEnum? MonitorType,
    string? SearchText,
    string SortDir,
    int Page,
    int PageSize);

public sealed class InstallerMonitorStatusModel
{
    public Guid MonitorId { get; init; }
    public bool IsOffline { get; init; }
    public DateTime? LastDataTime { get; init; }
    public string Status { get; init; } = "";
}

public enum What3WordsConversionFailureKind
{
    ServiceUnavailable,
    BadGateway
}

public sealed record What3WordsConversionResult(
    What3WordsConvertResponse? Value,
    What3WordsConversionFailureKind? Failure,
    int? ExternalStatusCode = null)
{
    // Function summary: Wraps a successful what3words conversion response.
    public static What3WordsConversionResult Success(What3WordsConvertResponse value)
    {
        return new What3WordsConversionResult(value, null);
    }

    // Function summary: Wraps an unavailable what3words configuration response.
    public static What3WordsConversionResult ServiceUnavailable()
    {
        return new What3WordsConversionResult(null, What3WordsConversionFailureKind.ServiceUnavailable);
    }

    // Function summary: Wraps a failed external what3words response.
    public static What3WordsConversionResult BadGateway(int statusCode)
    {
        return new What3WordsConversionResult(null, What3WordsConversionFailureKind.BadGateway, statusCode);
    }
}

public sealed class InstallerDeploymentWorkflowResult
{
    public bool NotFound { get; init; }
    public MonitorDetailResponse? Detail { get; init; }
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    // Function summary: Builds a not-found installer deployment workflow result.
    public static InstallerDeploymentWorkflowResult Missing()
    {
        return new InstallerDeploymentWorkflowResult { NotFound = true };
    }

    // Function summary: Builds a validation or mutation result from the deployment command.
    public static InstallerDeploymentWorkflowResult FromCommand(InstallerDeploymentCommandResult result)
    {
        return new InstallerDeploymentWorkflowResult
        {
            NotFound = result.NotFound,
            Errors = result.Errors
        };
    }
}

public sealed class InstallerApplicationService : IInstallerApplicationService
{
    private readonly RVTDbContext domainContext;
    private readonly IMonitorAdministrationReadService monitors;
    private readonly IMonitorDetailReader detailReader;
    private readonly IConfiguration configuration;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMediator mediator;

    // Function summary: Initializes installer workflows with monitor read services and the domain context.
    public InstallerApplicationService(
        RVTDbContext domainContext,
        IMonitorAdministrationReadService monitors,
        IMonitorDetailReader detailReader,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IMediator mediator)
    {
        this.domainContext = domainContext;
        this.monitors = monitors;
        this.detailReader = detailReader;
        this.configuration = configuration;
        this.httpClientFactory = httpClientFactory;
        this.mediator = mediator;
    }

    // Function summary: Returns the paged installer monitor list.
    public Task<MonitorInventoryResult> QueryMonitorsAsync(
        PortalUserContext actor,
        InstallerMonitorQuery query,
        CancellationToken cancellationToken)
    {
        return monitors.QueryAsync(
            new MonitorInventoryRequest(
                query.MonitorType,
                MonitorListStates.Installer,
                query.SearchText,
                "fleetNumber",
                query.SortDir,
                query.Page,
                query.PageSize),
            actor,
            cancellationToken);
    }

    // Function summary: Evaluates whether the actor may address a deployment.
    public async Task<bool> CanAccessDeploymentAsync(
        PortalUserContext actor,
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        return await FindVisibleDeploymentByDeploymentIdAsync(actor, deploymentId, cancellationToken) != null;
    }

    // Function summary: Builds detail for a visible current monitor deployment.
    public async Task<MonitorDetailResponse?> GetMonitorDetailAsync(
        PortalUserContext actor,
        ClaimsPrincipal principal,
        Guid monitorId,
        CancellationToken cancellationToken)
    {
        var deployment = await FindVisibleDeploymentByMonitorIdAsync(actor, monitorId, cancellationToken);
        return deployment == null
            ? null
            : await detailReader.BuildAsync(deployment.Monitor, deployment, principal, cancellationToken);
    }

    // Function summary: Builds detail for a deployment visible to the actor.
    public async Task<MonitorDetailResponse?> GetDeploymentDetailAsync(
        PortalUserContext actor,
        ClaimsPrincipal principal,
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        var deployment = await FindVisibleDeploymentByDeploymentIdAsync(actor, deploymentId, cancellationToken);
        return deployment == null
            ? null
            : await detailReader.BuildAsync(deployment.Monitor, deployment, principal, cancellationToken);
    }

    // Function summary: Updates visible installer deployment location data and returns refreshed detail.
    public async Task<InstallerDeploymentWorkflowResult> UpdateDeploymentAsync(
        PortalUserContext actor,
        ClaimsPrincipal principal,
        Guid deploymentId,
        InstallerDeploymentMutationRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessDeploymentAsync(actor, deploymentId, cancellationToken))
        {
            return InstallerDeploymentWorkflowResult.Missing();
        }

        var result = await mediator.Send(new UpdateInstallerDeploymentLocationCommand(deploymentId, request), cancellationToken);
        if (result.NotFound || result.Errors.Count > 0)
        {
            return InstallerDeploymentWorkflowResult.FromCommand(result);
        }

        var detail = await GetDeploymentDetailAsync(actor, principal, deploymentId, cancellationToken);
        return detail == null
            ? InstallerDeploymentWorkflowResult.Missing()
            : new InstallerDeploymentWorkflowResult
            {
                Detail = detail,
                Errors = result.Errors
            };
    }

    // Function summary: Builds installer monitor status for a visible current deployment.
    public async Task<InstallerMonitorStatusModel?> GetMonitorStatusAsync(
        PortalUserContext actor,
        Guid monitorId,
        CancellationToken cancellationToken)
    {
        var deployment = await FindVisibleDeploymentByMonitorIdAsync(actor, monitorId, cancellationToken);
        if (deployment == null)
        {
            return null;
        }

        var lastDataTime = LastDataTime(deployment.Monitor);
        var isOffline = IsOffline(lastDataTime);
        return new InstallerMonitorStatusModel
        {
            MonitorId = monitorId,
            LastDataTime = lastDataTime,
            IsOffline = isOffline,
            Status = isOffline ? "Offline" : "Online"
        };
    }

    // Function summary: Converts a what3words address to coordinates using the configured external API.
    public async Task<What3WordsConversionResult> ConvertWhat3WordsAsync(
        string what3words,
        CancellationToken cancellationToken)
    {
        var trimmedWords = what3words.Trim();
        var apiKey = configuration["What3Words:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return What3WordsConversionResult.ServiceUnavailable();
        }

        var path = $"https://api.what3words.com/v3/convert-to-coordinates?words={Uri.EscapeDataString(trimmedWords)}&key={Uri.EscapeDataString(apiKey)}";
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        using var response = await client.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return What3WordsConversionResult.BadGateway((int)response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var coordinates = root.TryGetProperty("coordinates", out var coordinateElement) ? coordinateElement : default;
        return What3WordsConversionResult.Success(new What3WordsConvertResponse
        {
            Words = root.TryGetProperty("words", out var wordsElement) ? wordsElement.GetString() ?? trimmedWords : trimmedWords,
            Lat = coordinates.ValueKind == JsonValueKind.Object && coordinates.TryGetProperty("lat", out var latElement) ? latElement.GetDouble() : null,
            Lng = coordinates.ValueKind == JsonValueKind.Object && coordinates.TryGetProperty("lng", out var lngElement) ? lngElement.GetDouble() : null,
            NearestPlace = root.TryGetProperty("nearestPlace", out var nearestPlaceElement) ? nearestPlaceElement.GetString() : null,
            Message = "Converted by what3words."
        });
    }

    // Function summary: Finds a current deployment visible to the actor by deployment id.
    private async Task<Deployment?> FindVisibleDeploymentByDeploymentIdAsync(
        PortalUserContext actor,
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        var deployment = await BaseVisibleDeploymentQuery()
            .SingleOrDefaultAsync(item => item.Id == deploymentId && item.EndDate == null, cancellationToken);
        return CanAccessDeployment(actor, deployment) ? deployment : null;
    }

    // Function summary: Finds a current deployment visible to the actor by monitor id.
    private async Task<Deployment?> FindVisibleDeploymentByMonitorIdAsync(
        PortalUserContext actor,
        Guid monitorId,
        CancellationToken cancellationToken)
    {
        var deployment = await BaseVisibleDeploymentQuery()
            .Where(item => item.MonitorId == monitorId && item.EndDate == null)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        return CanAccessDeployment(actor, deployment) ? deployment : null;
    }

    // Function summary: Builds the deployment query shape required for installer authorization and detail responses.
    private IQueryable<Deployment> BaseVisibleDeploymentQuery()
    {
        return domainContext.Deployments
            .AsNoTracking()
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Company)
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Site)
            .Include(deployment => deployment.Monitor);
    }

    // Function summary: Evaluates deployment access from shared portal role facts.
    private static bool CanAccessDeployment(PortalUserContext actor, Deployment? deployment)
    {
        if (deployment == null)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        return IsInstallerOnly(actor) &&
            actor.CompanyId.HasValue &&
            deployment.Contract.CompanyId == actor.CompanyId.Value;
    }

    // Function summary: Returns the latest monitor data timestamp used for installer status labels.
    private static DateTime? LastDataTime(MonitorEntity monitor)
    {
        return new[] { monitor.LastDataTime15Min, monitor.LastDataTime1Min, monitor.LastDataTime1Hour, monitor.LastDataTime24Hour }
            .Where(value => value.HasValue)
            .Max();
    }

    // Function summary: Evaluates whether a monitor is offline for installer status labels.
    private static bool IsOffline(DateTime? lastDataTime)
    {
        return !lastDataTime.HasValue || lastDataTime.Value < DateTime.UtcNow.AddHours(-1);
    }

    // Function summary: Evaluates whether the actor is an installer without admin privileges.
    private static bool IsInstallerOnly(PortalUserContext actor)
    {
        return actor.IsInstaller && !actor.IsAdmin;
    }
}
