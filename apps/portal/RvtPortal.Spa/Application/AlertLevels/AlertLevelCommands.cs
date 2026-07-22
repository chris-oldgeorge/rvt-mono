// File summary: Handles transactional CQRS commands for alert-level mutation workflows.
// Major updates:
// - 2026-07-09 pending Reused application-layer alert-level helpers instead of controller static methods.
// - 2026-06-26 pending Moved alert-level writes behind MediatR transactional commands.

using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using RVT.BusinessLogic;
using RVT.BusinessLogic.Ports.Vendors;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.AlertLevels;

public sealed record CreateAlertLevelCommand(AlertLevelMutationRequest Request)
    : IRequest<AlertLevelCommandResult>, ITransactionalRequest;

public sealed record UpdateAlertLevelCommand(Guid AlertLevelId, AlertLevelMutationRequest Request)
    : IRequest<AlertLevelCommandResult>, ITransactionalRequest;

public sealed record UpdateVibrationAlertLevelsCommand(Guid MonitorId, VibrationAlertLevelMutationRequest Request)
    : IRequest<VibrationAlertLevelCommandResult>, ITransactionalRequest;

public sealed record DeleteAlertLevelCommand(Guid AlertLevelId)
    : IRequest<AlertLevelCommandResult>, ITransactionalRequest;

public sealed class AlertLevelCommandResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public Guid? AlertLevelId { get; set; }
    public AlertLevelItem? Item { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class VibrationAlertLevelCommandResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public VibrationAlertLevelResponse? Response { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class CreateAlertLevelCommandHandler : IRequestHandler<CreateAlertLevelCommand, AlertLevelCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional alert-level create command handler.
    public CreateAlertLevelCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Creates a non-vibration alert level after validating monitor-specific rules.
    public async Task<AlertLevelCommandResult> Handle(CreateAlertLevelCommand request, CancellationToken cancellationToken)
    {
        var result = new AlertLevelCommandResult();
        var monitor = await domainContext.MonitorsList.SingleOrDefaultAsync(item => item.Id == request.Request.MonitorId, cancellationToken);
        if (monitor == null)
        {
            result.NotFound = true;
            return result;
        }

        if (monitor.TypeOfMonitor == MonitorTypeEnum.Vibration)
        {
            AddError(result.Errors, nameof(AlertLevelMutationRequest.MonitorId), "Use the vibration alert-level endpoint for vibration monitors.");
            return result;
        }

        if (!AlertLevelCommandWorkflow.ValidateAlertLevelMutation(monitor, request.Request, result.Errors, out var alertType, out var start, out var end))
        {
            return result;
        }

        var level = new Alertlevel
        {
            MonitorId = monitor.Id,
            SerialId = monitor.SerialId,
            AlertField = AlertLevelWorkflow.NormalizeAlertField(monitor, request.Request),
            AlertType = alertType,
            AveragingPeriod = request.Request.AveragingPeriod,
            LimitOn = request.Request.LimitOn,
            LimitOff = request.Request.LimitOff,
            Weekdays = request.Request.Weekdays,
            Saturdays = request.Request.Saturdays,
            Sundays = request.Request.Sundays,
            StartTime = start,
            EndTime = end,
            IsActive = false,
            IsDeleted = false
        };
        AlertLevelWorkflow.NormalizeNoiseSiteHours(monitor, level);
        domainContext.RvtAlertRules.Add(level);
        result.AlertLevelId = level.Id;
        result.Item = AlertLevelWorkflow.BuildAlertLevelItem(level);
        return result;
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string message) =>
        AlertLevelCommandWorkflow.AddError(errors, key, message);
}

public sealed class UpdateAlertLevelCommandHandler : IRequestHandler<UpdateAlertLevelCommand, AlertLevelCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional alert-level update command handler.
    public UpdateAlertLevelCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Updates a non-vibration alert level after validating monitor-specific rules.
    public async Task<AlertLevelCommandResult> Handle(UpdateAlertLevelCommand request, CancellationToken cancellationToken)
    {
        var result = new AlertLevelCommandResult { AlertLevelId = request.AlertLevelId };
        var level = await domainContext.RvtAlertRules.SingleOrDefaultAsync(
            item => item.Id == request.AlertLevelId && !item.IsDeleted,
            cancellationToken);
        if (level == null)
        {
            result.NotFound = true;
            return result;
        }

        var monitor = await domainContext.MonitorsList.SingleAsync(item => item.Id == level.MonitorId, cancellationToken);
        if (monitor.TypeOfMonitor == MonitorTypeEnum.Vibration)
        {
            AlertLevelCommandWorkflow.AddError(result.Errors, nameof(AlertLevelMutationRequest.MonitorId), "Use the vibration alert-level endpoint for vibration monitors.");
            return result;
        }

        request.Request.MonitorId = monitor.Id;
        if (!AlertLevelCommandWorkflow.ValidateAlertLevelMutation(monitor, request.Request, result.Errors, out var alertType, out var start, out var end))
        {
            return result;
        }

        level.AlertField = AlertLevelWorkflow.NormalizeAlertField(monitor, request.Request);
        level.AlertType = alertType;
        level.AveragingPeriod = request.Request.AveragingPeriod;
        level.LimitOn = request.Request.LimitOn;
        level.LimitOff = request.Request.LimitOff;
        level.Weekdays = request.Request.Weekdays;
        level.Saturdays = request.Request.Saturdays;
        level.Sundays = request.Request.Sundays;
        level.StartTime = start;
        level.EndTime = end;
        AlertLevelWorkflow.NormalizeNoiseSiteHours(monitor, level);
        result.Item = AlertLevelWorkflow.BuildAlertLevelItem(level);
        return result;
    }
}

