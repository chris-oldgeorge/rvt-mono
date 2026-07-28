using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rvt.Communication.SendGridMail;

namespace RvtPortal.Spa.Tests;

public sealed class SendGridConfigurationTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("false", false)]
    public void SendGridRegistration_UsesRvtEmailEnabledConfiguration(
        string? emailEnabled,
        bool expectedEnabled)
    {
        using var factory = new SpaTestApplicationFactory().WithWebHostBuilder(builder =>
        {
            if (emailEnabled is not null)
            {
                builder.UseSetting("RVT:EMAIL_ENABLED", emailEnabled);
            }
        });

        var options = factory.Services
            .GetRequiredService<IOptions<SendGridMailOptions>>()
            .Value;

        Assert.Equal(expectedEnabled, options.Enabled);
    }
}
