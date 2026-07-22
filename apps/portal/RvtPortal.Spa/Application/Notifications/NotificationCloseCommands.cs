// File summary: Handles transactional CQRS commands for notification close workflows.
// Major updates:
// - 2026-06-26 pending Scoped notification close authorization to effective deployment/contract ownership windows.
// - 2026-06-25 pending Moved notification close and batch-close mutations behind MediatR transactional commands.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;
using RvtPortal.Spa.Application.Monitors;

namespace RvtPortal.Spa.Application.Notifications;

public sealed record NotificationCloseActor(Guid? UserId, bool IsAdmin, bool IsCompanyUser);

public sealed record CloseNotificationCommand(Guid NotificationId, string? Note, NotificationCloseActor Actor)
    : IRequest<CloseNotificationResult>, ITransactionalRequest;

public sealed class CloseNotificationResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public Notification? Notification { get; set; }
    public Deployment? Deployment { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class CloseNotificationCommandHandler
    : IRequestHandler<CloseNotificationCommand, CloseNotificationResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional notification close command handler.
    public CloseNotificationCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Validates visibility and closes a single alert notification.
    public async Task<CloseNotificationResult> Handle(
        CloseNotificationCommand request,
        CancellationToken cancellationToken)
    {
        var result = new CloseNotificationResult();
        var notification = await NotificationCloseWorkflow.LoadNotificationAsync(domainContext, request.NotificationId, cancellationToken);
        if (notification == null)
        {
            result.NotFound = true;
            return result;
        }

        var deployment = await NotificationCloseWorkflow.FindDeploymentForNotificationAsync(domainContext, notification, cancellationToken);
        var access = NotificationCloseWorkflow.BuildAccessInfo(notification, deployment);
        var visibleSiteIds = await NotificationCloseWorkflow.VisibleSiteIdsAsync(domainContext, request.Actor, cancellationToken);
        if (!NotificationCloseWorkflow.CanReadNotification(access, request.Actor, visibleSiteIds))
        {
            result.NotFound = true;
            return result;
        }

        result.Notification = notification;
        result.Deployment = deployment;
        if (!access.CanClose)
        {
            AddError(result.Errors, nameof(NotificationCloseRequest.Note), "Only alert notifications can be closed.");
        }
        if (request.Note?.Length > 255)
        {
            AddError(result.Errors, nameof(NotificationCloseRequest.Note), "Note must be 255 characters or fewer.");
        }
        if (result.Errors.Count > 0)
        {
            return result;
        }

        NotificationCloseWorkflow.Close(notification, request.Note, request.Actor);
        return result;
    }

    // Function summary: Appends a validation error to a command result.
    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out var existing)
            ? [.. existing, message]
            : [message];
    }
}

public sealed record BatchCloseNotificationsCommand(
    IReadOnlyCollection<Guid> NotificationIds,
    string? Note,
    NotificationCloseActor Actor)
    : IRequest<NotificationBatchCloseResponse>, ITransactionalRequest;

public sealed class BatchCloseNotificationsCommandHandler
    : IRequestHandler<BatchCloseNotificationsCommand, NotificationBatchCloseResponse>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional notification batch-close command handler.
    public BatchCloseNotificationsCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Closes visible alert notifications and reports skipped notification ids.
    public async Task<NotificationBatchCloseResponse> Handle(
        BatchCloseNotificationsCommand request,
        CancellationToken cancellationToken)
    {
        var ids = request.NotificationIds.Distinct().ToList();
        var response = new NotificationBatchCloseResponse { Requested = ids.Count };
        if (ids.Count == 0)
        {
            return response;
        }

        var notifications = await domainContext.Notifications
            .Include(notification => notification.Monitor)
            .Where(notification => ids.Contains(notification.Id))
            .ToListAsync(cancellationToken);
        var byId = notifications.ToDictionary(notification => notification.Id);
        var deploymentLookup = await NotificationCloseWorkflow.BuildDeploymentLookupAsync(domainContext, notifications, cancellationToken);
        var visibleSiteIds = await NotificationCloseWorkflow.VisibleSiteIdsAsync(domainContext, request.Actor, cancellationToken);
        var note = string.IsNullOrWhiteSpace(request.Note) ? "batch close" : request.Note;

        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var notification))
            {
                response.NotFoundIds.Add(id);
                continue;
            }

            deploymentLookup.TryGetValue(notification.Id, out var deployment);
            var access = NotificationCloseWorkflow.BuildAccessInfo(notification, deployment);
            if (!NotificationCloseWorkflow.CanReadNotification(access, request.Actor, visibleSiteIds))
            {
                response.ForbiddenIds.Add(id);
                continue;
            }
            if (!access.CanClose)
            {
                response.InvalidIds.Add(id);
                continue;
            }

            NotificationCloseWorkflow.Close(notification, note, request.Actor);
            response.ClosedIds.Add(id);
        }

        return response;
    }
}

