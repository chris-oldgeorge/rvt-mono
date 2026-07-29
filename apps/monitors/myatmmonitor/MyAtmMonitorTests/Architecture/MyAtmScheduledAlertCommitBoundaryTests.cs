using System.Text.RegularExpressions;
using Rvt.Monitor.IntegrationTesting;

namespace MyAtmMonitorTests.Architecture;

[TestClass]
public sealed class MyAtmScheduledAlertCommitBoundaryTests
{
    private static readonly string[] _expected = ["CommitAlertAsync"];

    [TestMethod]
    [DataRow("ProcessDustLevelsHandler.cs", "CreateAggregateCommit,CreateDeletedRuleDeactivationCommit")]
    [DataRow("CheckForOfflineMonitorsHandler.cs", "CreateOfflineCommit,CreateOnlineRecoveryCommit")]
    public void ScheduledAlertHandlers_OnlyUseTheAtomicAlertCommitBoundary(
        string fileName,
        string expectedCommitFactories)
    {
        string source = File.ReadAllText(RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "myatmmonitor",
            "MyAtmMonitor",
            "api",
            "UseCases",
            fileName));

        Assert.Contains("IMyAtmAlertCommitCommands", source);
        CollectionAssert.AreEquivalent(
            _expected,
            InvokedMethods(source, "_alertCommitCommands"));
        CollectionAssert.AreEquivalent(
            expectedCommitFactories.Split(',', StringSplitOptions.RemoveEmptyEntries),
            InvokedMethods(source, "_ruleProcessor"));

        AssertContainsNone(
            source,
            "non-alert command ports",
            [
                "IDBClient",
                "IMyAtmMonitorCommands",
                "IMyAtmMeasurementCommands",
                "IMyAtmAccessoryCommands",
                "IMyAtmDustImportCommands"
            ]);
        AssertContainsNone(
            source,
            "non-alert command methods",
            [
                "WriteMonitorList(",
                "WriteLatestTimestamp(",
                "WriteFleetNr(",
                "SetMonitorOffline(",
                "InsertDustDtos(",
                "InsertAccessoryDto(",
                "InsertAccessoryPageAsync(",
                "CommitDustImportAsync(",
                "HandleException(",
                "WriteNotification(",
                "WriteNotificationAudit(",
                "UpdateAlertRule(",
                "ClearErrorMessages("
            ]);
        AssertContainsNone(source, "direct delivery and legacy rule processing",
        [
            "IMessageService",
            "IMqttClient",
            "IMonitorEventPublisher",
            "IMonitorDeliveryOutbox",
            "MonitorDeliveryDispatcher",
            "ProcessRule(",
            "ProcessRulesV2(",
            "ProcessAlertForContacts(",
            "ClaimNextDueAsync(",
            "CompleteAsync(",
            "RetryAsync(",
            "DeadLetterAsync(",
            "SendMessage",
            "PublishAsync("
        ]);
    }

    [TestMethod]
    public void StoreDustLevelsHandler_CommitsAtomicallyWithoutRequestingDelivery()
    {
        string source = File.ReadAllText(RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "myatmmonitor",
            "MyAtmMonitor",
            "api",
            "UseCases",
            "StoreDustLevelsHandler.cs"));

        Assert.Contains("IMyAtmDustImportCommands", source);
        Assert.IsFalse(source.Contains("MonitorDeliveryDispatcher", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("IMessageService", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("IMqttClient", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("IMonitorEventPublisher", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("WriteNotification", StringComparison.Ordinal));

        Assert.Contains("CommitDustImportAsync", source);
        Assert.IsFalse(source.Contains("DispatchDueAsync", StringComparison.Ordinal));
    }

    // The scheduled handlers reach delivery only through MyAtmRuleProcessor, so the
    // invariant also has to hold there: the processor must stay commit-only and must
    // never regrow a direct, synchronous notification route.
    [TestMethod]
    public void MyAtmRuleProcessor_BuildsCommitsWithoutADirectSynchronousDeliveryRoute()
    {
        string source = File.ReadAllText(RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "myatmmonitor",
            "MyAtmMonitor",
            "api",
            "MyAtmRuleProcessor.cs"));

        Assert.Contains("CreateAggregateCommit", source);
        Assert.Contains("CreateOfflineCommit", source);
        AssertContainsNone(source, "direct delivery and legacy rule processing",
        [
            "IMessageService",
            "IMonitorEventPublisher",
            "IMqttClient",
            "GetAwaiter().GetResult()",
            "SendMessage",
            "Sendmessage",
            "WriteNotification(",
            "WriteNotificationAudit(",
            "UpdateAlertRule(",
            "ProcessRule(",
            "ProcessRulesV2(",
            "ProcessAlertForContacts("
        ]);
    }

    [TestMethod]
    public void MyAtmService_UsesFocusedHandlersInsteadOfCompatibilityFacades()
    {
        string source = File.ReadAllText(RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "myatmmonitor",
            "MyAtmMonitor",
            "api",
            "MyAtmService.cs"));

        AssertContainsNone(source, "compatibility facades", ["MyAtmApi", "IDBClient"]);
        Assert.Contains("StoreMonitorsHandler", source);
        Assert.Contains("CheckForOfflineMonitorsHandler", source);
        Assert.Contains("StoreDustLevelsHandler", source);
        Assert.Contains("ProcessDustLevelsHandler", source);
        Assert.Contains("StoreAccessoryInfoHandler", source);
        Assert.Contains("MonitorDeliveryDispatcher", source);
    }

    // The optional underscore keeps this boundary check independent of which private-field
    // naming convention a handler uses; the repository still carries both.
    private static string[] InvokedMethods(string source, string receiver) =>
        [.. Regex.Matches(source, $@"\b_?{Regex.Escape(receiver)}\.(?<method>[A-Za-z0-9_]+)\s*\(")
            .Select(match => match.Groups["method"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private static void AssertContainsNone(string source, string boundary, IEnumerable<string> forbiddenReferences)
    {
        string[] violations = [.. forbiddenReferences.Where(reference => source.Contains(reference, StringComparison.Ordinal))];
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            violations,
            $"Scheduled alert handlers must not reference {boundary}.");
    }

}
