using Microsoft.Extensions.Options;
using Omnidots.Model.Config;

namespace OmnidotsAdapterTests.Config;

[TestClass]
public sealed class OmnidotsTraceCollectionOptionsTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Validate_NonPositiveLimit_Throws(int limit)
    {
        var options = new OmnidotsTraceCollectionOptions { MaxMonitorsPerRun = limit };

        var exception = Assert.ThrowsExactly<OptionsValidationException>(options.Validate);

        Assert.AreEqual(OmnidotsTraceCollectionOptions.SectionName, exception.OptionsName);
    }

    [TestMethod]
    public void AllowedSerialIds_NullValue_NormalizesToEmpty()
    {
        var options = new OmnidotsTraceCollectionOptions { AllowedSerialIds = null! };

        options.Validate();

        Assert.AreEqual(0, options.AllowedSerialIds.Length);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void Validate_BlankSerial_Throws(string serialId)
    {
        var options = new OmnidotsTraceCollectionOptions { AllowedSerialIds = [serialId] };

        Assert.ThrowsExactly<OptionsValidationException>(options.Validate);
    }

    [TestMethod]
    public void Validate_CaseInsensitiveDuplicateSerial_Throws()
    {
        var options = new OmnidotsTraceCollectionOptions
        {
            AllowedSerialIds = ["monitor-a", "MONITOR-A"]
        };

        Assert.ThrowsExactly<OptionsValidationException>(options.Validate);
    }
}
