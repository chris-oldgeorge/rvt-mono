using Omnidots.Api;
using Rvt.Monitor.IntegrationTesting;

namespace OmnidotsMonitorTests.Architecture;

// Summary: Asserts this host honors the shared communications-composition contract.
// Major updates:
// - 2026-07-29 TestKit consolidation: delegates to CommunicationsCompositionContract;
//   only the AddOmnidotsMonitor registration under test is host-specific.
[TestClass]
public sealed class CommunicationsCompositionTests
{
    [TestMethod]
    public Task AddOmnidotsMonitor_MissingProvider_ComposesSendGridSmsAndWorkflows() =>
        CommunicationsCompositionContract.VerifyMissingProviderComposesSendGridSmsAndWorkflowsAsync(
            (services, configuration) => services.AddOmnidotsMonitor(configuration));

    [TestMethod]
    public void AddOmnidotsMonitor_MicrosoftGraphCaseInsensitive_ComposesMicrosoftGraph() =>
        CommunicationsCompositionContract.VerifyMicrosoftGraphIsSelectedCaseInsensitively(
            (services, configuration) => services.AddOmnidotsMonitor(configuration));

    [TestMethod]
    public void AddOmnidotsMonitor_InvalidProvider_ThrowsSafeMessageAtCompositionTime() =>
        CommunicationsCompositionContract.VerifyInvalidProviderFailsWithTheSafeMessage(
            (services, configuration) => services.AddOmnidotsMonitor(configuration));
}
