using Microsoft.Extensions.Options;

namespace Omnidots.Model.Config;

public sealed class OmnidotsTraceCollectionOptions
{
    public const string SectionName = "Omnidots:TraceCollection";

    private string[] allowedSerialIds = [];

    public bool Enabled { get; init; } = true;

    public string[] AllowedSerialIds
    {
        get => allowedSerialIds;
        init => allowedSerialIds = value ?? [];
    }

    public int MaxMonitorsPerRun { get; init; } = 1;

    public void Validate()
    {
        var failures = GetValidationFailures();
        if (failures.Count > 0)
        {
            throw new OptionsValidationException(SectionName, typeof(OmnidotsTraceCollectionOptions), failures);
        }
    }

    internal IReadOnlyList<string> GetValidationFailures()
    {
        var failures = new List<string>();

        if (MaxMonitorsPerRun <= 0)
        {
            failures.Add("MaxMonitorsPerRun must be positive.");
        }

        if (AllowedSerialIds.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("AllowedSerialIds cannot contain blank values.");
        }

        if (AllowedSerialIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != AllowedSerialIds.Length)
        {
            failures.Add("AllowedSerialIds cannot contain duplicates.");
        }

        return failures;
    }
}
