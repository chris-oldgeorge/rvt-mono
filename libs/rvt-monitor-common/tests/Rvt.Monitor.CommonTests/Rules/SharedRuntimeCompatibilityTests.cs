using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;

namespace Rvt.Monitor.CommonTests.Rules;

[TestClass]
public sealed class SharedRuntimeCompatibilityTests
{
    [TestInitialize]
    public void TestInitialize()
    {
        using ILoggerFactory factory = LoggerFactory.Create(_ => { });
        RvtLogger.CreateLogger(factory, nameof(SharedRuntimeCompatibilityTests));
    }

    [TestMethod]
    public void MaintainsRulesContactDtoCompatibilitySurface()
    {
        Common.Rules.RvtContactDto contact = new(
            Rvt.Monitor.Common.Rules.ContactMethod.SMSAndEmail,
            "alerts@example.test",
            "441234567890",
            email: true,
            sms: true,
            sendStartTime: null,
            sendEndTime: null);

        // The Notifications-namespace contact DTO and its converter were
        // deleted by legacy-retirement step 5 (2026-07-29); the Rules DTO is
        // the one contact surface.
        Assert.AreEqual(Rvt.Monitor.Common.Rules.ContactMethod.SMSAndEmail, contact.ContactMethod);
    }

    [TestMethod]
    public void RulesContactDtoDoesNotHideDuplicateReflectedOrSerializedContactMethodProperties()
    {
        Common.Rules.RvtContactDto contact = new(
            Rvt.Monitor.Common.Rules.ContactMethod.Email,
            "alerts@example.test",
            null,
            sendStartTime: null,
            sendEndTime: null);

        List<PropertyInfo> reflectedContactMethodProperties = [.. typeof(Rvt.Monitor.Common.Rules.RvtContactDto)
            .GetProperties()
            .Where(property => property.Name == nameof(Rvt.Monitor.Common.Rules.RvtContactDto.ContactMethod))];

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(contact));
        List<JsonProperty> serializedContactMethodProperties = [.. json.RootElement
            .EnumerateObject()
            .Where(property => property.Name == nameof(Rvt.Monitor.Common.Rules.RvtContactDto.ContactMethod))];

        Assert.HasCount(1, reflectedContactMethodProperties);
        Assert.HasCount(1, serializedContactMethodProperties);
    }

}
