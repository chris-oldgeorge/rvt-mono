// File summary: Handles transactional CQRS commands for report-rule mutation workflows.
// Major updates:
// - 2026-06-26 pending Rejected archived sites explicitly during report-rule mutations.
// - 2026-06-26 pending Moved report-rule writes behind MediatR transactional commands.

using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Application.ReportRules;

public sealed record CreateReportRuleCommand(ReportRuleMutationRequest Request, Guid UserId)
    : IRequest<ReportRuleCommandResult>, ITransactionalRequest;

public sealed record UpdateReportRuleCommand(Guid ReportRuleId, ReportRuleMutationRequest Request, Guid UserId)
    : IRequest<ReportRuleCommandResult>, ITransactionalRequest;

public sealed record DeleteReportRuleCommand(Guid ReportRuleId)
    : IRequest<ReportRuleCommandResult>, ITransactionalRequest;

public sealed record AddReportRuleUserCommand(Guid ReportRuleId, Guid UserId)
    : IRequest<ReportRuleCommandResult>, ITransactionalRequest;

public sealed record RemoveReportRuleUserCommand(Guid ReportRuleId, Guid UserId)
    : IRequest<ReportRuleCommandResult>, ITransactionalRequest;

public sealed class ReportRuleCommandResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public Guid? ReportRuleId { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class CreateReportRuleCommandHandler : IRequestHandler<CreateReportRuleCommand, ReportRuleCommandResult>
{
    private readonly RVTSearchContext searchContext;
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional report-rule create command handler.
    public CreateReportRuleCommandHandler(RVTSearchContext searchContext, RVTDbContext domainContext)
    {
        this.searchContext = searchContext;
        this.domainContext = domainContext;
    }

    // Function summary: Creates a report rule after validating site and schedule fields.
    public async Task<ReportRuleCommandResult> Handle(CreateReportRuleCommand request, CancellationToken cancellationToken)
    {
        var result = new ReportRuleCommandResult();
        await ReportRuleCommandWorkflow.ValidateRuleRequestAsync(domainContext, request.Request, result.Errors, cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        var rule = ReportRuleCommandWorkflow.CreateRule(request.Request, request.UserId);
        searchContext.ReportRules.Add(rule);
        result.ReportRuleId = rule.Id;
        return result;
    }
}

public sealed class UpdateReportRuleCommandHandler : IRequestHandler<UpdateReportRuleCommand, ReportRuleCommandResult>
{
    private readonly RVTSearchContext searchContext;
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional report-rule update command handler.
    public UpdateReportRuleCommandHandler(RVTSearchContext searchContext, RVTDbContext domainContext)
    {
        this.searchContext = searchContext;
        this.domainContext = domainContext;
    }

    // Function summary: Updates a report rule after validating site and schedule fields.
    public async Task<ReportRuleCommandResult> Handle(UpdateReportRuleCommand request, CancellationToken cancellationToken)
    {
        var result = new ReportRuleCommandResult { ReportRuleId = request.ReportRuleId };
        var rule = await searchContext.ReportRules.SingleOrDefaultAsync(
            item => item.Id == request.ReportRuleId && !item.Deleted,
            cancellationToken);
        if (rule == null)
        {
            result.NotFound = true;
            return result;
        }

        await ReportRuleCommandWorkflow.ValidateRuleRequestAsync(domainContext, request.Request, result.Errors, cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        ReportRuleCommandWorkflow.ApplyRuleMutation(rule, request.Request, request.UserId);
        return result;
    }
}

public sealed class DeleteReportRuleCommandHandler : IRequestHandler<DeleteReportRuleCommand, ReportRuleCommandResult>
{
    private readonly RVTSearchContext searchContext;

    // Function summary: Initializes the transactional report-rule delete command handler.
    public DeleteReportRuleCommandHandler(RVTSearchContext searchContext)
    {
        this.searchContext = searchContext;
    }

