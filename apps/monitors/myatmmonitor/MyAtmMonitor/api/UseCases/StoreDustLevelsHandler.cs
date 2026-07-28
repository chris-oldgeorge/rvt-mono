using Microsoft.Extensions.Logging;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace MyAtm.Api.UseCases;

// Fetches and atomically stores new dust pages; delivery is handled by the independent outbox job.
public sealed class StoreDustLevelsHandler(
    MyAtmHttpGateway gateway,
    MyAtmMonitorReader monitorReader,
    IMyAtmRuleQueries ruleQueries,
    IMyAtmDustImportCommands dustImportCommands,
    IMyAtmOperationalCommands operationalCommands,
    MyAtmRuleEvaluator ruleEvaluator,
    TimeProvider timeProvider,
    int maxPagesPerMonitorPerRun)
{
    private readonly MyAtmHttpGateway gateway = gateway;
    private readonly MyAtmMonitorReader monitorReader = monitorReader;
    private readonly IMyAtmRuleQueries ruleQueries = ruleQueries;
    private readonly IMyAtmDustImportCommands dustImportCommands = dustImportCommands;
    private readonly IMyAtmOperationalCommands operationalCommands = operationalCommands;
    private readonly MyAtmRuleEvaluator ruleEvaluator = ruleEvaluator;
    private readonly TimeProvider timeProvider = timeProvider;
    private readonly int maxPagesPerMonitorPerRun = maxPagesPerMonitorPerRun;

    public async Task RunAsync<T>(
        int customerId,
        Period period,
        CancellationToken cancellationToken = default) where T : BaseDeviceMeasurement
    {
        List<DustMonitorDto> monitors = monitorReader.ReadMonitors(customerId) ?? [];
        MyAtmFailureCollector failures = new(operationalCommands);
        foreach (DustMonitorDto monitor in monitors)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                DateTime cursor = DateTimeUtil.AsUtc(monitor.GetLastDataTime(period) ?? MyAtmApi.JAN1_1970);
                for (int pageNumber = 0; pageNumber < maxPagesPerMonitorPerRun; pageNumber++)
                {
                    MyAtmMeasurementPage<T> page = await gateway.HttpGetDeviceMeasurementPageAsync<T>(
                        customerId,
                        monitor.SerialId,
                        cursor,
                        period,
                        cancellationToken);
                    List<DustDto> dtos = [.. page.Measurements.Select(measurement => new DustDto(monitor.SerialId, measurement))];
                    RvtLogger.Logger.LogInformation(
                        "StoreDustLevels page={PageNumber} count={Count} serialId={SerialId} cursor={Cursor}",
                        pageNumber + 1,
                        dtos.Count,
                        monitor.SerialId,
                        cursor);

                    if (dtos.Count > 0)
                    {
                        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
                        DateTime pageWatermark = DateTimeUtil.AsUtc(page.NextCursor!.Value);
                        List<RvtAlertRuleDto> rules = ruleQueries.ReadRules(monitor.SerialId, period) ?? [];
                        MyAtmRuleEvaluation evaluation = ruleEvaluator.Evaluate(monitor, period, rules, dtos, utcNow);
                        IReadOnlyList<Rvt.Monitor.Common.Rules.RvtContactDto> contacts =
                            evaluation.AlertOccurrences.Count == 0
                                ? Array.Empty<Rvt.Monitor.Common.Rules.RvtContactDto>()
                                : ruleQueries.ReadAlertContacts(monitor.Id);
                        List<AlertOccurrenceProposal> occurrences = [.. evaluation.AlertOccurrences.Select(proposal => proposal with { Contacts = contacts })];
                        await dustImportCommands.CommitDustImportAsync(
                            new MyAtmDustImportCommit(
                                monitor,
                                period,
                                dtos,
                                pageWatermark,
                                evaluation.RuleStateMutations,
                                occurrences,
                                utcNow),
                            cancellationToken);
                    }

                    if (!page.HasMore || !page.NextCursor.HasValue || page.NextCursor <= cursor)
                    {
                        break;
                    }

                    cursor = DateTimeUtil.AsUtc(page.NextCursor.Value);
                }
            }
            catch (Exception exception)
            {
                TryLogFailure(exception, period, monitor.SerialId);
                failures.Capture(
                    $"StoreDustLevels SerialId={monitor.SerialId}",
                    exception,
                    cancellationToken);
            }
        }

        failures.ThrowIfAny("StoreDustLevels");
    }

    private static void TryLogFailure(Exception exception, Period period, string serialId)
    {
        try
        {
            RvtLogger.Logger.LogError(
                exception,
                "StoreDustLevels failed for period={Period} serialId={SerialId}",
                period,
                serialId);
        }
        catch
        {
            // Operational recording and the final aggregate remain authoritative.
        }
    }
}
