using Omnidots.Api.Ports;
using Rvt.Monitor.IntegrationTesting;

namespace OmnidotsAdapterTests.Architecture;

// Summary: Asserts this host honors the shared monitor dependency-boundary contract.
// Major updates:
// - 2026-07-30 guard pack (G3/G4): first Omnidots dependency guards, instantiated
//   from MonitorDependencyBoundaryContract; only the definition is host-specific.
[TestClass]
public sealed class OmnidotsDependencyBoundaryTests
{
    private const string _monitorDirectory = "apps/monitors/omnidotsmonitor/OmnidotsMonitor";

    /// <summary>
    /// <c>OmnidotsApi</c> is the historical facade and still constructs the
    /// vendor gateway itself; the composition root registers the transport,
    /// database client, and durable alert stack. Both are listed so the guards
    /// pass today while keeping every other file honest.
    /// </summary>
    private static readonly MonitorBoundaryContractDefinition _definition = new()
    {
        MonitorDirectory = _monitorDirectory,
        ApiNamespaceRoot = "Omnidots.Api",
        VendorGatewayPort = typeof(IOmnidotsVendorGateway),
        TransportMarkers =
        [
            "Omnidots.Api.Http",
            "OmnidotsHttpGateway",
            "HttpWebClient",
            "IHttpClient",
            "HttpClient"
        ],
        PersistenceMarkers =
        [
            "OmnidotsMonitorContext",
            "Npgsql",
            "Microsoft.EntityFrameworkCore"
        ],
        PersistenceAllowlist = [_monitorDirectory + "/api/OmnidotsMonitorServices.cs"],
        AdapterConstructionMarkers =
        [
            "new OmnidotsHttpGateway",
            "new HttpWebClient",
            "new DBClient"
        ],
        AdapterConstructionAllowlist =
        [
            _monitorDirectory + "/api/OmnidotsApi.cs",
            _monitorDirectory + "/api/OmnidotsMonitorServices.cs"
        ]
        // The M7 baseline is empty: BatteryAlertType lives in
        // Omnidots.Model.Dto and JAN1_1970 comes from DateTimeUtil, so no
        // model file may import the api layer.
    };

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
