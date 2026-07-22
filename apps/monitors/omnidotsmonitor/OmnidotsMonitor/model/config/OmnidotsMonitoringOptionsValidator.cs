using Microsoft.Extensions.Options;

namespace Omnidots.Model.Config;

public sealed class OmnidotsMonitoringOptionsValidator : IValidateOptions<OmnidotsMonitoringOptions>
{
    public ValidateOptionsResult Validate(string? name, OmnidotsMonitoringOptions options)
    {
        var failures = options.GetValidationFailures();
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
