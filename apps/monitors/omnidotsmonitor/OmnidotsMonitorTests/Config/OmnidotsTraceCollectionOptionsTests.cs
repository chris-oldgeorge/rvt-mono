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
        OmnidotsTraceCollectionOptions options = new OmnidotsTraceCollectionOptions { MaxMonitorsPerRun = limit };

        OptionsValidationException exception = Assert.ThrowsExactly<OptionsValidationException>(options.Validate);

        Assert.AreEqual(OmnidotsTraceCollectionOptions.SectionName, exception.OptionsName);
    }

    [TestMethod]
    public void AllowedSerialIds_NullValue_NormalizesToEmpty()
    {
        OmnidotsTraceCollectionOptions options = new OmnidotsTraceCollectionOptions { AllowedSerialIds = null! };

        options.Validate();

        Assert.IsEmpty(options.AllowedSerialIds);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void Validate_BlankSerial_Throws(string serialId)
    {
        OmnidotsTraceCollectionOptions options = new OmnidotsTraceCollectionOptions { AllowedSerialIds = [serialId] };

        Assert.ThrowsExactly<OptionsValidationException>(options.Validate);
    }

    [TestMethod]
    public void Validate_CaseInsensitiveDuplicateSerial_Throws()
    {
        OmnidotsTraceCollectionOptions options = new OmnidotsTraceCollectionOptions
        {
            AllowedSerialIds = ["monitor-a", "MONITOR-A"]
        };

        Assert.ThrowsExactly<OptionsValidationException>(options.Validate);
    }
}
