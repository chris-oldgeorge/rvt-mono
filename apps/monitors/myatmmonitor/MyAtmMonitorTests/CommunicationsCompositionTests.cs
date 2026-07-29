using MyAtm.Api;
using Rvt.Monitor.IntegrationTesting;

namespace MyAtmMonitorTests;

// Summary: Asserts this host honors the shared communications-composition contract.
// Major updates:
// - 2026-07-29 TestKit consolidation: delegates to CommunicationsCompositionContract;
//   only the AddMyAtmMonitor registration under test is host-specific.
[TestClass]
public sealed class CommunicationsCompositionTests
{
    [TestMethod]
    public Task AddMyAtmMonitor_MissingProvider_ComposesSendGridSmsAndWorkflows() =>
        CommunicationsCompositionContract.VerifyMissingProviderComposesSendGridSmsAndWorkflowsAsync(
            (services, configuration) => services.AddMyAtmMonitor(configuration));

    [TestMethod]
    public void AddMyAtmMonitor_MicrosoftGraphCaseInsensitive_ComposesMicrosoftGraph() =>
        CommunicationsCompositionContract.VerifyMicrosoftGraphIsSelectedCaseInsensitively(
            (services, configuration) => services.AddMyAtmMonitor(configuration));

    [TestMethod]
    public void AddMyAtmMonitor_InvalidProvider_ThrowsSafeMessageAtCompositionTime() =>
        CommunicationsCompositionContract.VerifyInvalidProviderFailsWithTheSafeMessage(
            (services, configuration) => services.AddMyAtmMonitor(configuration));
}
