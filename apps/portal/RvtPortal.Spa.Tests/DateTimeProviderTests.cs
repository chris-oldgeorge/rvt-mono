// File summary: Covers configured clock and time-zone behavior for business-layer date conversion.
// Major updates:
// - 2026-07-09 pending Added regression coverage for DI/options-based time-zone conversion.

using Microsoft.Extensions.Options;
using RVT.BusinessLogic;

namespace RvtPortal.Spa.Tests;

public sealed class DateTimeProviderTests
{
    [Fact]
    // Function summary: Verifies the date-time provider uses the injected local time-zone option for UTC/local conversion.
    public void RvtDateTimeProvider_UsesConfiguredTimeZone()
    {
        var provider = new RvtDateTimeProvider(Options.Create(new RvtTimeZoneOptions
        {
            Local = "South Africa Standard Time"
        }));
        var utc = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var local = provider.UtcToLocal(utc);
        var roundTrip = provider.LocalToUtc(local);

        Assert.Equal(new DateTime(2026, 1, 15, 12, 0, 0), local);
        Assert.Equal(utc, roundTrip);
    }
}
