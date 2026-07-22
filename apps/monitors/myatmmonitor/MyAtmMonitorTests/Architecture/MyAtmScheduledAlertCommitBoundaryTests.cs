using System.Text.RegularExpressions;

namespace MyAtmMonitorTests.Architecture;

[TestClass]
public sealed class MyAtmScheduledAlertCommitBoundaryTests
{
    [DataTestMethod]
    [DataRow("ProcessDustLevelsHandler.cs", "CreateAggregateCommit,CreateDeletedRuleDeactivationCommit")]
    [DataRow("CheckForOfflineMonitorsHandler.cs", "CreateOfflineCommit,CreateOnlineRecoveryCommit")]
    public void ScheduledAlertHandlers_OnlyUseTheAtomicAlertCommitBoundary(
        string fileName,
        string expectedCommitFactories)
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "myatmmonitor", "MyAtmMonitor", "api", "UseCases", fileName));

        StringAssert.Contains(source, "IMyAtmAlertCommitCommands");
        CollectionAssert.AreEquivalent(
            new[] { "CommitAlertAsync" },
            InvokedMethods(source, "alertCommitCommands"));
        CollectionAssert.AreEquivalent(
            expectedCommitFactories.Split(',', StringSplitOptions.RemoveEmptyEntries),
            InvokedMethods(source, "ruleProcessor"));

        AssertContainsNone(
            source,
            "non-alert command ports",
            new[]
            {
                "IDBClient",
                "IMyAtmMonitorCommands",
                "IMyAtmMeasurementCommands",
                "IMyAtmAccessoryCommands",
                "IMyAtmDustImportCommands"
            });
        AssertContainsNone(
            source,
            "non-alert command methods",
            new[]
            {
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
            });
        AssertContainsNone(source, "direct delivery and legacy rule processing", new[]
        {
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
        });
    }

    [TestMethod]
    public void StoreDustLevelsHandler_CommitsAtomicallyWithoutRequestingDelivery()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "myatmmonitor",
            "MyAtmMonitor",
            "api",
            "UseCases",
            "StoreDustLevelsHandler.cs"));

        StringAssert.Contains(source, "IMyAtmDustImportCommands");
        Assert.IsFalse(source.Contains("MonitorDeliveryDispatcher", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("IMessageService", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("IMqttClient", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("IMonitorEventPublisher", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("WriteNotification", StringComparison.Ordinal));

        StringAssert.Contains(source, "CommitDustImportAsync");
        Assert.IsFalse(source.Contains("DispatchDueAsync", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MyAtmService_UsesFocusedHandlersInsteadOfCompatibilityFacades()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "myatmmonitor",
            "MyAtmMonitor",
            "api",
            "MyAtmService.cs"));

        AssertContainsNone(source, "compatibility facades", new[] { "MyAtmApi", "IDBClient" });
        StringAssert.Contains(source, "StoreMonitorsHandler");
        StringAssert.Contains(source, "CheckForOfflineMonitorsHandler");
        StringAssert.Contains(source, "StoreDustLevelsHandler");
        StringAssert.Contains(source, "ProcessDustLevelsHandler");
        StringAssert.Contains(source, "StoreAccessoryInfoHandler");
        StringAssert.Contains(source, "MonitorDeliveryDispatcher");
    }

    private static string[] InvokedMethods(string source, string receiver) =>
        Regex.Matches(source, $@"\b{Regex.Escape(receiver)}\.(?<method>[A-Za-z0-9_]+)\s*\(")
            .Select(match => match.Groups["method"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static void AssertContainsNone(string source, string boundary, IEnumerable<string> forbiddenReferences)
    {
        var violations = forbiddenReferences
            .Where(reference => source.Contains(reference, StringComparison.Ordinal))
            .ToArray();
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            violations,
            $"Scheduled alert handlers must not reference {boundary}.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not find repository root from test output directory.");
        return string.Empty;
    }
}
