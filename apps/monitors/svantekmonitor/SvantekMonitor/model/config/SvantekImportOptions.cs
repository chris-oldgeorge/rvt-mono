using Microsoft.Extensions.Options;

namespace Svantek.Model.Config;

public sealed class SvantekImportOptions
{
    public const string SectionName = "SvantekImport";

    public TimeSpan MaximumInitialBackfill { get; init; } = TimeSpan.FromDays(7);
    public TimeSpan MaximumRequestWindow { get; init; } = TimeSpan.FromHours(12);
    public TimeSpan WatermarkOverlap { get; init; } = TimeSpan.FromMinutes(5);

    public void Validate()
    {
        var failures = new List<string>();
        if (MaximumInitialBackfill <= TimeSpan.Zero)
        {
            failures.Add("MaximumInitialBackfill must be positive.");
        }

        if (MaximumRequestWindow <= TimeSpan.Zero)
        {
            failures.Add("MaximumRequestWindow must be positive.");
        }

        if (WatermarkOverlap <= TimeSpan.Zero)
        {
            failures.Add("WatermarkOverlap must be positive.");
        }

        if (MaximumRequestWindow > MaximumInitialBackfill)
        {
            failures.Add("MaximumRequestWindow cannot exceed MaximumInitialBackfill.");
        }

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(SectionName, typeof(SvantekImportOptions), failures);
        }
    }
}

internal sealed class SvantekImportOptionsValidator : IValidateOptions<SvantekImportOptions>
{
    public ValidateOptionsResult Validate(string? name, SvantekImportOptions options)
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
