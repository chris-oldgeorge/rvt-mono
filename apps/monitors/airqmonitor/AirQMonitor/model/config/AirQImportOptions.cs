using Microsoft.Extensions.Options;

namespace AirQ.Model.Config;

// Summary: Bounds how far back a single AirQ import processes, following the
// SvantekImportOptions pattern.
// Major updates:
// - 2026-07-30 First-import query storms: added so the rule-evaluation and
//   8-hour-average start can be clamped instead of seeding a year back.
public sealed class AirQImportOptions
{
    public const string SectionName = "AirQImport";

    /// <summary>
    /// The furthest back a single run walks its averaging and rule windows. An
    /// unwatermarked monitor deployed long ago would otherwise drive roughly
    /// 1,095 eight-hour averages plus 8,760 hour windows, each a separate
    /// context and aggregate query, inside the fleet loop.
    /// </summary>
    public TimeSpan MaximumInitialBackfill { get; init; } = TimeSpan.FromDays(7);

    public void Validate()
    {
        if (MaximumInitialBackfill <= TimeSpan.Zero)
        {
            throw new OptionsValidationException(
                SectionName,
                typeof(AirQImportOptions),
                ["MaximumInitialBackfill must be positive."]);
        }
    }
}

internal sealed class AirQImportOptionsValidator : IValidateOptions<AirQImportOptions>
{
    public ValidateOptionsResult Validate(string? name, AirQImportOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (OptionsValidationException exception)
        {
            return ValidateOptionsResult.Fail(exception.Failures);
        }
    }
}
