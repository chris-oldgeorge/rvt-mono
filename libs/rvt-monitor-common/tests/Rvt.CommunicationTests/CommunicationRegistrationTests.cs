using Microsoft.Extensions.DependencyInjection;
using Rvt.Communication;
using Rvt.Communication.Abstractions;

namespace Rvt.CommunicationTests;

[TestClass]
public sealed class CommunicationRegistrationTests
{
    [TestMethod]
    public void AddRvtCommunication_CalledTwice_RegistersEachWorkflowServiceOnce()
    {
        ServiceCollection services = new();

        services.AddRvtCommunication();
        services.AddRvtCommunication();

        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(INotificationMessageComposer)));
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(INotificationDeliveryService)));
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(IMessageService)));
    }
}
