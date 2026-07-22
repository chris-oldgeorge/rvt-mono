using System.Reflection;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.UseCases;
using MyAtm.Model.Config;
using Rvt.Monitor.Common.Delivery;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmServiceCompositionTests
{
    [TestMethod]
    public void Constructor_DependsOnFocusedHandlersInsteadOfCompatibilityFacades()
    {
        var parameterTypes = typeof(MyAtmService)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                typeof(StoreMonitorsHandler),
                typeof(CheckForOfflineMonitorsHandler),
                typeof(StoreDustLevelsHandler),
                typeof(ProcessDustLevelsHandler),
                typeof(ClearOlderErrorMessagesHandler),
                typeof(StoreAccessoryInfoHandler),
                typeof(MonitorDeliveryDispatcher),
                typeof(MyAtmMonitorOptions)
            },
            parameterTypes);
        CollectionAssert.DoesNotContain(parameterTypes, typeof(MyAtmApi));
        CollectionAssert.DoesNotContain(parameterTypes, typeof(IDBClient));
    }

    [TestMethod]
    public void ScheduledMethods_DelegateToTheExpectedFocusedHandlerWithPeriodAndCancellation()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "myatmmonitor",
            "MyAtmMonitor",
            "api",
            "MyAtmService.cs"));

        StringAssert.Contains(source, "storeMonitors.RunAsync(customerId, cancellationToken)");
        StringAssert.Contains(source, "checkForOfflineMonitors.RunAsync(customerId, cancellationToken)");
        StringAssert.Contains(source, "storeDustLevels.RunAsync<DeviceMeasurement>(customerId, Period.Minutes1, cancellationToken)");
        StringAssert.Contains(source, "storeDustLevels.RunAsync<AvgDeviceMeasurement>(customerId, Period.Minutes15, cancellationToken)");
        StringAssert.Contains(source, "storeDustLevels.RunAsync<AvgDeviceMeasurement>(customerId, Period.Hours1, cancellationToken)");
        StringAssert.Contains(source, "storeDustLevels.RunAsync<AvgDeviceMeasurement>(customerId, Period.Hours24, cancellationToken)");
        StringAssert.Contains(source, "processDustLevels.RunAsync<AvgDeviceMeasurement>(customerId, Period.Hours8, cancellationToken)");
        StringAssert.Contains(source, "clearOlderErrorMessages.Run()");
        StringAssert.Contains(source, "storeAccessoryInfo.RunAsync(customerId, cancellationToken)");
        StringAssert.Contains(source, "outboxDispatcher.DispatchDueAsync(cancellationToken)");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not find repository root from test output directory.");
        return string.Empty;
    }
}
