// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Diagnostics;

namespace Omnidots.Api.UseCases;

// Summary: The one per-monitor import loop shared by the Omnidots fleet jobs.
// Major updates:
// - 2026-07-29 Hexagonal convergence: extracted from StorePeakRecordsHandler.
//   Veff, Vdv, and Traces previously carried their own loops without the
//   per-monitor cancellation check, so a shutdown mid-fleet was recorded as
//   a monitor failure instead of stopping the import.
internal static class OmnidotsFleetImport
{
    public static async Task<List<OmnidotsMonitorFailure>> RunAsync(
        string jobName,
        IEnumerable<VibrationMonitorDto> monitors,
        IOmnidotsOperationalCommands operationalCommands,
        Func<VibrationMonitorDto, Task> importAsync,
        CancellationToken cancellationToken)
    {
        List<OmnidotsMonitorFailure> failures = [];
        foreach (VibrationMonitorDto monitor in monitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await importAsync(monitor);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                string msg = string.Format("{0} serialId={1}", jobName, monitor.SerialId);
                RvtLogger.Logger.LogError(e, "{JobName} failed for serialId={Value1}", jobName, monitor.SerialId);
                failures.Add(OmnidotsMonitorFailure.Record(
                    monitor.SerialId,
                    e,
                    () => operationalCommands.HandleException(msg, e)));
            }
        }

        return failures;
    }

    public static void ThrowIfAny(string jobName, List<OmnidotsMonitorFailure> failures)
    {
        if (failures.Count > 0)
        {
            throw new OmnidotsImportException(jobName, failures);
        }
    }
}
