# Portal Email Enabled Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Portal SendGrid delivery honor `RVT__Email_ENABLED` while retaining enabled delivery by default.

**Architecture:** The existing Portal host registration reads the standard .NET configuration key `RVT:EMAIL_ENABLED`, which environment variables expose as `RVT__Email_ENABLED`. That boolean directly initializes `SendGridMailOptions.Enabled`; the existing `EmailConfiguration` keys remain responsible for provider credentials and sender metadata.

**Tech Stack:** .NET 10, ASP.NET Core configuration and dependency injection, xUnit, Microsoft.Extensions.Options.

## Global Constraints

- Preserve the default enabled behavior when `RVT:EMAIL_ENABLED` is absent.
- Do not change `EmailConfiguration`, `Auth:SkipPasswordResetEmail`, email credentials, or deployment secrets.
- Treat `RVT__Email_ENABLED=false` as the launch-profile/runtime environment-variable form of `RVT:EMAIL_ENABLED=false`.

---

### Task 1: Configure and test the Portal SendGrid enable switch

**Files:**
- Create: `apps/portal/RvtPortal.Spa.Tests/SendGridConfigurationTests.cs`
- Modify: `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs:105-112`

**Interfaces:**
- Consumes: `ServiceCollectionExtensions.AddRvtPortalBusinessServices(IServiceCollection, IConfiguration)` and `SendGridMailOptions.Enabled`.
- Produces: Portal dependency injection registers `IOptions<SendGridMailOptions>` with `Enabled` derived from `RVT:EMAIL_ENABLED`.

- [ ] **Step 1: Write the failing test**

Create `SendGridConfigurationTests.cs` with a host-factory test that sets the configuration key and reads the real registered options:

```csharp
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
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter FullyQualifiedName~SendGridConfigurationTests --no-restore --nologo
```

Expected: the `false` case fails because `ServiceCollectionExtensions` currently assigns `Enabled = true` unconditionally.

- [ ] **Step 3: Implement the minimal configuration binding**

In `ServiceCollectionExtensions.AddRvtPortalBusinessServices`, resolve the setting immediately before `AddSendGridMail` and assign it to the provider options:

```csharp
var emailEnabled = configuration.GetValue("RVT:EMAIL_ENABLED", true);

services.AddSendGridMail(new SendGridMailOptions
{
    Enabled = emailEnabled,
    ApiKey = configuration["EmailConfiguration:SENDGRID_API_KEY"] ?? string.Empty,
    FromEmail = configuration["EmailConfiguration:Sending_Email_Address"] ?? string.Empty,
    FromName = "RVT Cloud"
});
```

- [ ] **Step 4: Run focused verification**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter FullyQualifiedName~SendGridConfigurationTests --no-restore --nologo
```

Expected: both theory cases pass; an absent key resolves to `true`, and `false` resolves to `false`.

- [ ] **Step 5: Run Portal regression verification**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore --nologo
```

Expected: all non-opt-in Portal tests pass; any test marked as requiring PostgreSQL remains skipped unless `RVT_TEST_POSTGRES_CONNECTION` is configured.

- [ ] **Step 6: Commit**

```bash
git add apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs \
  apps/portal/RvtPortal.Spa.Tests/SendGridConfigurationTests.cs
git commit -m "feat: configure portal email delivery"
```
