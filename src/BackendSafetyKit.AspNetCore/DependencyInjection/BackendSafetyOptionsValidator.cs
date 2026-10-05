using BackendSafetyKit;
using Microsoft.Extensions.Options;

namespace BackendSafetyKit.AspNetCore.DependencyInjection;

internal sealed class BackendSafetyOptionsValidator : IValidateOptions<BackendSafetyOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        BackendSafetyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        ValidateSection(
            failures,
            "ExceptionHandling",
            options.ExceptionHandling.Validate);

        ValidateSection(
            failures,
            "Correlation",
            options.Correlation.Validate);

        ValidateSection(
            failures,
            "ProblemDetails",
            options.ProblemDetails.Validate);

        ValidateSection(
            failures,
            "RequestLogging",
            options.RequestLogging.Validate);

        ValidateSection(
            failures,
            "DistributedTracing",
            options.DistributedTracing.Validate);

        ValidateSection(
            failures,
            "SensitiveDataMasking",
            options.SensitiveDataMasking.Validate);

        ValidateSection(
            failures,
            "RequestTiming",
            options.RequestTiming.Validate);

        ValidateSection(
            failures,
            "HttpSecurity",
            options.HttpSecurity.Validate);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateSection(
        List<string> failures,
        string sectionName,
        Action validate)
    {
        try
        {
            validate();
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException)
        {
            failures.Add($"{sectionName}: {exception.Message}");
        }
    }
}