    // Function summary: Soft-deletes a report rule.
    public async Task<ReportRuleCommandResult> Handle(DeleteReportRuleCommand request, CancellationToken cancellationToken)
    {
        var result = new ReportRuleCommandResult { ReportRuleId = request.ReportRuleId };
        var rule = await searchContext.ReportRules.SingleOrDefaultAsync(
            item => item.Id == request.ReportRuleId && !item.Deleted,
            cancellationToken);
        if (rule == null)
        {
            result.NotFound = true;
            return result;
        }

        rule.Deleted = true;
        return result;
    }
}

public sealed class AddReportRuleUserCommandHandler : IRequestHandler<AddReportRuleUserCommand, ReportRuleCommandResult>
{
    private readonly RVTSearchContext searchContext;
    private readonly RVTDbContext domainContext;
    private readonly UserManager<ApplicationUser> userManager;

    // Function summary: Initializes the transactional report-rule user-add command handler.
    public AddReportRuleUserCommandHandler(RVTSearchContext searchContext, RVTDbContext domainContext, UserManager<ApplicationUser> userManager)
    {
        this.searchContext = searchContext;
        this.domainContext = domainContext;
        this.userManager = userManager;
    }

    // Function summary: Adds a user assignment to a report rule when the user is eligible.
    public async Task<ReportRuleCommandResult> Handle(AddReportRuleUserCommand request, CancellationToken cancellationToken)
    {
        var result = new ReportRuleCommandResult { ReportRuleId = request.ReportRuleId };
        var rule = await searchContext.ReportRules.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.ReportRuleId && !item.Deleted,
            cancellationToken);
        if (rule == null)
        {
            result.NotFound = true;
            return result;
        }

        if (!await ReportRuleCommandWorkflow.ValidateReportUserAsync(domainContext, userManager, rule, request.UserId, result.Errors, cancellationToken))
        {
            return result;
        }

        var exists = await searchContext.ReportUsers.AnyAsync(
            item => item.ReportRuleId == request.ReportRuleId && item.UserId == request.UserId,
            cancellationToken);
        if (!exists)
        {
            searchContext.ReportUsers.Add(new ReportUser
            {
                ReportRuleId = request.ReportRuleId,
                UserId = request.UserId
            });
        }

        return result;
    }
}

public sealed class RemoveReportRuleUserCommandHandler : IRequestHandler<RemoveReportRuleUserCommand, ReportRuleCommandResult>
{
    private readonly RVTSearchContext searchContext;

    // Function summary: Initializes the transactional report-rule user-remove command handler.
    public RemoveReportRuleUserCommandHandler(RVTSearchContext searchContext)
    {
        this.searchContext = searchContext;
    }

    // Function summary: Removes matching user assignments from a report rule.
    public async Task<ReportRuleCommandResult> Handle(RemoveReportRuleUserCommand request, CancellationToken cancellationToken)
    {
        var result = new ReportRuleCommandResult { ReportRuleId = request.ReportRuleId };
        var rule = await searchContext.ReportRules.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.ReportRuleId && !item.Deleted,
            cancellationToken);
        if (rule == null)
        {
            result.NotFound = true;
            return result;
        }

        var assignments = await searchContext.ReportUsers
            .Where(item => item.ReportRuleId == request.ReportRuleId && item.UserId == request.UserId)
            .ToListAsync(cancellationToken);
        if (assignments.Count > 0)
        {
            searchContext.ReportUsers.RemoveRange(assignments);
        }

        return result;
    }
}

internal static class ReportRuleCommandWorkflow
{
    private const int MaxReportNameLength = 128;

    private static readonly ReportFrequencyType[] SupportedFrequencies =
    [
        ReportFrequencyType.Daily,
        ReportFrequencyType.Weekly,
        ReportFrequencyType.Monthly,
        ReportFrequencyType.WeeklyAndMonthly
    ];

