namespace Omnidots.Api.UseCases;

public sealed class OmnidotsConfigurationAuthenticationException : Exception
{
    public OmnidotsConfigurationAuthenticationException()
        : base("Measuring point configuration authentication failed.")
    {
    }
}
