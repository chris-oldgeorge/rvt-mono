// File summary: Handles transactional CQRS commands for user site-assignment workflows.
// Major updates:
// - 2026-06-26 pending Moved site-contact and user-from-site removal writes behind transactional commands.
// - 2026-06-26 pending Moved user site assignment and default notification-setting writes behind MediatR transactional commands.

using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Data;
using RvtPortal.Spa.UseCases.Common;

namespace RvtPortal.Spa.UseCases.Users;

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
    private readonly RVTDbContext _domainContext;
    private readonly UserManager<ApplicationUser> _userManager;

    // Function summary: Initializes the transactional user site-assignment command handler.
    public AddUserToSiteCommandHandler(RVTDbContext domainContext, UserManager<ApplicationUser> userManager)
    {
        _domainContext = domainContext;
        _userManager = userManager;
    }

    // Function summary: Adds a user to a site and creates the default notification settings atomically.
    public async Task<UserSiteAssignmentCommandResult> Handle(AddUserToSiteCommand request, CancellationToken cancellationToken)
    {
        UserSiteAssignmentCommandResult result = new();
        if (await _userManager.FindByIdAsync(request.UserId.ToString()) == null)
        {
            result.UserNotFound = true;
            return result;
        }

        if (!await _domainContext.Sites.AsNoTracking().AnyAsync(site => site.Id == request.SiteId, cancellationToken))
        {
            result.SiteNotFound = true;
            return result;
        }

        if (await _domainContext.SiteUsers.AnyAsync(
            siteUser => siteUser.UserId == request.UserId && siteUser.SiteId == request.SiteId,
            cancellationToken))
        {
            return result;
        }

        SiteUsers siteUser = new()
        {
            Id = Guid.NewGuid(),
            StartDate = DateTime.UtcNow,
            SiteId = request.SiteId,
            UserId = request.UserId,
            SiteContact = false
        };
        _domainContext.SiteUsers.Add(siteUser);
        _domainContext.NotificationSettings.Add(new NotificationSettings
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
    private readonly RVTDbContext _domainContext;

    // Function summary: Initializes the transactional set-site-contact command handler.
    public SetSiteContactCommandHandler(RVTDbContext domainContext)
    {
        _domainContext = domainContext;
    }

    // Function summary: Sets one assigned user as the site's contact and clears other contacts atomically.
    public async Task<UserSiteAssignmentCommandResult> Handle(SetSiteContactCommand request, CancellationToken cancellationToken)
    {
        UserSiteAssignmentCommandResult result = new();
        List<SiteUsers> siteUsers = await _domainContext.SiteUsers
            .Where(siteUser => siteUser.SiteId == request.SiteId)
            .ToListAsync(cancellationToken);
        SiteUsers? selected = siteUsers.FirstOrDefault(siteUser => siteUser.UserId == request.UserId);
        if (selected == null)
        {
            result.SiteNotFound = true;
            return result;
        }

        foreach (SiteUsers? siteUser in siteUsers)
        {
            siteUser.SiteContact = siteUser.Id == selected.Id;
        }

        return result;
    }
}

public sealed class RemoveSiteContactCommandHandler
    : IRequestHandler<RemoveSiteContactCommand, UserSiteAssignmentCommandResult>
{
    private readonly RVTDbContext _domainContext;

    // Function summary: Initializes the transactional remove-site-contact command handler.
    public RemoveSiteContactCommandHandler(RVTDbContext domainContext)
    {
        _domainContext = domainContext;
    }

    // Function summary: Clears the site contact flag for all assignments on the requested site.
    public async Task<UserSiteAssignmentCommandResult> Handle(RemoveSiteContactCommand request, CancellationToken cancellationToken)
    {
        UserSiteAssignmentCommandResult result = new();
        if (!await _domainContext.SiteUsers.AnyAsync(
            siteUser => siteUser.SiteId == request.SiteId && siteUser.UserId == request.UserId,
            cancellationToken))
        {
            result.SiteNotFound = true;
            return result;
        }

        List<SiteUsers> siteUsers = await _domainContext.SiteUsers
            .Where(siteUser => siteUser.SiteId == request.SiteId)
            .ToListAsync(cancellationToken);
        foreach (SiteUsers? siteUser in siteUsers)
        {
            siteUser.SiteContact = false;
        }

        return result;
    }
}

public sealed class RemoveUserFromSiteCommandHandler
    : IRequestHandler<RemoveUserFromSiteCommand, UserSiteAssignmentCommandResult>
{
    private readonly RVTDbContext _domainContext;

    // Function summary: Initializes the transactional remove-user-from-site command handler.
    public RemoveUserFromSiteCommandHandler(RVTDbContext domainContext)
    {
        _domainContext = domainContext;
    }

    // Function summary: Removes a user's site assignment through the shared transaction pipeline.
    public async Task<UserSiteAssignmentCommandResult> Handle(RemoveUserFromSiteCommand request, CancellationToken cancellationToken)
    {
        UserSiteAssignmentCommandResult result = new();
        SiteUsers? siteUser = await _domainContext.SiteUsers.SingleOrDefaultAsync(
            assignment => assignment.SiteId == request.SiteId && assignment.UserId == request.UserId,
            cancellationToken);
        if (siteUser == null)
        {
            result.SiteNotFound = true;
            return result;
        }

        _domainContext.SiteUsers.Remove(siteUser);
        result.Removed = true;
        return result;
    }
}