public sealed class UpdateVibrationAlertLevelsCommandHandler
    : IRequestHandler<UpdateVibrationAlertLevelsCommand, VibrationAlertLevelCommandResult>
{
    private readonly RVTDbContext domainContext;
    private readonly IVibrationVendorGateway vibrationVendorGateway;
    private readonly IWebHostEnvironment environment;

    // Function summary: Initializes the transactional vibration alert-level upsert command handler.
    public UpdateVibrationAlertLevelsCommandHandler(
        RVTDbContext domainContext,
        IVibrationVendorGateway vibrationVendorGateway,
        IWebHostEnvironment environment)
    {
        this.domainContext = domainContext;
        this.vibrationVendorGateway = vibrationVendorGateway;
        this.environment = environment;
    }

    // Function summary: Upserts the vibration alert/caution pair and synchronizes the external service when enabled.
    public async Task<VibrationAlertLevelCommandResult> Handle(
        UpdateVibrationAlertLevelsCommand request,
        CancellationToken cancellationToken)
    {
        var result = new VibrationAlertLevelCommandResult();
        var monitor = await domainContext.MonitorsList.SingleOrDefaultAsync(item => item.Id == request.MonitorId, cancellationToken);
        if (monitor == null)
        {
            result.NotFound = true;
            return result;
        }

        if (monitor.TypeOfMonitor != MonitorTypeEnum.Vibration)
        {
            AlertLevelCommandWorkflow.AddError(result.Errors, nameof(request.MonitorId), "The selected monitor is not a vibration monitor.");
        }
        if (request.Request.AlertLevel <= 0 || request.Request.CautionLevel <= 0)
        {
            AlertLevelCommandWorkflow.AddError(result.Errors, nameof(VibrationAlertLevelMutationRequest.AlertLevel), "Alert and caution levels must be greater than zero.");
        }
        if (request.Request.AlertLevel <= request.Request.CautionLevel)
        {
            AlertLevelCommandWorkflow.AddError(result.Errors, nameof(VibrationAlertLevelMutationRequest.AlertLevel), "Alert Level needs to be greater than Caution Level.");
        }
        if (result.Errors.Count > 0)
        {
            return result;
        }

        var externalAttempted = !environment.IsEnvironment("Testing");
        var externalSucceeded = false;
        if (externalAttempted)
        {
            var vendorSync = await vibrationVendorGateway.UpdateAlertLevelsAsync(
                monitor.SerialId,
                request.Request.AlertLevel,
                request.Request.CautionLevel,
                cancellationToken);
            externalSucceeded = vendorSync.Succeeded;
            if (!externalSucceeded)
            {
                AlertLevelCommandWorkflow.AddError(result.Errors, nameof(VibrationAlertLevelMutationRequest.AlertLevel), $"Alert Level API call failed: {vendorSync.Error}");
                return result;
            }
        }

        var levels = await domainContext.RvtAlertRules
            .Where(level => level.MonitorId == request.MonitorId && !level.IsDeleted)
            .ToListAsync(cancellationToken);
        if (levels.Count is not 0 and not 2)
        {
            AlertLevelCommandWorkflow.AddError(result.Errors, nameof(request.MonitorId), "Inconsistent number of vibration alert levels; expected zero or two.");
            return result;
        }

        if (levels.Count == 0)
        {
            levels.Add(AlertLevelWorkflow.BuildVibrationLevel(monitor, AlertTypeEnum.Alert, request.Request.AlertLevel, 8));
            levels.Add(AlertLevelWorkflow.BuildVibrationLevel(monitor, AlertTypeEnum.Caution, request.Request.CautionLevel, 5));
            domainContext.RvtAlertRules.AddRange(levels);
        }
        else
        {
            levels.Single(level => level.AlertType == AlertTypeEnum.Alert).LimitOn = request.Request.AlertLevel;
            levels.Single(level => level.AlertType == AlertTypeEnum.Caution).LimitOn = request.Request.CautionLevel;
        }

        result.Response = new VibrationAlertLevelResponse
        {
            MonitorId = monitor.Id,
            SerialId = monitor.SerialId,
            AlertLevel = request.Request.AlertLevel,
            CautionLevel = request.Request.CautionLevel,
            ExternalSyncAttempted = externalAttempted,
            ExternalSyncSucceeded = externalSucceeded,
            AlertLevels = levels.OrderBy(level => level.AlertType)
                .Select(level => AlertLevelWorkflow.BuildAlertLevelItem(level, monitor.TypeOfMonitor))
                .ToList()
        };
        return result;
    }
}

