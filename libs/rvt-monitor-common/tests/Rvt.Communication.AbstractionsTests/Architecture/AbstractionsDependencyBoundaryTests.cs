using System.Reflection;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Communication.AbstractionsTests.Architecture;

[TestClass]
public sealed class AbstractionsDependencyBoundaryTests
{
    [TestMethod]
    public void AbstractionsProject_HasNoProjectOrProviderDependencies()
    {
        string project = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj"));

        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("SendGrid", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Azure.", project, StringComparison.Ordinal);
        Assert.DoesNotContain("AWSSDK.", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore.App", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Rvt.Monitor.Common.csproj", project, StringComparison.Ordinal);
    }

    [TestMethod]
    public void MessageServiceContract_UsesTopLevelLegacyEnums()
    {
        MethodInfo? method = typeof(IMessageService).GetMethod(nameof(IMessageService.SendMessageAsync));

        Assert.IsNotNull(method);
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(LegacyMessageKind),
                typeof(LegacyMessageChannel),
                typeof(RvtContactDto),
                typeof(string),
                typeof(string),
                typeof(CancellationToken)
            },
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.AreSame(typeof(IMessageService).Assembly, typeof(RvtContactDto).Assembly);
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
