using System.Reflection;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.UseCases;
using MyAtm.Model.Config;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.IntegrationTesting;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmServiceCompositionTests
{
    [TestMethod]
    public void Constructor_DependsOnFocusedHandlersInsteadOfCompatibilityFacades()
    {
        Type[] parameterTypes = [.. typeof(MyAtmService)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)];

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
        string source = File.ReadAllText(RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "myatmmonitor",
            "MyAtmMonitor",
            "Api",
            "MyAtmService.cs"));

        Assert.Contains("_storeMonitors.RunAsync(_customerId, cancellationToken)", source);
        Assert.Contains("_checkForOfflineMonitors.RunAsync(_customerId, cancellationToken)", source);
        Assert.Contains("_storeDustLevels.RunAsync<DeviceMeasurement>(_customerId, Period.Minutes1, cancellationToken)", source);
        Assert.Contains("_storeDustLevels.RunAsync<AvgDeviceMeasurement>(_customerId, Period.Minutes15, cancellationToken)", source);
        Assert.Contains("_storeDustLevels.RunAsync<AvgDeviceMeasurement>(_customerId, Period.Hours1, cancellationToken)", source);
        Assert.Contains("_storeDustLevels.RunAsync<AvgDeviceMeasurement>(_customerId, Period.Hours24, cancellationToken)", source);
        Assert.Contains("_processDustLevels.RunAsync<AvgDeviceMeasurement>(_customerId, Period.Hours8, cancellationToken)", source);
        Assert.Contains("_clearOlderErrorMessages.Run()", source);
        Assert.Contains("_storeAccessoryInfo.RunAsync(_customerId, cancellationToken)", source);
        Assert.Contains("_outboxDispatcher.DispatchDueAsync(cancellationToken)", source);
    }

}
