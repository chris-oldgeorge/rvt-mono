using System.Reflection;
using AirQ.Api.Ports;
using Rvt.Monitor.IntegrationTesting;

namespace AirQMonitorTests.Architecture;

/// <summary>
/// AirQ was the only monitor without architecture guards, so the boundaries the
/// July port extraction established were held by convention alone. These tests
/// pin them: the use cases talk to <see cref="IAirQVendorGateway"/> rather than
/// the vendor transport, persistence detail stays inside <c>api/db</c>, and the
/// import chain stays asynchronous end to end.
/// </summary>
[TestClass]
public sealed class AirQDependencyBoundaryTests
{
    /// <summary>
    /// <c>AirQApi</c> is the historical facade and still constructs the adapter
    /// itself. It is listed here so the guard passes today while keeping every
    /// other file honest; the correct end state is composition-root only.
    /// </summary>
    private static readonly string[] _adapterConstructionAllowlist =
    [
        "apps/monitors/airqmonitor/AirQMonitor/api/AirQApi.cs",
        "apps/monitors/airqmonitor/AirQMonitor/api/AirQMonitorServices.cs"
    ];

    [TestMethod]
    public void UseCases_DependOnTheVendorPortRatherThanTheHttpAdapter()
    {
        string[] transportMarkers =
        [
            "AirQ.Api.Http",
            "AirQHttpGateway",
            "HttpWebClient",
            "IHttpClient",
            "HttpClient"
        ];

        string[] offenders = [.. ProductionFiles("api", "UseCases")
            .Where(file => transportMarkers.Any(marker =>
                file.Text.Contains(marker, StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            offenders,
            "Use cases must depend on IAirQVendorGateway, not on the vendor transport: "
                + string.Join(", ", offenders));
    }

    [TestMethod]
    public void ApplicationCodeOutsideTheDataAdapter_DoesNotReferenceEfCoreOrNpgsql()
    {
        string[] persistenceMarkers =
        [
            "AirQMonitorContext",
            "Npgsql",
            "Microsoft.EntityFrameworkCore"
        ];

        string[] offenders = [.. ProductionFiles()
            .Where(file => !file.RelativePath.Contains("/api/db/", StringComparison.Ordinal))
            .Where(file => persistenceMarkers.Any(marker =>
                file.Text.Contains(marker, StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            offenders,
            "Entity Framework and Npgsql detail must stay inside api/db: "
                + string.Join(", ", offenders));
    }

    [TestMethod]
    public void ProductionCode_DoesNotBlockOnAsynchronousCalls()
    {
        string[] blockingMarkers =
        [
            ".GetAwaiter().GetResult()",
            ".Wait()",
            ".Result"
        ];

        string[] offenders = [.. ProductionFiles()
            .Where(file => blockingMarkers.Any(marker =>
                file.Text.Contains(marker, StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            offenders,
            "The AirQ import chain is asynchronous end to end; blocking on a task "
                + "reintroduces the deadlock and swallows cancellation: "
                + string.Join(", ", offenders));
    }

    [TestMethod]
    public void EveryVendorPortMethodIsAsynchronousAndCancellable()
    {
        MethodInfo[] methods = typeof(IAirQVendorGateway).GetMethods();

        Assert.IsNotEmpty(methods);
        foreach (MethodInfo method in methods)
        {
            Assert.IsTrue(
                typeof(Task).IsAssignableFrom(method.ReturnType),
                $"{method.Name} must return a Task so the vendor call can be awaited.");
            Assert.IsTrue(
                method.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(CancellationToken)),
                $"{method.Name} must accept a CancellationToken so shutdown reaches the vendor request.");
        }
    }

    [TestMethod]
    public void ConcreteAdapterConstruction_IsConfinedToTheCompositionAllowlist()
    {
        string[] adapterConstructors =
        [
            "new AirQHttpGateway",
            "new HttpWebClient",
            "new DBClient"
        ];

        string[] offenders = [.. ProductionFiles()
            .Where(file => !_adapterConstructionAllowlist.Contains(file.RelativePath, StringComparer.Ordinal))
            .Where(file => adapterConstructors.Any(marker =>
                file.Text.Contains(marker, StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            offenders,
            "Only the composition root may name concrete adapters: "
                + string.Join(", ", offenders));
    }

    private static IReadOnlyList<(string RelativePath, string Text)> ProductionFiles(
        params string[] relativeSegments)
    {
        string[] segments = ["apps", "monitors", "airqmonitor", "AirQMonitor", .. relativeSegments];
        return [.. Directory
            .EnumerateFiles(RepositoryLayout.GetPath(segments), "*.cs", SearchOption.AllDirectories)
            .Select(path => Normalize(Path.GetRelativePath(RepositoryLayout.Root, path)))
            .Where(relativePath => !relativePath.Contains("/bin/", StringComparison.Ordinal))
            .Where(relativePath => !relativePath.Contains("/obj/", StringComparison.Ordinal))
            .Select(relativePath => (
                relativePath,
                File.ReadAllText(Path.Combine(RepositoryLayout.Root, relativePath))))];
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