public sealed class DeleteAlertLevelCommandHandler : IRequestHandler<DeleteAlertLevelCommand, AlertLevelCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional alert-level delete command handler.
    public DeleteAlertLevelCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Soft-deletes an alert level and disables it.
    public async Task<AlertLevelCommandResult> Handle(DeleteAlertLevelCommand request, CancellationToken cancellationToken)
    {
        var result = new AlertLevelCommandResult { AlertLevelId = request.AlertLevelId };
        var level = await domainContext.RvtAlertRules.SingleOrDefaultAsync(
            item => item.Id == request.AlertLevelId && !item.IsDeleted,
            cancellationToken);
        if (level == null)
        {
            result.NotFound = true;
            return result;
        }

        level.IsDeleted = true;
        level.IsActive = false;
        return result;
    }
}

internal static class AlertLevelCommandWorkflow
{
    // Function summary: Evaluates alert-level mutation fields for monitor-specific constraints.
    public static bool ValidateAlertLevelMutation(
        MonitorEntity monitor,
        AlertLevelMutationRequest request,
        Dictionary<string, string[]> errors,
        out AlertTypeEnum alertType,
        out TimeSpan? start,
        out TimeSpan? end)
    {
        start = null;
        end = null;
        var isValid = TryValidateAlertType(request, errors, out alertType);
        isValid &= ValidateLimits(monitor, request, errors);
        isValid &= ValidateAlertField(monitor, request, errors);
        isValid &= ValidateAveragingPeriod(monitor, request, errors);
        if (monitor.TypeOfMonitor == MonitorTypeEnum.Noise && request.AveragingPeriod == (int)AveragingPeriodsNoiseEnum._Site_hours)
        {
            return isValid && errors.Count == 0;
        }

        isValid &= ValidateSelectedDays(request, errors);
        isValid &= ValidateNoiseTimeWindow(monitor, request, errors, out start, out end);
        return isValid && errors.Count == 0;
    }

