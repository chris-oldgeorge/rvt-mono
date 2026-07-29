// File summary: Ratchets the host application layer's dependency on the web API layer down to zero.

using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// The in-host application layer under <c>RvtPortal.Spa/Application</c> still
/// imports <c>RvtPortal.Spa.Api</c>, which inverts the intended dependency: the
/// API layer is a driving adapter and must depend on the application layer, not
/// the other way round. The extracted <c>RvtPortal.Application</c> project is
/// already clean and is guarded by
/// <see cref="ApplicationBoundaryArchitectureTests"/>.
/// </summary>
/// <remarks>
/// <see cref="_apiDependentFiles"/> is a shrinking baseline, not an approved
/// list. Adding an entry is a regression; the test fails on any file that is
/// not listed. Removing an entry after moving its API contract into the
/// application layer is the intended direction, and the test also fails if a
/// listed file no longer imports the API layer, so the baseline cannot go stale.
/// </remarks>
public sealed class HostApplicationLayerBoundaryTests
{
    private const string ApiLayerNamespace = "RvtPortal.Spa.Api";

    private static readonly string[] _apiDependentFiles =
    [
        "AlertLevels/AlertLevelApplicationService.cs",
        "AlertLevels/AlertLevelCommands.cs",
        "AlertLevels/AlertLevelWorkflow.cs",
        "Auth/AuthApplicationService.cs",
        "Companies/CompanyApplicationService.cs",
        "Companies/CompanyCommands.cs",
        "Contracts/ContractApplicationService.cs",
        "Contracts/ContractCommands.cs",
        "Data/DataApplicationService.cs",
        "Installers/InstallerApplicationService.cs",
        "Installers/InstallerDeploymentCommands.cs",
        "Monitors/MonitorAdministrationReadService.cs",
        "Monitors/MonitorAdministrationWorkflowService.cs",
        "Monitors/MonitorContractAssignmentCommands.cs",
        "Monitors/MonitorDetailReader.cs",
        "Monitors/MonitorListReader.cs",
        "Monitors/MonitorMutationCommands.cs",
        "Monitors/MonitorReadAuthorizationService.cs",
        "Monitors/MonitorRemovalImpactReader.cs",
        "Monitors/RemoveUnattachedMonitorCommand.cs",
        "Monitors/UploadMonitorPictureCommand.cs",
        "Notifications/NotificationApplicationService.cs",
        "Notifications/NotificationCloseCommands.cs",
        "ReportRules/ReportRuleRecipientReader.cs",
        "Reports/ReportApplicationService.cs",
        "Users/UserAccountCommands.cs",
        "Users/UserAccountWorkflowService.cs"
    ];

    private static string HostApplicationRoot =>
        Path.Combine(RepositoryLayout.Root, "RvtPortal.Spa", "Application");

    [Fact]
    public void HostApplicationLayer_ImportsTheApiLayerOnlyWhereTheBaselineRecordsIt()
    {
        Assert.True(
            Directory.Exists(HostApplicationRoot),
            $"{HostApplicationRoot} must exist.");

        string[] observed = [.. Directory
            .EnumerateFiles(HostApplicationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File
                .ReadAllText(path)
                .Contains(ApiLayerNamespace, StringComparison.Ordinal))
            .Select(path => Path
                .GetRelativePath(HostApplicationRoot, path)
                .Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

        string[] added = [.. observed.Except(_apiDependentFiles, StringComparer.Ordinal)];
        string[] resolved = [.. _apiDependentFiles.Except(observed, StringComparer.Ordinal)];

        Assert.True(
            added.Length == 0,
            "The application layer must not gain new dependencies on the API layer. "
                + $"Move the shared contract into the application layer instead: {string.Join(", ", added)}");
        Assert.True(
            resolved.Length == 0,
            "These files no longer import the API layer. Delete them from "
                + $"{nameof(_apiDependentFiles)} so the baseline keeps ratcheting down: {string.Join(", ", resolved)}");
    }

    [Fact]
    public void ExtractedApplicationProject_HasNoApiLayerDependencyToBaseline()
    {
        string extractedRoot = Path.Combine(RepositoryLayout.Root, "RvtPortal.Application");

        string[] violations = [.. Directory
            .EnumerateFiles(extractedRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File
                .ReadAllText(path)
                .Contains(ApiLayerNamespace, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(extractedRoot, path))];

        Assert.Empty(violations);
    }
}
