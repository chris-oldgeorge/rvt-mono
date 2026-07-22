namespace Omnidots.Api.UseCases;

public sealed class OmnidotsVendorConfigurationException : Exception
{
    public OmnidotsVendorConfigurationException()
        : base("Measuring point configuration failed.")
    {
    }
}
