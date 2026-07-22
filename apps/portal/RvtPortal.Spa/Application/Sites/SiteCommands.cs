// File summary: Handles transactional CQRS commands for site mutation workflows.
// Major updates:
// - 2026-06-26 pending Moved site create/update/archive/notification-setting writes behind MediatR transactional commands.

using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;

namespace RvtPortal.Spa.Application.Sites;

public sealed record SiteMutationCommandActor(Guid? UserId, string? DisplayName, bool IsAdmin, bool IsCompanyUser);

public sealed record CreateSiteCommand(SiteMutationRequest Request)
    : IRequest<SiteMutationCommandResult>, ITransactionalRequest;

public sealed record UpdateSiteCommand(Guid SiteId, SiteMutationRequest Request)
    : IRequest<SiteMutationCommandResult>, ITransactionalRequest;

public sealed record UpdateSiteNotificationSettingCommand(
    Guid SiteId,
    Guid SiteUserId,
    SiteNotificationSettingMutationRequest Request,
    SiteMutationCommandActor Actor)
    : IRequest<SiteNotificationSettingCommandResult>, ITransactionalRequest;

public class SiteMutationCommandResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public Guid? SiteId { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class SiteNotificationSettingCommandResult : SiteMutationCommandResult
{
    public bool Forbidden { get; set; }
    public Guid? SiteUserId { get; set; }
}

public sealed class CreateSiteCommandHandler : IRequestHandler<CreateSiteCommand, SiteMutationCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional site create command handler.
    public CreateSiteCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Creates a site and assigns the selected contract in one transaction boundary.
    public async Task<SiteMutationCommandResult> Handle(CreateSiteCommand request, CancellationToken cancellationToken)
    {
        var result = new SiteMutationCommandResult();
        var contract = await SiteCommandWorkflow.ValidateSiteAsync(
            domainContext,
            request.Request,
            currentId: null,
            requireContract: true,
            result.Errors,
            cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        var site = SiteCommandWorkflow.CreateSite(request.Request);
        domainContext.Sites.Add(site);
        contract!.SiteiD = site.Id;
        result.SiteId = site.Id;
        return result;
    }
}

public sealed class UpdateSiteCommandHandler : IRequestHandler<UpdateSiteCommand, SiteMutationCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional site update command handler.
    public UpdateSiteCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Updates site fields and operating-hours rows in one transaction boundary.
    public async Task<SiteMutationCommandResult> Handle(UpdateSiteCommand request, CancellationToken cancellationToken)
    {
        var result = new SiteMutationCommandResult { SiteId = request.SiteId };
        var site = await domainContext.Sites
            .Include(item => item.OperatingHours)
            .SingleOrDefaultAsync(item => item.Id == request.SiteId, cancellationToken);
        if (site == null)
        {
            result.NotFound = true;
            return result;
        }

        await SiteCommandWorkflow.ValidateSiteAsync(
            domainContext,
            request.Request,
            request.SiteId,
            requireContract: false,
            result.Errors,
            cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        SiteCommandWorkflow.ApplySiteMutation(site, request.Request);
        return result;
    }
}

public sealed class UpdateSiteNotificationSettingCommandHandler
    : IRequestHandler<UpdateSiteNotificationSettingCommand, SiteNotificationSettingCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional site notification-setting command handler.
    public UpdateSiteNotificationSettingCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Upserts notification settings for an authorized site user.
    public async Task<SiteNotificationSettingCommandResult> Handle(
        UpdateSiteNotificationSettingCommand request,
        CancellationToken cancellationToken)
    {
        var result = new SiteNotificationSettingCommandResult
        {
            SiteId = request.SiteId,
            SiteUserId = request.SiteUserId
        };
        var siteUser = await domainContext.SiteUsers.SingleOrDefaultAsync(
            item => item.Id == request.SiteUserId && item.SiteId == request.SiteId,
            cancellationToken);
        if (siteUser == null || !await SiteCommandWorkflow.CanReadSiteAsync(domainContext, request.SiteId, request.Actor, cancellationToken))
        {
            result.NotFound = true;
            return result;
        }

        if (request.Actor.IsCompanyUser && request.Actor.UserId != siteUser.UserId)
        {
            result.Forbidden = true;
            return result;
        }

        var start = SiteCommandWorkflow.ParseOptionalTime(request.Request.StartTime);
        var end = SiteCommandWorkflow.ParseOptionalTime(request.Request.EndTime);
        SiteCommandWorkflow.ValidateTimePair(
            nameof(SiteNotificationSettingMutationRequest.StartTime),
            start,
            end,
            result.Errors);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        var settings = await domainContext.NotificationSettings.SingleOrDefaultAsync(
            item => item.SiteUserId == request.SiteUserId,
            cancellationToken);
        if (settings == null)
        {
            settings = new NotificationSettings { SiteUserId = request.SiteUserId };
            domainContext.NotificationSettings.Add(settings);
        }

        settings.Email = request.Request.Email;
        settings.SMS = request.Request.Sms;
        settings.StartTime = start;
        settings.EndTime = end;
        return result;
    }
}

