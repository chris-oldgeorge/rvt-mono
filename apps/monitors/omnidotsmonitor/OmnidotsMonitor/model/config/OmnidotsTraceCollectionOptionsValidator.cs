using Microsoft.Extensions.Options;

namespace Omnidots.Model.Config;

public sealed class OmnidotsTraceCollectionOptionsValidator : IValidateOptions<OmnidotsTraceCollectionOptions>
{
    public ValidateOptionsResult Validate(string? name, OmnidotsTraceCollectionOptions options)
    {
        var failures = options.GetValidationFailures();
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
