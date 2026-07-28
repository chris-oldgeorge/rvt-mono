using Rvt.Monitor.IntegrationTesting;

namespace Rvt.Monitor.IntegrationTesting.Tests;

[TestClass]
public sealed class RepositoryLayoutTests
{
    [TestMethod]
    public void FindRepositoryRoot_UsesOutputTreeWhenItContainsRepositoryMarkers()
    {
        using var fixture = TemporaryDirectory.Create();
        string repositoryRoot = fixture.CreateRepository("repository");
        string outputDirectory = fixture.CreateDirectory(
            "repository",
            "artifacts",
            "bin",
            "Rvt.Monitor.IntegrationTesting.Tests");
        string sourceFile = fixture.CreateFile(
            "unrelated-source",
            "RepositoryLayoutTests.cs");

        string actual = RepositoryLayout.FindRepositoryRoot(outputDirectory, sourceFile);

        Assert.AreEqual(repositoryRoot, actual);
    }

    [TestMethod]
    public void FindRepositoryRoot_FallsBackToSourceTreeWhenOutputIsRedirected()
    {
        using var fixture = TemporaryDirectory.Create();
        string repositoryRoot = fixture.CreateRepository("repository");
        string sourceFile = fixture.CreateFile(
            "repository",
            "libs",
            "rvt-monitor-common",
            "testing",
            "Rvt.Monitor.IntegrationTesting",
            "RepositoryLayout.cs");
        string redirectedOutput = fixture.CreateDirectory(
            "redirected-output",
            "bin",
            "Rvt.Monitor.IntegrationTesting.Tests");

        string actual = RepositoryLayout.FindRepositoryRoot(redirectedOutput, sourceFile);

        Assert.AreEqual(repositoryRoot, actual);
    }

    [TestMethod]
    public void FindRepositoryRoot_RejectsGitMarkerWithoutMonoSolution()
    {
        using var fixture = TemporaryDirectory.Create();
        string falseRoot = fixture.CreateDirectory("not-the-monorepo");
        File.WriteAllText(
            System.IO.Path.Combine(falseRoot, ".git"),
            "gitdir: /tmp/not-the-monorepo.git");
        string sourceFile = fixture.CreateFile(
            "not-the-monorepo",
            "src",
            "Probe.cs");
        string redirectedOutput = fixture.CreateDirectory("redirected-output");

        DirectoryNotFoundException exception = Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => RepositoryLayout.FindRepositoryRoot(redirectedOutput, sourceFile));

        StringAssert.Contains(exception.Message, redirectedOutput);
        StringAssert.Contains(exception.Message, sourceFile);
    }

    [TestMethod]
    public void GetPath_CombinesSegmentsBelowTheResolvedRoot()
    {
        string expected = System.IO.Path.Combine(
            RepositoryLayout.Root,
            "apps",
            "monitors",
            "myatmmonitor");

        string actual = RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "myatmmonitor");

        Assert.AreEqual(expected, actual);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"rvt-repository-layout-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public string CreateRepository(string segment)
        {
            string repositoryRoot = CreateDirectory(segment);
            File.WriteAllText(
                System.IO.Path.Combine(repositoryRoot, ".git"),
                "gitdir: /tmp/rvt-repository-layout.git");
            File.WriteAllText(
                System.IO.Path.Combine(repositoryRoot, "Rvt.Mono.slnx"),
                "<Solution />");
            return repositoryRoot;
        }

        public string CreateDirectory(params string[] segments)
        {
            string path = GetPath(segments);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(params string[] segments)
        {
            string path = GetPath(segments);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }

        private string GetPath(params string[] segments) =>
            System.IO.Path.Combine([Path, .. segments]);
    }
}
