// File summary: Handles transactional CQRS commands for monitor contract assignment workflows.
// Major updates:
// - 2026-06-25 pending Moved monitor contract attach/remove mutations behind MediatR transactional commands.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Monitors;

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
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional monitor assignment command handler.
    public AssignMonitorToContractCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Validates and creates the current deployment for a monitor contract assignment.
    public async Task<AssignMonitorToContractResult> Handle(
        AssignMonitorToContractCommand request,
        CancellationToken cancellationToken)
    {
        var result = new AssignMonitorToContractResult();
        var monitor = await domainContext.MonitorsList
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

        var contract = await domainContext.Contracts.SingleOrDefaultAsync(item => item.Id == request.ContractId, cancellationToken);
        if (contract == null)
        {
            AddError(result.Errors, nameof(MonitorAssignmentRequest.ContractId), "Please select a contract.");
        }
        else if (!contract.SiteiD.HasValue)
        {
            AddError(result.Errors, nameof(MonitorAssignmentRequest.ContractId), "The contract must be assigned to a site before monitor deployment.");
        }

        if (await domainContext.Deployments.AnyAsync(
            item => item.MonitorId == request.MonitorId && item.EndDate == null,
            cancellationToken))
        {
            AddError(result.Errors, "id", "Monitor already assigned to a contract.");
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        var deployment = new Deployment
        {
            Id = Guid.NewGuid(),
            ContractId = request.ContractId,
            MonitorId = request.MonitorId,
            StartDate = DateTime.UtcNow
        };
        domainContext.Deployments.Add(deployment);
        result.DeploymentId = deployment.Id;
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
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional monitor unassignment command handler.
    public RemoveMonitorFromContractCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Ends or deletes the active deployment for a monitor contract assignment.
    public async Task<RemoveMonitorFromContractResult> Handle(
        RemoveMonitorFromContractCommand request,
        CancellationToken cancellationToken)
    {
        var result = new RemoveMonitorFromContractResult();
        var deployment = await domainContext.Deployments.SingleOrDefaultAsync(
            item => item.MonitorId == request.MonitorId && item.EndDate == null,
            cancellationToken);
        if (deployment == null)
        {
            AddError(result.Errors, "id", "Monitor not assigned to a contract.");
            return result;
        }

        if (deployment.StartDate > DateTime.UtcNow.AddHours(-1))
        {
            domainContext.Deployments.Remove(deployment);
        }
        else
        {
            deployment.EndDate = DateTime.UtcNow;
        }

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
