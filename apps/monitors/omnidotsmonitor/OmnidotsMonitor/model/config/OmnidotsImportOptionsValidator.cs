using Microsoft.Extensions.Options;

namespace Omnidots.Model.Config;

public sealed class OmnidotsImportOptionsValidator : IValidateOptions<OmnidotsImportOptions>
{
    public ValidateOptionsResult Validate(string? name, OmnidotsImportOptions options)
    {
        IReadOnlyList<string> failures = options.GetValidationFailures();
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
