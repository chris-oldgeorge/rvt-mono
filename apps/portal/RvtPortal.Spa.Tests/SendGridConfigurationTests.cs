using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rvt.Communication.SendGridMail;

using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public sealed class SendGridConfigurationTests
{
    [RequiresPostgresTheory]
    [InlineData(null, true)]
    [InlineData("false", false)]
    public void SendGridRegistration_UsesRvtEmailEnabledConfiguration(
        string? emailEnabled,
        bool expectedEnabled)
    {
        // Dispose the SpaTestApplicationFactory itself, not just the derived factory: only the parent's
        // disposal drops the throwaway PostgreSQL schema.
        using SpaTestApplicationFactory parentFactory = new();
        using WebApplicationFactory<Program> factory = parentFactory.WithWebHostBuilder(builder =>
        {
            if (emailEnabled is not null)
            {
                builder.UseSetting("RVT:EMAIL_ENABLED", emailEnabled);
            }
        });

        SendGridMailOptions options = factory.Services
            .GetRequiredService<IOptions<SendGridMailOptions>>()
            .Value;

        Assert.Equal(expectedEnabled, options.Enabled);
    }
}
