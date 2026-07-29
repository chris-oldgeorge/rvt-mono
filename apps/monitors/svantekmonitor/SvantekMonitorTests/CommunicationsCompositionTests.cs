using Rvt.Monitor.IntegrationTesting;
using Svantek.Api;

namespace SvantekMonitorTests;

// Summary: Asserts this host honors the shared communications-composition contract.
// Major updates:
// - 2026-07-29 TestKit consolidation: delegates to CommunicationsCompositionContract;
//   only the AddSvantekMonitor registration under test is host-specific.
[TestClass]
public sealed class CommunicationsCompositionTests
{
    [TestMethod]
    public Task AddSvantekMonitor_MissingProvider_ComposesSendGridSmsAndWorkflows() =>
        CommunicationsCompositionContract.VerifyMissingProviderComposesSendGridSmsAndWorkflowsAsync(
            (services, configuration) => services.AddSvantekMonitor(configuration));

    [TestMethod]
    public void AddSvantekMonitor_MicrosoftGraphCaseInsensitive_ComposesMicrosoftGraph() =>
        CommunicationsCompositionContract.VerifyMicrosoftGraphIsSelectedCaseInsensitively(
            (services, configuration) => services.AddSvantekMonitor(configuration));

    [TestMethod]
    public void AddSvantekMonitor_InvalidProvider_ThrowsSafeMessageAtCompositionTime() =>
        CommunicationsCompositionContract.VerifyInvalidProviderFailsWithTheSafeMessage(
            (services, configuration) => services.AddSvantekMonitor(configuration));
}
