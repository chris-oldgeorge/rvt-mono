namespace Rvt.CommunicationTests.Architecture;

[TestClass]
public sealed class CommunicationDependencyBoundaryTests
{
    [TestMethod]
    public void CommunicationProject_ReferencesOnlyAbstractionsAndNeutralDependencyInjection()
    {
        string project = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "libs/rvt-monitor-common/src/Rvt.Communication/Rvt.Communication.csproj"));

        Assert.Contains("Rvt.Communication.Abstractions", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Extensions.DependencyInjection.Abstractions", project, StringComparison.Ordinal);
        Assert.DoesNotContain("SendGrid", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Azure.Identity", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Azure.Storage", project, StringComparison.Ordinal);
        Assert.DoesNotContain("AWSSDK.S3", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore.App", project, StringComparison.Ordinal);

        string[] productionSource = Directory
            .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "libs/rvt-monitor-common/src/Rvt.Communication"), "*.cs")
            .Select(File.ReadAllText)
            .ToArray();

        Assert.IsFalse(productionSource.Any(source => source.Contains("Rvt.Monitor.Common.Infrastructure", StringComparison.Ordinal)));
        Assert.IsFalse(productionSource.Any(source => source.Contains("Rvt.Monitor.Common.Communications", StringComparison.Ordinal)));
        Assert.IsFalse(productionSource.Any(source => source.Contains("SendGrid", StringComparison.Ordinal)));
        Assert.IsFalse(productionSource.Any(source => source.Contains("Azure.", StringComparison.Ordinal)));
        Assert.IsFalse(productionSource.Any(source => source.Contains("AWSSDK", StringComparison.Ordinal)));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
