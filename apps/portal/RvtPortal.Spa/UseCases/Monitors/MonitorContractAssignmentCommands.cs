// File summary: Handles transactional CQRS commands for monitor contract assignment workflows.
// Major updates:
// - 2026-06-25 pending Moved monitor contract attach/remove mutations behind MediatR transactional commands.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.UseCases.Common;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.UseCases.Monitors;

public sealed record AssignMonitorToContractCommand(Guid MonitorId, Guid ContractId)
    : IRequest<AssignMonitorToContractResult>, ITransactionalRequest;

public sealed class AssignMonitorToContractResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public MonitorEntity? Monitor { get; set; }
    public Guid? DeploymentId { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class AssignMonitorToContractCommandHandler
    : IRequestHandler<AssignMonitorToContractCommand, AssignMonitorToContractResult>
{
    private readonly RVTDbContext _domainContext;
    private readonly TimeProvider _timeProvider;

    // Function summary: Initializes the transactional monitor assignment command handler.
    public AssignMonitorToContractCommandHandler(RVTDbContext domainContext, TimeProvider timeProvider)
    {
        _domainContext = domainContext;
        _timeProvider = timeProvider;
    }

    // Function summary: Validates and creates the current deployment for a monitor contract assignment.
    public async Task<AssignMonitorToContractResult> Handle(
        AssignMonitorToContractCommand request,
        CancellationToken cancellationToken)
    {
        AssignMonitorToContractResult result = new();
        MonitorEntity? monitor = await _domainContext.MonitorsList
            .SingleOrDefaultAsync(item => item.Id == request.MonitorId && !item.Archived, cancellationToken);
        if (monitor == null)
        {
            result.NotFound = true;
            return result;
        }

        result.Monitor = monitor;
        if (string.IsNullOrWhiteSpace(monitor.FleetNr))
        {
            AddError(result.Errors, nameof(MonitorEntity.FleetNr), "A fleet number is required before assigning a monitor to a contract.");
        }

        Contract? contract = await _domainContext.Contracts.SingleOrDefaultAsync(item => item.Id == request.ContractId, cancellationToken);
        if (contract == null)
        {
            AddError(result.Errors, nameof(MonitorAssignmentRequest.ContractId), "Please select a contract.");
        }
        else if (!contract.SiteiD.HasValue)
        {
            AddError(result.Errors, nameof(MonitorAssignmentRequest.ContractId), "The contract must be assigned to a site before monitor deployment.");
        }

        if (await _domainContext.Deployments.AnyAsync(
            item => item.MonitorId == request.MonitorId && item.EndDate == null,
            cancellationToken))
        {
            AddError(result.Errors, "id", "Monitor already assigned to a contract.");
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        Deployment deployment = new()
        {
            Id = Guid.NewGuid(),
            ContractId = request.ContractId,
            MonitorId = request.MonitorId,
            StartDate = _timeProvider.GetUtcNow().UtcDateTime
        };
        _domainContext.Deployments.Add(deployment);
        result.DeploymentId = deployment.Id;
        return result;
    }

    // Function summary: Appends a validation error to a command result.
    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out string[]? existing)
            ? [.. existing, message]
            : [message];
    }
}

public sealed record RemoveMonitorFromContractCommand(Guid MonitorId)
    : IRequest<RemoveMonitorFromContractResult>, ITransactionalRequest;

public sealed class RemoveMonitorFromContractResult : ITransactionOutcome
{
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => Errors.Count == 0;
}

public sealed class RemoveMonitorFromContractCommandHandler
    : IRequestHandler<RemoveMonitorFromContractCommand, RemoveMonitorFromContractResult>
{
    private readonly RVTDbContext _domainContext;
    private readonly IDeploymentMeasurementProbe _measurementProbe;
    private readonly TimeProvider _timeProvider;

    // Function summary: Initializes the transactional monitor unassignment command handler.
    public RemoveMonitorFromContractCommandHandler(
        RVTDbContext domainContext,
        IDeploymentMeasurementProbe measurementProbe,
        TimeProvider timeProvider)
    {
        _domainContext = domainContext;
        _measurementProbe = measurementProbe;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Ends the active deployment, or deletes it outright when it owns no data.
    /// <para>
    /// PRODUCT RULING (§4.2, 2026-07-30): the undo affordance for a mis-assignment stays, but it is conditioned
    /// on <em>no measurement having arrived</em> inside the deployment's ownership window - not on the
    /// deployment being under an hour old, which is what this used to test. That rule made any data that had
    /// already landed permanently unattributable (the deployment row is the only link from a measurement's
    /// serial and timestamp to a contract and site) while also refusing to undo an obvious mis-assignment
    /// noticed 61 minutes later. The one-hour value had no explanation and is gone.
    /// </para>
    /// </summary>
    public async Task<RemoveMonitorFromContractResult> Handle(
        RemoveMonitorFromContractCommand request,
        CancellationToken cancellationToken)
    {
        RemoveMonitorFromContractResult result = new();
        Deployment? deployment = await _domainContext.Deployments
            .Include(item => item.Monitor)
            .Include(item => item.Contract)
            .SingleOrDefaultAsync(
                item => item.MonitorId == request.MonitorId && item.EndDate == null,
                cancellationToken);
        if (deployment == null)
        {
            AddError(result.Errors, "id", "Monitor not assigned to a contract.");
            return result;
        }

        bool ownsMeasurements = await _measurementProbe.HasMeasurementsAsync(
            deployment.Monitor?.SerialId ?? "",
            MonitorOwnershipWindowResolver.ForDeployment(deployment),
            cancellationToken);
        if (ownsMeasurements)
        {
            deployment.EndDate = _timeProvider.GetUtcNow().UtcDateTime;
        }
        else
        {
            _domainContext.Deployments.Remove(deployment);
        }

        return result;
    }

    // Function summary: Appends a validation error to a command result.
    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out string[]? existing)
            ? [.. existing, message]
            : [message];
    }
}
