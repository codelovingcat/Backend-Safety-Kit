using Microsoft.Extensions.Options;

namespace BackendSafetyKit.Redis.DependencyInjection;

internal sealed class BackendSafetyRedisOptionsValidator : IValidateOptions<BackendSafetyRedisOptions>
{
    public ValidateOptionsResult Validate(string? name, BackendSafetyRedisOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (ArgumentException exception)
        {
            return ValidateOptionsResult.Fail(exception.Message);
        }
    }
}
