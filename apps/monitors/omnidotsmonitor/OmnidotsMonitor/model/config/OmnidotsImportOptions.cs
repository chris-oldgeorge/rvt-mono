using Microsoft.Extensions.Options;

namespace Omnidots.Model.Config;

/// <summary>
/// Bounds a single vendor measurement request. The Omnidots HTTP client caps a
/// response at 4 MB and times out after 30 seconds, and the import handlers
/// asked for the whole range from the cursor to now in one call - so a
/// months-old monitor stalled permanently. Svantek's equivalent cap is
/// <c>SvantekImportOptions.MaximumRequestWindow</c>.
/// </summary>
public sealed class OmnidotsImportOptions
{
    public const string SectionName = "Omnidots:Import";

    public TimeSpan MaximumRequestWindow { get; init; } = TimeSpan.FromHours(12);

    /// <summary>
    /// How far back a monitor that has never imported may reach. Only the
    /// bootstrap path is capped - a monitor with a cursor or stored samples
    /// still catches up in full, one window at a time. Without this an
    /// unimported monitor's start came from its deployment date, which can be
    /// arbitrarily far back (Svantek's <c>MaximumInitialBackfill</c> is the
    /// same cap for the same reason); Veff and Vdv already bootstrap from a
    /// two-hour lookback.
    /// </summary>
    public TimeSpan MaximumInitialBackfill { get; init; } = TimeSpan.FromDays(7);

    public void Validate()
    {
        IReadOnlyList<string> failures = GetValidationFailures();
        if (failures.Count > 0)
        {
            throw new OptionsValidationException(SectionName, typeof(OmnidotsImportOptions), failures);
        }
    }

    internal IReadOnlyList<string> GetValidationFailures()
    {
        List<string> failures = [];

        if (MaximumRequestWindow <= TimeSpan.Zero)
        {
            failures.Add("MaximumRequestWindow must be positive.");
        }

        if (MaximumInitialBackfill <= TimeSpan.Zero)
        {
            failures.Add("MaximumInitialBackfill must be positive.");
        }

        if (MaximumRequestWindow > MaximumInitialBackfill)
        {
            failures.Add("MaximumRequestWindow cannot exceed MaximumInitialBackfill.");
        }

        return failures;
    }
}
