using Microsoft.Extensions.Options;

namespace MyAtm.Model.Config;

public sealed class MyAtmMonitorOptionsValidator : IValidateOptions<MyAtmMonitorOptions>
{
    public ValidateOptionsResult Validate(string? name, MyAtmMonitorOptions options) =>
        ValidateOptions(options.Validate);

    private static ValidateOptionsResult ValidateOptions(Action validate)
    {
        try
        {
            validate();
            return ValidateOptionsResult.Success;
        }
        catch (OptionsValidationException exception)
        {
            return ValidateOptionsResult.Fail(exception.Failures);
        }
    }
}

public sealed class MyAtmVendorOptionsValidator : IValidateOptions<MyAtmVendorOptions>
{
    public ValidateOptionsResult Validate(string? name, MyAtmVendorOptions options)
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
