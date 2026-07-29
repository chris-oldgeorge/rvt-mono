using ReportingMonitor.Api;
using Rvt.Monitor.IntegrationTesting;

namespace ReportingMonitorTests;

// Summary: Asserts this host honors the shared communications-composition contract.
// Major updates:
// - 2026-07-29 TestKit consolidation: delegates to CommunicationsCompositionContract;
//   only the AddReportingMonitor registration under test is host-specific.
public sealed class CommunicationsCompositionTests
{
    [Fact]
    public Task AddReportingMonitor_MissingProvider_ComposesSendGridSmsAndWorkflows() =>
        CommunicationsCompositionContract.VerifyMissingProviderComposesSendGridSmsAndWorkflowsAsync(
            (services, configuration) => services.AddReportingMonitor(configuration));

    [Fact]
    public void AddReportingMonitor_MicrosoftGraphCaseInsensitive_ComposesMicrosoftGraph() =>
        CommunicationsCompositionContract.VerifyMicrosoftGraphIsSelectedCaseInsensitively(
            (services, configuration) => services.AddReportingMonitor(configuration));

    [Fact]
    public void AddReportingMonitor_InvalidProvider_ThrowsSafeMessageAtCompositionTime() =>
        CommunicationsCompositionContract.VerifyInvalidProviderFailsWithTheSafeMessage(
            (services, configuration) => services.AddReportingMonitor(configuration));
}
