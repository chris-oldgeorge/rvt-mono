using System.Reflection;
using MyAtm.Api.Db;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.IntegrationTesting;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmOutboxContractTests
{
    [TestMethod]
    public void StoreDustLevelsHandler_DoesNotDependOnOrInvokeDeliveryDispatcher()
    {
        string repositoryRoot = RepositoryLayout.Root;
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "apps",
            "monitors",
            "myatmmonitor",
            "MyAtmMonitor",
            "api",
            "UseCases",
            "StoreDustLevelsHandler.cs"));

        Assert.DoesNotContain("MonitorDeliveryDispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchDueAsync", source, StringComparison.Ordinal);
    }

    [TestMethod]
    public void OutboxPorts_UseCommonOneAtATimeClaimsAndFencedOutcomes()
    {
        MethodInfo claim = FindMethod<IMonitorDeliveryOutboxQueries>(nameof(IMonitorDeliveryOutboxQueries.ClaimNextDueAsync));
        MethodInfo complete = FindMethod<IMonitorDeliveryOutboxCommands>(nameof(IMonitorDeliveryOutboxCommands.CompleteAsync));
        MethodInfo retry = FindMethod<IMonitorDeliveryOutboxCommands>(nameof(IMonitorDeliveryOutboxCommands.RetryAsync));
        MethodInfo deadLetter = FindMethod<IMonitorDeliveryOutboxCommands>(nameof(IMonitorDeliveryOutboxCommands.DeadLetterAsync));

        Assert.IsNotNull(claim);
        Assert.IsNotNull(complete);
        Assert.IsNotNull(retry);
        Assert.IsNotNull(deadLetter);
        Assert.IsTrue(typeof(IMonitorDeliveryOutboxQueries).IsAssignableFrom(typeof(IDBClient)));
        Assert.IsTrue(typeof(IMonitorDeliveryOutboxCommands).IsAssignableFrom(typeof(IDBClient)));
        Assert.AreEqual(typeof(Task<MonitorDeliveryMessage?>), claim.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(string), typeof(DateTime), typeof(TimeSpan), typeof(CancellationToken) },
            claim.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.AreEqual(typeof(Task<bool>), complete.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(Guid), typeof(Guid), typeof(DateTime), typeof(MonitorDeliveryAudit), typeof(CancellationToken) },
            complete.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.AreEqual(typeof(Task<bool>), retry.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(Guid), typeof(Guid), typeof(DateTime), typeof(string), typeof(CancellationToken) },
            retry.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.AreEqual(typeof(Task<bool>), deadLetter.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(Guid), typeof(Guid), typeof(DateTime), typeof(string), typeof(MonitorDeliveryAudit), typeof(CancellationToken) },
            deadLetter.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    [TestMethod]
    public async Task ClaimNextDueAsync_RejectsCaseVariantUnknownProducerBeforeDatabaseAccess()
    {
        IMonitorDeliveryOutboxQueries queries = (IMonitorDeliveryOutboxQueries)new DBClient(string.Empty);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => queries.ClaimNextDueAsync(
            "myatm",
            DateTime.UtcNow,
            TimeSpan.FromMinutes(2)));
    }

    private static System.Reflection.MethodInfo FindMethod<T>(string name, int? parameterCount = null) =>
        typeof(T).GetMethods()
            .Single(method => method.Name == name && (parameterCount == null || method.GetParameters().Length == parameterCount));

}