internal static class NotificationCloseWorkflow
{
    // Function summary: Loads a notification with the monitor needed for close response mapping.
    public static Task<Notification?> LoadNotificationAsync(RVTDbContext domainContext, Guid id, CancellationToken cancellationToken)
    {
        return domainContext.Notifications
            .Include(notification => notification.Monitor)
            .SingleOrDefaultAsync(notification => notification.Id == id, cancellationToken);
    }

    // Function summary: Builds deployment lookup data for a notification batch close.
    public static async Task<Dictionary<Guid, Deployment?>> BuildDeploymentLookupAsync(
        RVTDbContext domainContext,
        IReadOnlyCollection<Notification> notifications,
        CancellationToken cancellationToken)
    {
        var monitorIds = notifications.Select(notification => notification.MonitorId).Distinct().ToList();
        var deployments = new List<Deployment>();
        if (monitorIds.Count > 0)
        {
            deployments = await domainContext.Deployments
                .AsNoTracking()
                .Include(deployment => deployment.Contract)
                .ThenInclude(contract => contract.Company)
                .Include(deployment => deployment.Contract)
                .ThenInclude(contract => contract.Site)
                .Include(deployment => deployment.Monitor)
                .Where(deployment => monitorIds.Contains(deployment.MonitorId))
                .ToListAsync(cancellationToken);
        }

        return notifications.ToDictionary(
            notification => notification.Id,
            notification => MatchDeployment(notification, deployments));
    }

    // Function summary: Finds the deployment most relevant to a notification close decision.
    public static async Task<Deployment?> FindDeploymentForNotificationAsync(
        RVTDbContext domainContext,
        Notification notification,
        CancellationToken cancellationToken)
    {
        var deployments = await domainContext.Deployments
            .AsNoTracking()
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Company)
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Site)
            .Include(deployment => deployment.Monitor)
            .Where(deployment => deployment.MonitorId == notification.MonitorId)
            .ToListAsync(cancellationToken);
        return MatchDeployment(notification, deployments);
    }

    // Function summary: Reads site visibility for the current company-user actor.
    public static async Task<HashSet<Guid>> VisibleSiteIdsAsync(
        RVTDbContext domainContext,
        NotificationCloseActor actor,
        CancellationToken cancellationToken)
    {
        if (!actor.IsCompanyUser || !actor.UserId.HasValue)
        {
            return [];
        }

        return await domainContext.SiteUsers
            .AsNoTracking()
            .Where(siteUser => siteUser.UserId == actor.UserId.Value && siteUser.EndDate == null)
            .Select(siteUser => siteUser.SiteId)
            .ToHashSetAsync(cancellationToken);
    }

    // Function summary: Builds the close-access facts required by notification command handlers.
    public static NotificationCloseAccess BuildAccessInfo(Notification notification, Deployment? deployment)
    {
        return new NotificationCloseAccess(deployment?.Contract?.SiteiD, notification.AlertType == AlertTypeEnum.Alert);
    }

    // Function summary: Evaluates whether the actor can read and close-skip a notification row.
    public static bool CanReadNotification(
        NotificationCloseAccess access,
        NotificationCloseActor actor,
        IReadOnlySet<Guid> visibleSiteIds)
    {
        return actor.IsAdmin || (actor.IsCompanyUser && access.SiteId.HasValue && visibleSiteIds.Contains(access.SiteId.Value));
    }

    // Function summary: Applies the close state to a tracked notification entity.
    public static void Close(Notification notification, string? note, NotificationCloseActor actor)
    {
        notification.ClosedNote = note ?? "";
        notification.ClosedTime = DateTime.UtcNow;
        notification.ClosedByUser = actor.UserId;
    }

    // Function summary: Selects the deployment active at notification time, falling back to current/latest deployment.
    private static Deployment? MatchDeployment(Notification notification, IEnumerable<Deployment> deployments)
    {
        return MonitorOwnershipWindowResolver.MatchDeploymentAt(
            notification.MonitorId,
            notification.NotificationTime,
            deployments);
    }
}

public sealed record NotificationCloseAccess(Guid? SiteId, bool CanClose);
