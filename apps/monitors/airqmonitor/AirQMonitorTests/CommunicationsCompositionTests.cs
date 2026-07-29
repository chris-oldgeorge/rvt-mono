using AirQ.Api;
using Rvt.Monitor.IntegrationTesting;

namespace AirQMonitorTests;

// Summary: Asserts this host honors the shared communications-composition contract.
// Major updates:
// - 2026-07-29 TestKit consolidation: delegates to CommunicationsCompositionContract;
//   only the AddAirQMonitor registration under test is host-specific.
[TestClass]
public sealed class CommunicationsCompositionTests
{
    [TestMethod]
    public Task AddAirQMonitor_MissingProvider_ComposesSendGridSmsAndWorkflows() =>
        CommunicationsCompositionContract.VerifyMissingProviderComposesSendGridSmsAndWorkflowsAsync(
            (services, configuration) => services.AddAirQMonitor(configuration));

    [TestMethod]
    public void AddAirQMonitor_MicrosoftGraphCaseInsensitive_ComposesMicrosoftGraph() =>
        CommunicationsCompositionContract.VerifyMicrosoftGraphIsSelectedCaseInsensitively(
            (services, configuration) => services.AddAirQMonitor(configuration));

    [TestMethod]
    public void AddAirQMonitor_InvalidProvider_ThrowsSafeMessageAtCompositionTime() =>
        CommunicationsCompositionContract.VerifyInvalidProviderFailsWithTheSafeMessage(
            (services, configuration) => services.AddAirQMonitor(configuration));
}