internal static class SiteCommandWorkflow
{
    // Function summary: Validates a site mutation request and returns the selected contract when required.
    public static async Task<Contract?> ValidateSiteAsync(
        RVTDbContext domainContext,
        SiteMutationRequest request,
        Guid? currentId,
        bool requireContract,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        await ValidateSiteNameAsync(domainContext, request, currentId, errors, cancellationToken);
        ValidateSiteFields(request, errors);
        var contract = await ValidateSiteContractAsync(domainContext, request, requireContract, errors, cancellationToken);
        await ValidateSiteCompanyAsync(domainContext, request.CompanyId, errors, cancellationToken);
        return contract;
    }

    // Function summary: Creates a site entity from the normalized mutation request.
    public static Site CreateSite(SiteMutationRequest request)
    {
        var site = new Site
        {
            SiteName = request.SiteName.Trim(),
            AddressLine1 = EmptyToNull(request.AddressLine1),
            AddressLine2 = EmptyToNull(request.AddressLine2),
            Postcode = EmptyToNull(request.Postcode),
            City = EmptyToNull(request.City),
            County = EmptyToNull(request.County),
            StartTime = ParseOptionalTime(request.StartTime),
            EndTime = ParseOptionalTime(request.EndTime),
            SatStartTime = ParseOptionalTime(request.SatStartTime),
            SatEndTime = ParseOptionalTime(request.SatEndTime),
            SunStartTime = ParseOptionalTime(request.SunStartTime),
            SunEndTime = ParseOptionalTime(request.SunEndTime),
            CreateDate = DateTime.UtcNow,
            Contracts = [],
            OperatingHours = BuildSiteOperatingHours(request)
        };
        return site;
    }

    // Function summary: Applies mutable site fields and operating-hours rows.
    public static void ApplySiteMutation(Site site, SiteMutationRequest request)
    {
        site.SiteName = request.SiteName.Trim();
        site.AddressLine1 = EmptyToNull(request.AddressLine1);
        site.AddressLine2 = EmptyToNull(request.AddressLine2);
        site.Postcode = EmptyToNull(request.Postcode);
        site.City = EmptyToNull(request.City);
        site.County = EmptyToNull(request.County);
        site.StartTime = ParseOptionalTime(request.StartTime);
        site.EndTime = ParseOptionalTime(request.EndTime);
        site.SatStartTime = ParseOptionalTime(request.SatStartTime);
        site.SatEndTime = ParseOptionalTime(request.SatEndTime);
        site.SunStartTime = ParseOptionalTime(request.SunStartTime);
        site.SunEndTime = ParseOptionalTime(request.SunEndTime);
        site.OperatingHours.Clear();
        foreach (var hours in BuildSiteOperatingHours(request))
        {
            hours.SiteId = site.Id;
            site.OperatingHours.Add(hours);
        }
    }

    // Function summary: Evaluates site visibility for a command actor.
    public static async Task<bool> CanReadSiteAsync(
        RVTDbContext domainContext,
        Guid siteId,
        SiteMutationCommandActor actor,
        CancellationToken cancellationToken)
    {
        if (actor.IsAdmin)
        {
            return await domainContext.Sites.AnyAsync(site => site.Id == siteId, cancellationToken);
        }

        return actor.UserId.HasValue &&
            await domainContext.SiteUsers.AnyAsync(
                siteUser => siteUser.SiteId == siteId && siteUser.UserId == actor.UserId.Value,
                cancellationToken);
    }

    // Function summary: Parses optional HH:mm time values.
    public static TimeSpan? ParseOptionalTime(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : TimeSpan.Parse(value, CultureInfo.InvariantCulture);
    }

