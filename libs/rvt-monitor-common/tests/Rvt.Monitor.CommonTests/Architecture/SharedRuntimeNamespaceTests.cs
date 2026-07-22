using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Utilities;

namespace Rvt.Monitor.CommonTests.Architecture;

[TestClass]
public sealed class SharedRuntimeNamespaceTests
{
    [TestMethod]
    public void SharedRuntimeContractsUseTheCommonLibraryNamespaces()
    {
        var runtimeContracts = new[]
        {
            typeof(RvtConfig),
            typeof(AdapterException),
            typeof(IEmailDeliveryPort),
            typeof(ISmsDeliveryPort),
            typeof(INotificationDeliveryService),
            typeof(IMessageService),
            typeof(IMqttClient),
            typeof(RvtMqttMessage),
            typeof(NotificationDto),
            typeof(global::Rvt.Monitor.Common.Rules.RvtAlertRuleDto),
            typeof(DateTimeUtil)
        };

        CollectionAssert.AllItemsAreUnique(runtimeContracts.Select(type => type.FullName).ToArray());
        Assert.IsFalse(typeof(RvtConfig).Assembly.GetTypes().Any(type =>
            type.Namespace is "Rvt.Api" or "Rvt.Api.Comms" or "Rvt.Api.Mqtt" or "Rvt.Model.Mqtt"
            or "Rvt.Notification" or "Rvt.Rules" or "Rvt.Util"));
    }
}