    // Function summary: Appends a validation error to a command result.
    public static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out var existing)
            ? [.. existing, message]
            : [message];
    }

    private static bool TryValidateAlertType(AlertLevelMutationRequest request, Dictionary<string, string[]> errors, out AlertTypeEnum alertType)
    {
        if (AlertLevelWorkflow.TryParseAlertType(request.AlertType, out alertType))
        {
            return true;
        }

        AddError(errors, nameof(AlertLevelMutationRequest.AlertType), "Please select an alert type.");
        return false;
    }

    private static bool ValidateLimits(MonitorEntity monitor, AlertLevelMutationRequest request, Dictionary<string, string[]> errors)
    {
        if (request.LimitOn <= 0 || request.LimitOff <= 0)
        {
            AddError(errors, nameof(AlertLevelMutationRequest.LimitOn), "Limits must be greater than zero.");
            return false;
        }

        if (monitor.TypeOfMonitor == MonitorTypeEnum.Noise && (request.LimitOn < 1 || request.LimitOff < 1))
        {
            AddError(errors, nameof(AlertLevelMutationRequest.LimitOn), "Noise limits must be at least 1.");
            return false;
        }

        if (request.LimitOn > request.LimitOff)
        {
            return true;
        }

        AddError(errors, nameof(AlertLevelMutationRequest.LimitOn), "Limit On needs to be greater than Limit Off.");
        return false;
    }

    private static bool ValidateAlertField(MonitorEntity monitor, AlertLevelMutationRequest request, Dictionary<string, string[]> errors)
    {
        var validFields = AlertLevelWorkflow.BuildFieldOptions(monitor.TypeOfMonitor)
            .Select(item => item.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var alertField = AlertLevelWorkflow.NormalizeAlertField(monitor, request);
        if (!string.IsNullOrWhiteSpace(alertField) && validFields.Contains(alertField))
        {
            return true;
        }

        AddError(errors, nameof(AlertLevelMutationRequest.AlertField), "Please select a valid parameter.");
        return false;
    }

    private static bool ValidateAveragingPeriod(MonitorEntity monitor, AlertLevelMutationRequest request, Dictionary<string, string[]> errors)
    {
        var validPeriods = AlertLevelWorkflow.BuildAveragingPeriodOptions(monitor.TypeOfMonitor)
            .Select(item => int.Parse(item.Value, CultureInfo.InvariantCulture))
            .ToHashSet();
        if (validPeriods.Contains(request.AveragingPeriod))
        {
            return true;
        }

        AddError(errors, nameof(AlertLevelMutationRequest.AveragingPeriod), "Please select a valid averaging period.");
        return false;
    }

    private static bool ValidateSelectedDays(AlertLevelMutationRequest request, Dictionary<string, string[]> errors)
    {
        if (request.Weekdays || request.Saturdays || request.Sundays)
        {
            return true;
        }

        AddError(errors, nameof(AlertLevelMutationRequest.Weekdays), "Select at least one of Weekdays, Saturday or Sundays.");
        return false;
    }

    private static bool ValidateNoiseTimeWindow(
        MonitorEntity monitor,
        AlertLevelMutationRequest request,
        Dictionary<string, string[]> errors,
        out TimeSpan? start,
        out TimeSpan? end)
    {
        start = null;
        end = null;
        if (monitor.TypeOfMonitor != MonitorTypeEnum.Noise)
        {
            return true;
        }

        if (!AlertLevelWorkflow.TryParseOptionalTime(request.StartTime, out start) ||
            !AlertLevelWorkflow.TryParseOptionalTime(request.EndTime, out end))
        {
            AddError(errors, nameof(AlertLevelMutationRequest.StartTime), "Start and end time must use HH:mm format.");
            return false;
        }

        if (!start.HasValue || !end.HasValue)
        {
            AddError(errors, nameof(AlertLevelMutationRequest.StartTime), "Start and end time are required for noise alert levels.");
            return false;
        }

        if (start.Value < end.Value)
        {
            return true;
        }

        AddError(errors, nameof(AlertLevelMutationRequest.EndTime), "End Time needs to be after the Start Time.");
        return false;
    }
}