    // Function summary: Validates a start/end time pair.
    public static bool ValidateTimePair(string key, TimeSpan? start, TimeSpan? end, Dictionary<string, string[]> errors)
    {
        if (start.HasValue != end.HasValue)
        {
            AddError(errors, key, "You need to set both start and end time");
            return false;
        }
        if (!start.HasValue)
        {
            return true;
        }

        if (start.Value >= end.GetValueOrDefault())
        {
            AddError(errors, key, "Start time needs to be before end time");
            return false;
        }

        return true;
    }

    // Function summary: Evaluates site name uniqueness and shape.
    private static async Task ValidateSiteNameAsync(
        RVTDbContext domainContext,
        SiteMutationRequest request,
        Guid? currentId,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        var siteName = request.SiteName?.Trim();
        if (string.IsNullOrWhiteSpace(siteName))
        {
            AddError(errors, nameof(SiteMutationRequest.SiteName), "The Site Name is required");
            return;
        }

        if (siteName.Length > 100)
        {
            AddError(errors, nameof(SiteMutationRequest.SiteName), "Site name must be 100 characters or fewer.");
            return;
        }

        if (await domainContext.Sites.AnyAsync(
            site => site.Id != currentId && site.SiteName == siteName,
            cancellationToken))
        {
            AddError(errors, nameof(SiteMutationRequest.SiteName), "The Site Name is already registered");
        }
    }

    // Function summary: Evaluates scalar site fields and operating-hours shape.
    private static void ValidateSiteFields(SiteMutationRequest request, Dictionary<string, string[]> errors)
    {
        ValidateMaxLength(nameof(SiteMutationRequest.AddressLine1), request.AddressLine1, 100, errors);
        ValidateMaxLength(nameof(SiteMutationRequest.AddressLine2), request.AddressLine2, 100, errors);
        ValidateMaxLength(nameof(SiteMutationRequest.Postcode), request.Postcode, 10, errors);
        ValidateMaxLength(nameof(SiteMutationRequest.City), request.City, 30, errors);
        ValidateMaxLength(nameof(SiteMutationRequest.County), request.County, 30, errors);
        ValidateTimePair(nameof(SiteMutationRequest.StartTime), ParseOptionalTime(request.StartTime), ParseOptionalTime(request.EndTime), errors);
        ValidateTimePair(nameof(SiteMutationRequest.SatStartTime), ParseOptionalTime(request.SatStartTime), ParseOptionalTime(request.SatEndTime), errors);
        ValidateTimePair(nameof(SiteMutationRequest.SunStartTime), ParseOptionalTime(request.SunStartTime), ParseOptionalTime(request.SunEndTime), errors);
        ValidateOperatingHours(request, errors);
    }

    // Function summary: Evaluates selected contract validity.
    private static async Task<Contract?> ValidateSiteContractAsync(
        RVTDbContext domainContext,
        SiteMutationRequest request,
        bool requireContract,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        if (!requireContract)
        {
            return null;
        }

        if (!request.ContractId.HasValue || request.ContractId.Value == Guid.Empty)
        {
            AddError(errors, nameof(SiteMutationRequest.ContractId), "The Contract is Required");
            return null;
        }

        var contract = await domainContext.Contracts.SingleOrDefaultAsync(
            item => item.Id == request.ContractId.Value,
            cancellationToken);
        if (contract == null)
        {
            AddError(errors, nameof(SiteMutationRequest.ContractId), "The Contract is Required");
            return null;
        }

        if (contract.CompanyId != request.CompanyId)
        {
            AddError(errors, nameof(SiteMutationRequest.ContractId), "The Contract must belong to the selected company.");
            return null;
        }

        if (contract.SiteiD.HasValue)
        {
            AddError(errors, nameof(SiteMutationRequest.ContractId), "The Contract is already assigned to a site.");
            return null;
        }

        return contract;
    }

    // Function summary: Evaluates selected company validity.
    private static async Task ValidateSiteCompanyAsync(
        RVTDbContext domainContext,
        Guid companyId,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        if (!await domainContext.Companies.AnyAsync(company => company.Id == companyId, cancellationToken))
        {
            AddError(errors, nameof(SiteMutationRequest.CompanyId), "The Company is required");
        }
    }

