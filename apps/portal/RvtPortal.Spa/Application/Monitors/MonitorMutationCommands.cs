// File summary: Handles transactional CQRS commands for residual monitor mutation workflows.
// Major updates:
// - 2026-06-26 pending Moved monitor update/fleet/default-alert writes behind MediatR transactional commands.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Monitors;

public sealed record UpdateMonitorCommand(Guid MonitorId, MonitorMutationRequest Request)
    : IRequest<MonitorMutationCommandResult>, ITransactionalRequest;

public sealed record SetMonitorFleetNumberCommand(Guid MonitorId, string FleetNumber)
    : IRequest<MonitorMutationCommandResult>, ITransactionalRequest;

public sealed record CreateDefaultMonitorAlertLevelsCommand()
    : IRequest<DefaultMonitorsResponse>, ITransactionalRequest;

public sealed class MonitorMutationCommandResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public Guid? MissingId { get; set; }
    public Guid MonitorId { get; set; }
    public Guid? DeploymentId { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class UpdateMonitorCommandHandler : IRequestHandler<UpdateMonitorCommand, MonitorMutationCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional monitor update command handler.
    public UpdateMonitorCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Updates monitor fields and optional deployment coordinates.
    public async Task<MonitorMutationCommandResult> Handle(UpdateMonitorCommand request, CancellationToken cancellationToken)
    {
        var result = new MonitorMutationCommandResult { MonitorId = request.MonitorId };
        var monitor = await domainContext.MonitorsList.SingleOrDefaultAsync(
            item => item.Id == request.MonitorId && !item.Archived,
            cancellationToken);
        if (monitor == null)
        {
            result.NotFound = true;
            result.MissingId = request.MonitorId;
            return result;
        }

        await MonitorMutationWorkflow.ValidateMonitorMutationAsync(domainContext, request.MonitorId, request.Request, result.Errors, cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        var hadNoFleetNumber = string.IsNullOrWhiteSpace(monitor.FleetNr);
        monitor.FleetNr = EmptyToNull(request.Request.FleetNumber);
        monitor.CalibrationDate = request.Request.CalibrationDate;
        monitor.CalibrationDue = request.Request.CalibrationDue;
        if (hadNoFleetNumber && !string.IsNullOrWhiteSpace(monitor.FleetNr))
        {
            await MonitorMutationWorkflow.AddDefaultAlertLevelsAsync(domainContext, monitor, cancellationToken);
        }

        if (request.Request.DeploymentId.HasValue)
        {
            var deployment = await domainContext.Deployments.SingleOrDefaultAsync(
                item => item.Id == request.Request.DeploymentId.Value && item.MonitorId == request.MonitorId,
                cancellationToken);
            if (deployment == null)
            {
                result.NotFound = true;
                result.MissingId = request.Request.DeploymentId.Value;
                return result;
            }

            deployment.What3words = EmptyToNull(request.Request.What3words);
            deployment.Location = EmptyToNull(request.Request.Location);
            deployment.Lat = request.Request.Lat ?? deployment.Lat;
            deployment.Lng = request.Request.Lng ?? deployment.Lng;
            result.DeploymentId = deployment.Id;
        }

        return result;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SetMonitorFleetNumberCommandHandler
    : IRequestHandler<SetMonitorFleetNumberCommand, MonitorMutationCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional monitor fleet-number command handler.
    public SetMonitorFleetNumberCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Sets a monitor fleet number and creates default alert levels when needed.
    public async Task<MonitorMutationCommandResult> Handle(SetMonitorFleetNumberCommand request, CancellationToken cancellationToken)
    {
        var result = new MonitorMutationCommandResult { MonitorId = request.MonitorId };
        var monitor = await domainContext.MonitorsList.SingleOrDefaultAsync(
            item => item.Id == request.MonitorId && !item.Archived,
            cancellationToken);
        if (monitor == null)
        {
            result.NotFound = true;
            result.MissingId = request.MonitorId;
            return result;
        }

        var mutation = new MonitorMutationRequest { FleetNumber = request.FleetNumber };
        await MonitorMutationWorkflow.ValidateMonitorMutationAsync(domainContext, request.MonitorId, mutation, result.Errors, cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        var wasFirstFleetNumber = string.IsNullOrWhiteSpace(monitor.FleetNr);
        monitor.FleetNr = request.FleetNumber.Trim();
        if (wasFirstFleetNumber)
        {
            await MonitorMutationWorkflow.AddDefaultAlertLevelsAsync(domainContext, monitor, cancellationToken);
        }

        return result;
    }
}

public sealed class CreateDefaultMonitorAlertLevelsCommandHandler
    : IRequestHandler<CreateDefaultMonitorAlertLevelsCommand, DefaultMonitorsResponse>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional default-monitor-alert command handler.
    public CreateDefaultMonitorAlertLevelsCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Creates default alert levels for all monitors with fleet numbers.
    public async Task<DefaultMonitorsResponse> Handle(CreateDefaultMonitorAlertLevelsCommand request, CancellationToken cancellationToken)
    {
        var monitors = await domainContext.MonitorsList
            .Where(monitor => !monitor.Archived && !string.IsNullOrWhiteSpace(monitor.FleetNr))
            .OrderBy(monitor => monitor.FleetNr)
            .ToListAsync(cancellationToken);
        var response = new DefaultMonitorsResponse { Processed = monitors.Count };
        foreach (var monitor in monitors)
        {
            var created = await MonitorMutationWorkflow.AddDefaultAlertLevelsAsync(domainContext, monitor, cancellationToken);
            if (created > 0)
            {
                response.CreatedAlertLevels += created;
                response.MonitorIds.Add(monitor.Id);
            }
        }

        return response;
    }
}

internal static class MonitorMutationWorkflow
{
    private sealed record DefaultAlertLevel(
        string AlertField,
        double LimitOn,
        double LimitOff,
        AlertTypeEnum AlertType,
        int AveragingPeriod,
        TimeSpan? StartTime = null,
        TimeSpan? EndTime = null);

    // Function summary: Validates monitor mutation fields.
    public static async Task ValidateMonitorMutationAsync(
        RVTDbContext domainContext,
        Guid id,
        MonitorMutationRequest request,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        var fleet = EmptyToNull(request.FleetNumber);
        if (fleet?.Length > 32)
        {
            AddError(errors, nameof(MonitorMutationRequest.FleetNumber), "Fleet number must be 32 characters or fewer.");
        }
        if (!string.IsNullOrWhiteSpace(fleet) && await domainContext.MonitorsList.AnyAsync(
            monitor => monitor.Id != id &&
                !monitor.Archived &&
                monitor.FleetNr != null &&
                monitor.FleetNr == fleet,
            cancellationToken))
        {
            AddError(errors, nameof(MonitorMutationRequest.FleetNumber), "The Fleet Nr is already in use");
        }

        ValidateMaxLength(nameof(MonitorMutationRequest.Location), request.Location, 256, errors);
        ValidateMaxLength(nameof(MonitorMutationRequest.What3words), request.What3words, 256, errors);
        ValidateCoordinate(nameof(MonitorMutationRequest.Lat), request.Lat, -90, 90, errors);
        ValidateCoordinate(nameof(MonitorMutationRequest.Lng), request.Lng, -180, 180, errors);
    }

    // Function summary: Registers default alert levels for a monitor if none exist yet.
    public static async Task<int> AddDefaultAlertLevelsAsync(
        RVTDbContext domainContext,
        MonitorEntity monitor,
        CancellationToken cancellationToken)
    {
        if (await domainContext.RvtAlertRules.AnyAsync(level => level.MonitorId == monitor.Id && !level.IsDeleted, cancellationToken))
        {
            return 0;
        }

        Alertlevel[] levels = monitor.TypeOfMonitor switch
        {
            MonitorTypeEnum.Dust =>
            [
                BuildAlertLevel(monitor, new DefaultAlertLevel("pm10", 45, 43, AlertTypeEnum.Alert, (int)AveragingPeriodsDustEnum._1_day)),
                BuildAlertLevel(monitor, new DefaultAlertLevel("pm10", 190, 180, AlertTypeEnum.Alert, (int)AveragingPeriodsDustEnum._1_hour))
            ],
            MonitorTypeEnum.Noise =>
            [
                BuildAlertLevel(
                    monitor,
                    new DefaultAlertLevel(
                        "LAeq",
                        75,
                        70,
                        AlertTypeEnum.Alert,
                        (int)AveragingPeriodsNoiseEnum._1_hour,
                        new TimeSpan(8, 0, 0),
                        new TimeSpan(18, 0, 0)))
            ],
            MonitorTypeEnum.Vibration =>
            [
                BuildAlertLevel(monitor, new DefaultAlertLevel("Peak", 10, 8, AlertTypeEnum.Alert, (int)AveragingPeriodsNoiseEnum._1_hour)),
                BuildAlertLevel(monitor, new DefaultAlertLevel("Peak", 7, 5, AlertTypeEnum.Caution, (int)AveragingPeriodsNoiseEnum._1_hour))
            ],
            _ => []
        };

        domainContext.RvtAlertRules.AddRange(levels);
        return levels.Length;
    }

    private static Alertlevel BuildAlertLevel(MonitorEntity monitor, DefaultAlertLevel level)
    {
        return new Alertlevel
        {
            MonitorId = monitor.Id,
            SerialId = monitor.SerialId,
            AlertField = level.AlertField,
            LimitOn = level.LimitOn,
            LimitOff = level.LimitOff,
            AlertType = level.AlertType,
            IsActive = false,
            AveragingPeriod = level.AveragingPeriod,
            Weekdays = true,
            Saturdays = false,
            Sundays = false,
            StartTime = level.StartTime,
            EndTime = level.EndTime,
            IsDeleted = false
        };
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateMaxLength(string key, string? value, int maxLength, Dictionary<string, string[]> errors)
    {
        if (value?.Length > maxLength)
        {
            AddError(errors, key, $"{key} must be {maxLength} characters or fewer.");
        }
    }

    private static void ValidateCoordinate(string key, double? value, double min, double max, Dictionary<string, string[]> errors)
    {
        if (value.HasValue && (value < min || value > max))
        {
            AddError(errors, key, $"{key} must be between {min} and {max}.");
        }
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out var existing)
            ? [.. existing, message]
            : [message];
    }
}
