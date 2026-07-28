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
        string currentDirectory = fixture.CreateDirectory("unrelated-current");

        string actual = RepositoryLayout.FindRepositoryRoot(
            outputDirectory,
            sourceFile,
            currentDirectory,
            configuredRoot: null);

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
        string currentDirectory = fixture.CreateDirectory("unrelated-current");

        string actual = RepositoryLayout.FindRepositoryRoot(
            redirectedOutput,
            sourceFile,
            currentDirectory,
            configuredRoot: null);

        Assert.AreEqual(repositoryRoot, actual);
    }

    [TestMethod]
    public void FindRepositoryRoot_UsesCurrentDirectoryWhenSourcePathIsMappedAndOutputIsRedirected()
    {
        using var fixture = TemporaryDirectory.Create();
        string repositoryRoot = fixture.CreateRepository("repository");
        string currentDirectory = fixture.CreateDirectory(
            "repository",
            "apps",
            "monitors",
            "myatmmonitor");
        string redirectedOutput = fixture.CreateDirectory(
            "redirected-output",
            "bin",
            "Rvt.Monitor.IntegrationTesting.Tests");

        string actual = RepositoryLayout.FindRepositoryRoot(
            redirectedOutput,
            "/_/libs/rvt-monitor-common/testing/RepositoryLayout.cs",
            currentDirectory,
            configuredRoot: null);

        Assert.AreEqual(repositoryRoot, actual);
    }

    [TestMethod]
    public void FindRepositoryRoot_RejectsDistinctCheckoutCandidates()
    {
        using var fixture = TemporaryDirectory.Create();
        string outputRepository = fixture.CreateRepository("output-repository");
        string sourceRepository = fixture.CreateRepository("source-repository");
        string outputDirectory = fixture.CreateDirectory(
            "output-repository",
            "artifacts",
            "bin");
        string sourceFile = fixture.CreateFile(
            "source-repository",
            "libs",
            "RepositoryLayout.cs");
        string currentDirectory = fixture.CreateDirectory("unrelated-current");

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => RepositoryLayout.FindRepositoryRoot(
                outputDirectory,
                sourceFile,
                currentDirectory,
                configuredRoot: null));

        StringAssert.Contains(exception.Message, "output");
        StringAssert.Contains(exception.Message, outputRepository);
        StringAssert.Contains(exception.Message, "source");
        StringAssert.Contains(exception.Message, sourceRepository);
    }

    [TestMethod]
    public void FindRepositoryRoot_RejectsInvalidConfiguredRootWithoutFallingBack()
    {
        using var fixture = TemporaryDirectory.Create();
        string validRepository = fixture.CreateRepository("repository");
        string outputDirectory = fixture.CreateDirectory(
            "repository",
            "artifacts",
            "bin");
        string sourceFile = fixture.CreateFile(
            "repository",
            "libs",
            "RepositoryLayout.cs");
        string invalidConfiguredRoot = fixture.CreateDirectory("configured-root");

        DirectoryNotFoundException exception = Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => RepositoryLayout.FindRepositoryRoot(
                outputDirectory,
                sourceFile,
                validRepository,
                invalidConfiguredRoot));

        StringAssert.Contains(exception.Message, "RVT_MONOREPO_ROOT");
        StringAssert.Contains(exception.Message, invalidConfiguredRoot);
    }

    [TestMethod]
    public void FindRepositoryRoot_AcceptsConfiguredRootFromArbitraryWorkingDirectory()
    {
        using var fixture = TemporaryDirectory.Create();
        string configuredRoot = fixture.CreateRepository("repository");
        string outputDirectory = fixture.CreateDirectory("redirected-output");
        string currentDirectory = fixture.CreateDirectory("arbitrary-current");

        string actual = RepositoryLayout.FindRepositoryRoot(
            outputDirectory,
            "/_/libs/rvt-monitor-common/testing/RepositoryLayout.cs",
            currentDirectory,
            configuredRoot);

        Assert.AreEqual(configuredRoot, actual);
    }

    [TestMethod]
    public void FindRepositoryRoot_CollapsesSymlinkAliasesOfTheSameCheckout()
    {
        using var fixture = TemporaryDirectory.Create();
        string repositoryRoot = fixture.CreateRepository("repository");
        string repositoryAlias = fixture.CreateDirectoryLink("repository-alias", repositoryRoot);
        string outputDirectory = fixture.CreateDirectory(
            "repository",
            "artifacts",
            "bin");
        string sourceFile = System.IO.Path.Combine(
            repositoryAlias,
            "libs",
            "RepositoryLayout.cs");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sourceFile)!);
        File.WriteAllText(sourceFile, string.Empty);
        string currentDirectory = fixture.CreateDirectory("unrelated-current");

        string actual = RepositoryLayout.FindRepositoryRoot(
            outputDirectory,
            sourceFile,
            currentDirectory,
            configuredRoot: null);

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
        string currentDirectory = fixture.CreateDirectory("unrelated-current");

        DirectoryNotFoundException exception = Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => RepositoryLayout.FindRepositoryRoot(
                redirectedOutput,
                sourceFile,
                currentDirectory,
                configuredRoot: null));

        StringAssert.Contains(exception.Message, redirectedOutput);
        StringAssert.Contains(exception.Message, sourceFile);
    }

    [TestMethod]
    public void FindRepositoryRoot_AcceptsGitDirectoryMarker()
    {
        using var fixture = TemporaryDirectory.Create();
        string repositoryRoot = fixture.CreateRepository(
            "repository",
            useGitDirectory: true);
        string outputDirectory = fixture.CreateDirectory(
            "repository",
            "artifacts",
            "bin");
        string sourceFile = fixture.CreateFile("unrelated-source", "Probe.cs");
        string currentDirectory = fixture.CreateDirectory("unrelated-current");

        string actual = RepositoryLayout.FindRepositoryRoot(
            outputDirectory,
            sourceFile,
            currentDirectory,
            configuredRoot: null);

        Assert.AreEqual(repositoryRoot, actual);
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

    [TestMethod]
    public void GetPath_RejectsRootedSegment()
    {
        string rootedSegment = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "outside-repository"));

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => RepositoryLayout.GetPath("apps", rootedSegment));

        StringAssert.Contains(exception.Message, rootedSegment);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow("apps/monitors")]
    [DataRow(@"apps\monitors")]
    public void GetPath_RejectsInvalidSegment(string invalidSegment)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => RepositoryLayout.GetPath("apps", invalidSegment));

        StringAssert.Contains(exception.Message, "segment");
    }

    [TestMethod]
    public void GetPath_RejectsNullSegment()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => RepositoryLayout.GetPath("apps", null!));
    }

    [TestMethod]
    public void GetPath_RejectsEmptySegmentCollection()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => RepositoryLayout.GetPath());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly List<string> _directoryLinks = [];

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

        public string CreateRepository(
            string segment,
            bool useGitDirectory = false)
        {
            string repositoryRoot = CreateDirectory(segment);
            string gitPath = System.IO.Path.Combine(repositoryRoot, ".git");
            if (useGitDirectory)
            {
                Directory.CreateDirectory(gitPath);
            }
            else
            {
                File.WriteAllText(
                    gitPath,
                    "gitdir: /tmp/rvt-repository-layout.git");
            }

            File.WriteAllText(
                System.IO.Path.Combine(repositoryRoot, "Rvt.Mono.slnx"),
                "<Solution />");
            return repositoryRoot;
        }

        public string CreateDirectoryLink(
            string segment,
            string targetPath)
        {
            string linkPath = GetPath(segment);
            try
            {
                Directory.CreateSymbolicLink(linkPath, targetPath);
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                PlatformNotSupportedException)
            {
                Assert.Inconclusive(
                    $"Directory symbolic links are not supported in this environment: {exception.Message}");
            }

            _directoryLinks.Add(linkPath);
            return linkPath;
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
            foreach (string directoryLink in _directoryLinks)
            {
                if (Directory.Exists(directoryLink))
                {
                    Directory.Delete(directoryLink);
                }
            }

            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }

        private string GetPath(params string[] segments) =>
            System.IO.Path.Combine([Path, .. segments]);
    }
}
