// File summary: Guards the generated SPA proxy launch configuration used by Visual Studio.
// Major updates:
// - 2026-07-28 Added JSON integrity and UNC-compatible launcher coverage.

using System.Text.Json;
using System.Xml.Linq;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public sealed class SpaProxyConfigurationTests
{
    private const string UncCompatibleLaunchCommand =
        "node.exe scripts/start-vite-for-visual-studio.mjs";

    [Fact]
    public void SpaProxyLaunchCommand_GeneratesValidJson()
    {
        string launchCommand = ReadLaunchCommand();
        string generatedJson = $$"""
            {
              "SpaProxyServer": {
                "LaunchCommand": "{{launchCommand}}"
              }
            }
            """;

        Exception error = Record.Exception(() => JsonDocument.Parse(generatedJson));

        Assert.Null(error);
    }

    [Fact]
    public void SpaProxyLaunchCommand_UsesUncCompatibleNodeLauncher()
    {
        string launchCommand = ReadLaunchCommand();

        Assert.Equal(UncCompatibleLaunchCommand, launchCommand);
    }

    [Fact]
    public void WindowsLauncher_InstallsPlatformDependenciesOutsideTheSharedSourceTree()
    {
        string launcherPath = Path.Combine(
            RepositoryLayout.Root,
            "RvtPortal.Client",
            "scripts",
            "start-vite-for-visual-studio.mjs");

        string launcher = File.ReadAllText(launcherPath);

        Assert.Contains("LOCALAPPDATA", launcher, StringComparison.Ordinal);
        Assert.Contains("createHash", launcher, StringComparison.Ordinal);
        Assert.Contains("ComSpec", launcher, StringComparison.Ordinal);
        Assert.Contains("cmd.exe", launcher, StringComparison.Ordinal);
        Assert.Contains("npm.cmd", launcher, StringComparison.Ordinal);
        Assert.Contains("--ignore-scripts", launcher, StringComparison.Ordinal);
        Assert.Contains("cachedViteConfigPath", launcher, StringComparison.Ordinal);
        Assert.Contains("copyFileSync(viteConfigPath", launcher, StringComparison.Ordinal);
        Assert.Contains("robocopy.exe", launcher, StringComparison.Ordinal);
        Assert.Contains("'/MIR'", launcher, StringComparison.Ordinal);
        Assert.Contains("workspaceRoot", launcher, StringComparison.Ordinal);
        Assert.Contains("setInterval", launcher, StringComparison.Ordinal);
        Assert.Contains("node_modules", launcher, StringComparison.Ordinal);
        Assert.Contains("vite/bin/vite.js", launcher, StringComparison.Ordinal);
        Assert.Contains("--config", launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void DebugBuild_DoesNotInstallWindowsDependenciesIntoTheSharedSourceTree()
    {
        XDocument project = ReadProject();
        XElement target = Assert.Single(
            project.Descendants("Target"),
            element => element.Attribute("Name")?.Value == "DebugEnsureNodeEnv");

        string condition = Assert.IsType<XAttribute>(target.Attribute("Condition")).Value;

        Assert.Contains("'$(OS)' != 'Windows_NT'", condition, StringComparison.Ordinal);
    }

    private static string ReadLaunchCommand()
    {
        XDocument project = ReadProject();

        return Assert.Single(project.Descendants("SpaProxyLaunchCommand")).Value;
    }

    private static XDocument ReadProject()
    {
        string projectPath = Path.Combine(
            RepositoryLayout.Root,
            "RvtPortal.Spa",
            "RvtPortal.Spa.csproj");

        return XDocument.Load(projectPath);
    }
}