    // Function summary: Validates Monday-Sunday operating-hours mutation data.
    private static void ValidateOperatingHours(SiteMutationRequest request, Dictionary<string, string[]> errors)
    {
        if (request.OperatingHours is not { Count: > 0 })
        {
            return;
        }

        var seenDays = new HashSet<int>();
        foreach (var hours in request.OperatingHours)
        {
            var key = $"{nameof(SiteMutationRequest.OperatingHours)}[{hours.DayOfWeek}]";
            if (hours.DayOfWeek is < 1 or > 7 || !seenDays.Add(hours.DayOfWeek))
            {
                AddError(errors, key, "Operating hours must use unique days from 1 to 7.");
                continue;
            }

            if (hours.IsClosed)
            {
                continue;
            }

            TimeSpan? start;
            TimeSpan? end;
            try
            {
                start = ParseOptionalTime(hours.StartTime);
                end = ParseOptionalTime(hours.EndTime);
            }
            catch (FormatException)
            {
                AddError(errors, key, "Operating hours must use HH:mm time values.");
                continue;
            }

            ValidateTimePair(key, start, end, errors);
        }
    }

    // Function summary: Builds normalized per-day operating-hours entities from new or legacy request values.
    private static List<SiteOperatingHours> BuildSiteOperatingHours(SiteMutationRequest request)
    {
        return NormalizeOperatingHours(request)
            .Select(hours => new SiteOperatingHours
            {
                DayOfWeek = hours.DayOfWeek,
                StartTime = hours.IsClosed ? null : ParseOptionalTime(hours.StartTime),
                EndTime = hours.IsClosed ? null : ParseOptionalTime(hours.EndTime),
                IsClosed = hours.IsClosed || (string.IsNullOrWhiteSpace(hours.StartTime) && string.IsNullOrWhiteSpace(hours.EndTime))
            })
            .ToList();
    }

    // Function summary: Normalizes request hours to a full seven-day schedule.
    private static List<SiteOperatingHoursMutationRequest> NormalizeOperatingHours(SiteMutationRequest request)
    {
        var supplied = request.OperatingHours is { Count: > 0 }
            ? request.OperatingHours
            : LegacyOperatingHours(request);
        var byDay = supplied
            .Where(hours => hours.DayOfWeek is >= 1 and <= 7)
            .GroupBy(hours => hours.DayOfWeek)
            .ToDictionary(group => group.Key, group => group.First());
        return Enumerable.Range(1, 7)
            .Select(day => byDay.TryGetValue(day, out var hours)
                ? hours
                : new SiteOperatingHoursMutationRequest { DayOfWeek = day, IsClosed = true })
            .ToList();
    }

    // Function summary: Maps legacy grouped weekday/Saturday/Sunday request fields into daily rows.
    private static List<SiteOperatingHoursMutationRequest> LegacyOperatingHours(SiteMutationRequest request)
    {
        return
        [
            new() { DayOfWeek = 1, StartTime = request.StartTime, EndTime = request.EndTime, IsClosed = IsLegacyHoursClosed(request.StartTime, request.EndTime) },
            new() { DayOfWeek = 2, StartTime = request.StartTime, EndTime = request.EndTime, IsClosed = IsLegacyHoursClosed(request.StartTime, request.EndTime) },
            new() { DayOfWeek = 3, StartTime = request.StartTime, EndTime = request.EndTime, IsClosed = IsLegacyHoursClosed(request.StartTime, request.EndTime) },
            new() { DayOfWeek = 4, StartTime = request.StartTime, EndTime = request.EndTime, IsClosed = IsLegacyHoursClosed(request.StartTime, request.EndTime) },
            new() { DayOfWeek = 5, StartTime = request.StartTime, EndTime = request.EndTime, IsClosed = IsLegacyHoursClosed(request.StartTime, request.EndTime) },
            new() { DayOfWeek = 6, StartTime = request.SatStartTime, EndTime = request.SatEndTime, IsClosed = IsLegacyHoursClosed(request.SatStartTime, request.SatEndTime) },
            new() { DayOfWeek = 7, StartTime = request.SunStartTime, EndTime = request.SunEndTime, IsClosed = IsLegacyHoursClosed(request.SunStartTime, request.SunEndTime) }
        ];
    }

    // Function summary: Evaluates whether grouped legacy operating-hours fields represent a closed day.
    private static bool IsLegacyHoursClosed(string? startTime, string? endTime)
    {
        return string.IsNullOrWhiteSpace(startTime) && string.IsNullOrWhiteSpace(endTime);
    }

    // Function summary: Handles the empty to null workflow for this module.
    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    // Function summary: Evaluates max length for the current decision point.
    private static void ValidateMaxLength(string key, string? value, int maxLength, Dictionary<string, string[]> errors)
    {
        if (value?.Length > maxLength)
        {
            AddError(errors, key, $"{key} must be {maxLength} characters or fewer.");
        }
    }

    // Function summary: Appends a validation error to a command result.
    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out var existing)
            ? [.. existing, message]
            : [message];
    }
}
