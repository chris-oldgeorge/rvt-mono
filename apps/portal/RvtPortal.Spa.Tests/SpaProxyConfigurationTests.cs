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
        "node.exe node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5173 --strictPort";

    [Fact]
    public void SpaProxyLaunchCommand_GeneratesValidJson()
    {
        var launchCommand = ReadLaunchCommand();
        var generatedJson = $$"""
            {
              "SpaProxyServer": {
                "LaunchCommand": "{{launchCommand}}"
              }
            }
            """;

        var error = Record.Exception(() => JsonDocument.Parse(generatedJson));

        Assert.Null(error);
    }

    [Fact]
    public void SpaProxyLaunchCommand_UsesUncCompatibleNodeLauncher()
    {
        var launchCommand = ReadLaunchCommand();

        Assert.Equal(UncCompatibleLaunchCommand, launchCommand);
    }

    private static string ReadLaunchCommand()
    {
        var projectPath = Path.Combine(
            RepositoryLayout.Root,
            "RvtPortal.Spa",
            "RvtPortal.Spa.csproj");
        var project = XDocument.Load(projectPath);

        return Assert.Single(project.Descendants("SpaProxyLaunchCommand")).Value;
    }
}
