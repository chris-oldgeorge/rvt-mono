// File summary: Handles transactional CQRS commands for unattached monitor removal workflows.
// Major updates:
// - 2026-06-25 pending Moved unattached monitor archive/delete decisions behind a MediatR transactional command.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.UseCases.Common;

namespace RvtPortal.Spa.UseCases.Monitors;

public sealed record RemoveUnattachedMonitorCommand(Guid MonitorId, string? Reason, string? ArchivedBy)
    : IRequest<RemoveUnattachedMonitorResult>, ITransactionalRequest;

public sealed class RemoveUnattachedMonitorResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public MonitorRemovalResponse? Response { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class RemoveUnattachedMonitorCommandHandler
    : IRequestHandler<RemoveUnattachedMonitorCommand, RemoveUnattachedMonitorResult>
{
    private readonly RVTDbContext _domainContext;
    private readonly IMonitorRemovalImpactReader _impactReader;
    private readonly TimeProvider _timeProvider;

    // Function summary: Initializes the transactional unattached monitor removal command handler.
    public RemoveUnattachedMonitorCommandHandler(
        RVTDbContext domainContext,
        IMonitorRemovalImpactReader impactReader,
        TimeProvider timeProvider)
    {
        _domainContext = domainContext;
        _impactReader = impactReader;
        _timeProvider = timeProvider;
    }

    // Function summary: Archives monitors with historical data and deletes monitors without related data.
    public async Task<RemoveUnattachedMonitorResult> Handle(
        RemoveUnattachedMonitorCommand request,
        CancellationToken cancellationToken)
    {
        RemoveUnattachedMonitorResult result = new();
        RVT.Entities.Monitor? monitor = await _domainContext.MonitorsList.SingleOrDefaultAsync(
            item => item.Id == request.MonitorId && !item.Archived,
            cancellationToken);
        if (monitor == null)
        {
            result.NotFound = true;
            return result;
        }

        if (await _domainContext.Deployments.AnyAsync(
            item => item.MonitorId == request.MonitorId && item.EndDate == null,
            cancellationToken))
        {
            AddError(result.Errors, "id", "Monitor is attached to a site and cannot be removed from this page.");
            return result;
        }

        MonitorRemovalImpactResponse impact = await _impactReader.BuildAsync(request.MonitorId, monitor.SerialId, cancellationToken);
        string monitorName = monitor.FleetNr ?? monitor.SerialId;
        if (impact.HasRelatedData)
        {
            monitor.Archived = true;
            monitor.ArchivedAt = _timeProvider.GetUtcNow().UtcDateTime;
            monitor.ArchivedBy = request.ArchivedBy;
            monitor.ArchiveReason = EmptyToNull(request.Reason);
            result.Response = new MonitorRemovalResponse
            {
                Id = request.MonitorId,
                Action = "archived",
                Impact = impact,
                Message = $"Monitor '{monitorName}' has been archived because related data exists."
            };
            return result;
        }

        _domainContext.MonitorsList.Remove(monitor);
        result.Response = new MonitorRemovalResponse
        {
            Id = request.MonitorId,
            Action = "deleted",
            Impact = impact,
            Message = $"Monitor '{monitorName}' has been deleted."
        };
        return result;
    }

    // Function summary: Appends a validation error to a command result.
    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out string[]? existing)
            ? [.. existing, message]
            : [message];
    }

    // Function summary: Converts blank user input to null before persistence.
    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
