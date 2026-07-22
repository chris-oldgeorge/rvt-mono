using MyAtm.Api.Db;
using Rvt.Monitor.Common.Delivery;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmOutboxContractTests
{
    [TestMethod]
    public void StoreDustLevelsHandler_DoesNotDependOnOrInvokeDeliveryDispatcher()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
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
        var claim = FindMethod<IMonitorDeliveryOutboxQueries>(nameof(IMonitorDeliveryOutboxQueries.ClaimNextDueAsync));
        var complete = FindMethod<IMonitorDeliveryOutboxCommands>(nameof(IMonitorDeliveryOutboxCommands.CompleteAsync));
        var retry = FindMethod<IMonitorDeliveryOutboxCommands>(nameof(IMonitorDeliveryOutboxCommands.RetryAsync));
        var deadLetter = FindMethod<IMonitorDeliveryOutboxCommands>(nameof(IMonitorDeliveryOutboxCommands.DeadLetterAsync));

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
        var queries = (IMonitorDeliveryOutboxQueries)new DBClient(string.Empty);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => queries.ClaimNextDueAsync(
            "myatm",
            DateTime.UtcNow,
            TimeSpan.FromMinutes(2)));
    }

    private static System.Reflection.MethodInfo FindMethod<T>(string name, int? parameterCount = null) =>
        typeof(T).GetMethods()
            .Single(method => method.Name == name && (parameterCount == null || method.GetParameters().Length == parameterCount));

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

        throw new InvalidOperationException("Repository root was not found.");
    }
}
