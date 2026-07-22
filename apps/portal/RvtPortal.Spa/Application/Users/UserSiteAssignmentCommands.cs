// File summary: Handles transactional CQRS commands for user site-assignment workflows.
// Major updates:
// - 2026-06-26 pending Moved site-contact and user-from-site removal writes behind transactional commands.
// - 2026-06-26 pending Moved user site assignment and default notification-setting writes behind MediatR transactional commands.

using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Application.Common;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Application.Users;

public sealed record AddUserToSiteCommand(Guid UserId, Guid SiteId)
    : IRequest<UserSiteAssignmentCommandResult>, ITransactionalRequest;

public sealed record SetSiteContactCommand(Guid UserId, Guid SiteId)
    : IRequest<UserSiteAssignmentCommandResult>, ITransactionalRequest;

public sealed record RemoveSiteContactCommand(Guid UserId, Guid SiteId)
    : IRequest<UserSiteAssignmentCommandResult>, ITransactionalRequest;

public sealed record RemoveUserFromSiteCommand(Guid UserId, Guid SiteId)
    : IRequest<UserSiteAssignmentCommandResult>, ITransactionalRequest;

public sealed class UserSiteAssignmentCommandResult : ITransactionOutcome
{
    public bool UserNotFound { get; set; }
    public bool SiteNotFound { get; set; }
    public bool Created { get; set; }
    public bool Removed { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !UserNotFound && !SiteNotFound && Errors.Count == 0;
}

public sealed class AddUserToSiteCommandHandler
    : IRequestHandler<AddUserToSiteCommand, UserSiteAssignmentCommandResult>
{
    private readonly RVTDbContext domainContext;
    private readonly UserManager<ApplicationUser> userManager;

    // Function summary: Initializes the transactional user site-assignment command handler.
    public AddUserToSiteCommandHandler(RVTDbContext domainContext, UserManager<ApplicationUser> userManager)
    {
        this.domainContext = domainContext;
        this.userManager = userManager;
    }

    // Function summary: Adds a user to a site and creates the default notification settings atomically.
    public async Task<UserSiteAssignmentCommandResult> Handle(AddUserToSiteCommand request, CancellationToken cancellationToken)
    {
        var result = new UserSiteAssignmentCommandResult();
        if (await userManager.FindByIdAsync(request.UserId.ToString()) == null)
        {
            result.UserNotFound = true;
            return result;
        }

        if (!await domainContext.Sites.AsNoTracking().AnyAsync(site => site.Id == request.SiteId, cancellationToken))
        {
            result.SiteNotFound = true;
            return result;
        }

        if (await domainContext.SiteUsers.AnyAsync(
            siteUser => siteUser.UserId == request.UserId && siteUser.SiteId == request.SiteId,
            cancellationToken))
        {
            return result;
        }

        var siteUser = new SiteUsers
        {
            Id = Guid.NewGuid(),
            StartDate = DateTime.UtcNow,
            SiteId = request.SiteId,
            UserId = request.UserId,
            SiteContact = false
        };
        domainContext.SiteUsers.Add(siteUser);
        domainContext.NotificationSettings.Add(new NotificationSettings
        {
            SiteUserId = siteUser.Id,
            Email = true,
            SMS = false,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(18, 0, 0)
        });
        result.Created = true;
        return result;
    }
}

public sealed class SetSiteContactCommandHandler
    : IRequestHandler<SetSiteContactCommand, UserSiteAssignmentCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional set-site-contact command handler.
    public SetSiteContactCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Sets one assigned user as the site's contact and clears other contacts atomically.
    public async Task<UserSiteAssignmentCommandResult> Handle(SetSiteContactCommand request, CancellationToken cancellationToken)
    {
        var result = new UserSiteAssignmentCommandResult();
        var siteUsers = await domainContext.SiteUsers
            .Where(siteUser => siteUser.SiteId == request.SiteId)
            .ToListAsync(cancellationToken);
        var selected = siteUsers.FirstOrDefault(siteUser => siteUser.UserId == request.UserId);
        if (selected == null)
        {
            result.SiteNotFound = true;
            return result;
        }

        foreach (var siteUser in siteUsers)
        {
            siteUser.SiteContact = siteUser.Id == selected.Id;
        }

        return result;
    }
}

public sealed class RemoveSiteContactCommandHandler
    : IRequestHandler<RemoveSiteContactCommand, UserSiteAssignmentCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional remove-site-contact command handler.
    public RemoveSiteContactCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Clears the site contact flag for all assignments on the requested site.
    public async Task<UserSiteAssignmentCommandResult> Handle(RemoveSiteContactCommand request, CancellationToken cancellationToken)
    {
        var result = new UserSiteAssignmentCommandResult();
        if (!await domainContext.SiteUsers.AnyAsync(
            siteUser => siteUser.SiteId == request.SiteId && siteUser.UserId == request.UserId,
            cancellationToken))
        {
            result.SiteNotFound = true;
            return result;
        }

        var siteUsers = await domainContext.SiteUsers
            .Where(siteUser => siteUser.SiteId == request.SiteId)
            .ToListAsync(cancellationToken);
        foreach (var siteUser in siteUsers)
        {
            siteUser.SiteContact = false;
        }

        return result;
    }
}

public sealed class RemoveUserFromSiteCommandHandler
    : IRequestHandler<RemoveUserFromSiteCommand, UserSiteAssignmentCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional remove-user-from-site command handler.
    public RemoveUserFromSiteCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Removes a user's site assignment through the shared transaction pipeline.
    public async Task<UserSiteAssignmentCommandResult> Handle(RemoveUserFromSiteCommand request, CancellationToken cancellationToken)
    {
        var result = new UserSiteAssignmentCommandResult();
        var siteUser = await domainContext.SiteUsers.SingleOrDefaultAsync(
            assignment => assignment.SiteId == request.SiteId && assignment.UserId == request.UserId,
            cancellationToken);
        if (siteUser == null)
        {
            result.SiteNotFound = true;
            return result;
        }

        domainContext.SiteUsers.Remove(siteUser);
        result.Removed = true;
        return result;
    }
}
