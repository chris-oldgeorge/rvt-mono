using System.Reflection;

namespace Rvt.Monitor.IntegrationTesting;

// Summary: The one parameterized dependency-boundary contract every monitor host test instantiates.
// Major updates:
// - 2026-07-30 guard pack (G3/G4): generalized AirQ's five architecture guards into
//   this contract and added the model-must-not-import-api scan, so all four vendor
//   monitors enforce the July port extraction instead of holding it by convention.

/// <summary>
/// Describes one monitor application to
/// <see cref="MonitorDependencyBoundaryContract"/>. All paths are
/// repository-relative and use forward slashes.
/// </summary>
public sealed record MonitorBoundaryContractDefinition
{
    /// <summary>The monitor application directory, e.g. <c>apps/monitors/airqmonitor/AirQMonitor</c>.</summary>
    public required string MonitorDirectory { get; init; }

    /// <summary>The monitor's api-layer namespace root, e.g. <c>AirQ.Api</c>.</summary>
    public required string ApiNamespaceRoot { get; init; }

    /// <summary>The driven vendor port the use cases must depend on.</summary>
    public required Type VendorGatewayPort { get; init; }

    /// <summary>Markers that betray a vendor-transport dependency inside <c>api/UseCases</c>.</summary>
    public required IReadOnlyList<string> TransportMarkers { get; init; }

    /// <summary>Markers that betray EF Core or Npgsql detail outside <c>api/db</c>.</summary>
    public required IReadOnlyList<string> PersistenceMarkers { get; init; }

    /// <summary>
    /// Files outside <c>api/db</c> allowed to name persistence detail: the
    /// composition roots that register the durable alert stack.
    /// </summary>
    public IReadOnlyList<string> PersistenceAllowlist { get; init; } = [];

    /// <summary>Concrete adapter constructor markers, e.g. <c>new AirQHttpGateway</c>.</summary>
    public required IReadOnlyList<string> AdapterConstructionMarkers { get; init; }

    /// <summary>Files allowed to construct concrete adapters: the composition allowlist.</summary>
    public required IReadOnlyList<string> AdapterConstructionAllowlist { get; init; }

    /// <summary>
    /// The frozen baseline of <c>model/</c> files still importing the api layer
    /// (the M7 <c>JAN1_1970</c>/enum inversion). Fixing an entry should shrink
    /// this list; no file may join it.
    /// </summary>
    public IReadOnlyList<string> ModelApiImportAllowlist { get; init; } = [];

    /// <summary>
    /// Exact lines the blocking-call scan tolerates: members that merely share a
    /// name with the <see cref="Task"/> APIs (for example a DTO property named
    /// <c>Result</c>).
    /// </summary>
    public IReadOnlyList<BlockingCallAllowance> BlockingCallAllowlist { get; init; } = [];
}

/// <summary>One tolerated blocking-scan line: the file and its trimmed text.</summary>
public sealed record BlockingCallAllowance(string RelativePath, string TrimmedLine);

/// <summary>
/// Verifies the dependency boundaries the July port extraction established in
/// every monitor host: use cases talk to the vendor port rather than the
/// transport, persistence detail stays inside <c>api/db</c>, nothing blocks on
/// asynchronous calls, the vendor port is asynchronous and cancellable,
/// concrete adapters are constructed only by the composition roots, and the
/// model layer never imports the api layer. Framework-neutral: failures throw
/// <see cref="InvalidOperationException"/> so MSTest and xUnit callers surface
/// them identically.
/// </summary>
public static class MonitorDependencyBoundaryContract
{
    private static readonly string[] _blockingMarkers =
    [
        ".GetAwaiter().GetResult()",
        ".Wait()",
        ".Result"
    ];

