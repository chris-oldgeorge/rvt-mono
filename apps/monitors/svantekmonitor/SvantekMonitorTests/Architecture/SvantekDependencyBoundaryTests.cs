using Rvt.Monitor.IntegrationTesting;
using Svantek.Api.Ports;

namespace SvantekMonitorTests.Architecture;

[TestClass]
public sealed class SvantekDependencyBoundaryTests
{
    private const string _monitorDirectory = "apps/monitors/svantekmonitor/SvantekMonitor";

    /// <summary>
    /// <c>SvantekApi</c> is the historical facade and still constructs the
    /// vendor gateway itself; the composition root registers the transport,
    /// database client, and durable alert stack. Both are listed so the guards
    /// pass today while keeping every other file honest.
    /// </summary>
    private static readonly MonitorBoundaryContractDefinition _definition = new()
    {
        MonitorDirectory = _monitorDirectory,
        ApiNamespaceRoot = "Svantek.Api",
        VendorGatewayPort = typeof(ISvantekVendorGateway),
        TransportMarkers =
        [
            "Svantek.Api.Http",
            "SvantekHttpGateway",
            "HttpWebClient",
            "IHttpClient",
            "HttpClient"
        ],
        PersistenceMarkers =
        [
            "SvantekMonitorContext",
            "Npgsql",
            "Microsoft.EntityFrameworkCore"
        ],
        PersistenceAllowlist = [_monitorDirectory + "/api/SvantekMonitorServices.cs"],
        AdapterConstructionMarkers =
        [
            "new SvantekHttpGateway",
            "new HttpWebClient",
            "new DBClient"
        ],
        AdapterConstructionAllowlist =
        [
            _monitorDirectory + "/api/SvantekApi.cs",
            _monitorDirectory + "/api/SvantekMonitorServices.cs"
        ]
        // The M7 baseline is empty: BatteryAlertType lives in Svantek.Model.Dto
        // and JAN1_1970 comes from DateTimeUtil, so no model file may import
        // the api layer.
    };

    [TestMethod]
    public void ApiPartials_DoNotCallConcreteDatabaseClientFieldDirectly()
    {
        string repositoryRoot = RepositoryLayout.Root;
        string[] apiFiles = Directory.GetFiles(
            RepositoryLayout.GetPath(
                "apps",
                "monitors",
                "svantekmonitor",
                "SvantekMonitor",
                "api"),
            "SvantekApi*.cs",
            SearchOption.TopDirectoryOnly);

        List<string> directCalls = [.. apiFiles
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = Path.GetRelativePath(repositoryRoot, path),
                    Line = index + 1,
                    Text = line
                }))
            .Where(row => row.Text.Contains("dbClient.", StringComparison.Ordinal))
            .Select(row => $"{row.Path}:{row.Line}: {row.Text.Trim()}")];

        CollectionAssert.AreEqual(Array.Empty<string>(), directCalls);
    }

    [TestMethod]
    public void UseCases_DependOnTheVendorPortRatherThanTheHttpAdapter() =>
        MonitorDependencyBoundaryContract.VerifyUseCasesDependOnTheVendorPortRatherThanTheTransport(_definition);

    [TestMethod]
    public void ApplicationCodeOutsideTheDataAdapter_DoesNotReferenceEfCoreOrNpgsql() =>
        MonitorDependencyBoundaryContract.VerifyPersistenceDetailStaysInsideTheDataAdapter(_definition);

    [TestMethod]
    public void ProductionCode_DoesNotBlockOnAsynchronousCalls() =>
        MonitorDependencyBoundaryContract.VerifyProductionCodeDoesNotBlockOnAsynchronousCalls(_definition);

    [TestMethod]
    public void EveryVendorPortMethodIsAsynchronousAndCancellable() =>
        MonitorDependencyBoundaryContract.VerifyEveryVendorPortMethodIsAsynchronousAndCancellable(_definition);

    [TestMethod]
    public void ConcreteAdapterConstruction_IsConfinedToTheCompositionAllowlist() =>
        MonitorDependencyBoundaryContract.VerifyConcreteAdapterConstructionIsConfinedToTheCompositionAllowlist(_definition);

    [TestMethod]
    public void ModelLayer_DoesNotImportTheApiLayer() =>
        MonitorDependencyBoundaryContract.VerifyTheModelLayerDoesNotImportTheApiLayer(_definition);
}