    private static readonly DayOfWeek[] SupportedDays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    // Function summary: Validates report-rule mutation input.
    public static async Task ValidateRuleRequestAsync(
        RVTDbContext domainContext,
        ReportRuleMutationRequest request,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        if (!SupportedFrequencies.Contains(request.Frequency))
        {
            AddError(errors, nameof(ReportRuleMutationRequest.Frequency), "Frequency is not supported.");
        }

        var site = await domainContext.Sites
            .AsNoTracking()
            .Where(item => item.Id == request.SiteId)
            .Select(item => new { item.Archived })
            .SingleOrDefaultAsync(cancellationToken);
        if (site == null)
        {
            AddError(errors, nameof(ReportRuleMutationRequest.SiteId), "Site was not found.");
        }
        else if (site.Archived)
        {
            AddError(errors, nameof(ReportRuleMutationRequest.SiteId), "Archived sites cannot be used for report rules.");
        }

        if (request.Frequency is ReportFrequencyType.Weekly or ReportFrequencyType.WeeklyAndMonthly && !request.DayOfWeek.HasValue)
        {
            AddError(errors, nameof(ReportRuleMutationRequest.DayOfWeek), "Day of week is required for weekly reports.");
        }
        else if (request.DayOfWeek.HasValue && !SupportedDays.Contains(request.DayOfWeek.Value))
        {
            AddError(errors, nameof(ReportRuleMutationRequest.DayOfWeek), "Day of week is not valid.");
        }

        if (request.Frequency is ReportFrequencyType.Monthly or ReportFrequencyType.WeeklyAndMonthly &&
            (!request.DayOfMonth.HasValue || request.DayOfMonth < 1 || request.DayOfMonth > 31))
        {
            AddError(errors, nameof(ReportRuleMutationRequest.DayOfMonth), "Day of month must be between 1 and 31.");
        }

        if (request.ReportName?.Length > MaxReportNameLength)
        {
            AddError(errors, nameof(ReportRuleMutationRequest.ReportName), $"Report name must be {MaxReportNameLength} characters or fewer.");
        }
    }

    // Function summary: Creates a report rule from request data.
    public static ReportRule CreateRule(ReportRuleMutationRequest request, Guid userId)
    {
        return new ReportRule
        {
            SiteId = request.SiteId,
            UserId = userId,
            Frequency = request.Frequency,
            DayOfWeek = request.DayOfWeek,
            DayOfMonth = request.DayOfMonth,
            ReportName = EmptyToNull(request.ReportName),
            Deleted = false
        };
    }

    // Function summary: Applies request data to an existing report rule.
    public static void ApplyRuleMutation(ReportRule rule, ReportRuleMutationRequest request, Guid userId)
    {
        rule.SiteId = request.SiteId;
        rule.UserId = userId;
        rule.Frequency = request.Frequency;
        rule.DayOfWeek = request.DayOfWeek;
        rule.DayOfMonth = request.DayOfMonth;
        rule.ReportName = EmptyToNull(request.ReportName);
        rule.Deleted = false;
    }

    // Function summary: Validates whether a user may be assigned to a report rule.
    public static async Task<bool> ValidateReportUserAsync(
        RVTDbContext domainContext,
        UserManager<ApplicationUser> userManager,
        ReportRule rule,
        Guid userId,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            AddError(errors, nameof(ReportUserMutationRequest.UserId), "User was not found.");
            return false;
        }

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(RoleNames.RVTMasterAdmin, StringComparer.Ordinal) ||
            roles.Contains(RoleNames.RVTAdmin, StringComparer.Ordinal))
        {
            return true;
        }

        if (await domainContext.SiteUsers.AsNoTracking().AnyAsync(
            siteUser => siteUser.SiteId == rule.SiteId &&
                siteUser.UserId == userId &&
                siteUser.EndDate == null,
            cancellationToken))
        {
            return true;
        }

        AddError(errors, nameof(ReportUserMutationRequest.UserId), "User is not assigned to the report site.");
        return false;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out var existing)
            ? [.. existing, message]
            : [message];
    }
}