    public static void VerifyUseCasesDependOnTheVendorPortRatherThanTheTransport(
        MonitorBoundaryContractDefinition definition)
    {
        string[] offenders = [.. ProductionFiles(definition, "api/UseCases")
            .Where(file => definition.TransportMarkers.Any(marker =>
                file.Text.Contains(marker, StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        RequireEmpty(
            offenders,
            $"Use cases must depend on {definition.VendorGatewayPort.Name}, not on the vendor transport");
    }

    public static void VerifyPersistenceDetailStaysInsideTheDataAdapter(
        MonitorBoundaryContractDefinition definition)
    {
        string dataAdapterPrefix = definition.MonitorDirectory + "/api/db/";
        string[] offenders = [.. ProductionFiles(definition)
            .Where(file => !file.RelativePath.StartsWith(dataAdapterPrefix, StringComparison.Ordinal))
            .Where(file => !definition.PersistenceAllowlist.Contains(file.RelativePath, StringComparer.Ordinal))
            .Where(file => definition.PersistenceMarkers.Any(marker =>
                file.Text.Contains(marker, StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        RequireEmpty(offenders, "Entity Framework and Npgsql detail must stay inside api/db");
    }

    public static void VerifyProductionCodeDoesNotBlockOnAsynchronousCalls(
        MonitorBoundaryContractDefinition definition)
    {
        string[] offenders = [.. ProductionFiles(definition)
            .SelectMany(file => file.Text
                .Split('\n')
                .Select((line, index) => (Line: line.TrimEnd('\r'), Number: index + 1))
                .Where(row => _blockingMarkers.Any(marker =>
                    row.Line.Contains(marker, StringComparison.Ordinal)))
                .Where(row => !definition.BlockingCallAllowlist.Any(allowance =>
                    allowance.RelativePath == file.RelativePath &&
                    allowance.TrimmedLine == row.Line.Trim()))
                .Select(row => $"{file.RelativePath}:{row.Number}"))
            .Order(StringComparer.Ordinal)];

        RequireEmpty(
            offenders,
            "The monitor import chain is asynchronous end to end; blocking on a task "
                + "reintroduces the deadlock and swallows cancellation");
    }

    public static void VerifyEveryVendorPortMethodIsAsynchronousAndCancellable(
        MonitorBoundaryContractDefinition definition)
    {
        MethodInfo[] methods = definition.VendorGatewayPort.GetMethods();
        if (methods.Length == 0)
        {
            throw new InvalidOperationException(
                $"{definition.VendorGatewayPort.Name} declares no methods; the vendor port contract is empty.");
        }

        foreach (MethodInfo method in methods)
        {
            if (!typeof(Task).IsAssignableFrom(method.ReturnType))
            {
                throw new InvalidOperationException(
                    $"{definition.VendorGatewayPort.Name}.{method.Name} must return a Task "
                        + "so the vendor call can be awaited.");
            }

            if (!method.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(CancellationToken)))
            {
                throw new InvalidOperationException(
                    $"{definition.VendorGatewayPort.Name}.{method.Name} must accept a CancellationToken "
                        + "so shutdown reaches the vendor request.");
            }
        }
    }

    public static void VerifyConcreteAdapterConstructionIsConfinedToTheCompositionAllowlist(
        MonitorBoundaryContractDefinition definition)
    {
        string[] offenders = [.. ProductionFiles(definition)
            .Where(file => !definition.AdapterConstructionAllowlist.Contains(file.RelativePath, StringComparer.Ordinal))
            .Where(file => definition.AdapterConstructionMarkers.Any(marker =>
                file.Text.Contains(marker, StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        RequireEmpty(offenders, "Only the composition root may name concrete adapters");
    }

    // Scans model/ for the monitor's own api namespace root. Api-layer files
    // that deliberately keep a shared-kernel namespace (MyAtm's api/Delivery,
    // PR #51) are outside model/ and therefore outside this scan; model files
    // must reach them only through genuinely shared abstractions, which is why
    // the scan keys on the monitor's api namespace rather than folder-declared
    // type locations.
    public static void VerifyTheModelLayerDoesNotImportTheApiLayer(
        MonitorBoundaryContractDefinition definition)
    {
        string[] offenders = [.. ProductionFiles(definition, "model")
            .Where(file => !definition.ModelApiImportAllowlist.Contains(file.RelativePath, StringComparer.Ordinal))
            .Where(file => file.Text.Contains(definition.ApiNamespaceRoot, StringComparison.Ordinal))
            .Select(file => file.RelativePath)
            .Order(StringComparer.Ordinal)];

        RequireEmpty(
            offenders,
            $"The model layer must not import {definition.ApiNamespaceRoot} "
                + "(dependencies point api -> model, never back)");
    }

    private static void RequireEmpty(string[] offenders, string requirement)
    {
        if (offenders.Length > 0)
        {
            throw new InvalidOperationException(
                $"{requirement}: {string.Join(", ", offenders)}.");
        }
    }

    private static IReadOnlyList<(string RelativePath, string Text)> ProductionFiles(
        MonitorBoundaryContractDefinition definition,
        params string[] relativeDirectories)
    {
        string[] segments = [
            .. definition.MonitorDirectory.Split('/'),
            .. relativeDirectories.SelectMany(directory => directory.Split('/'))];
        return [.. Directory
            .EnumerateFiles(RepositoryLayout.GetPath(segments), "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/'))
            .Where(relativePath => !relativePath.Contains("/bin/", StringComparison.Ordinal))
            .Where(relativePath => !relativePath.Contains("/obj/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(relativePath => (
                relativePath,
                File.ReadAllText(Path.Combine(RepositoryLayout.Root, relativePath))))];
    }
}
