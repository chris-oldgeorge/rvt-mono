using Rvt.Monitor.Common.Infrastructure.Communications;

namespace Rvt.Monitor.Common.InfrastructureTests.Communications;

[TestClass]
public sealed class CommunicationsStartupValidationServiceTests
{
    [TestMethod]
    public async Task StartAsync_ValidatesConfiguration()
    {
        var service = new CommunicationsStartupValidationService(new CommunicationsOptions());

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        Assert.Contains("RVT__SENDGRID_API_KEY", exception.Message);
    }

    [TestMethod]
    public async Task StopAsync_CompletesImmediately()
    {
        var service = new CommunicationsStartupValidationService(new CommunicationsOptions
        {
            EmailEnabled = false
        });

        await service.StopAsync(CancellationToken.None);
    }
}
