using BackendSafetyKit;
using Xunit;

namespace BackendSafetyKit.Tests;

public sealed class ExceptionMappingHardeningTests
{
    [Fact]
    public void MoreSpecificStatusMappingWinsRegardlessOfRegistrationOrder()
    {
        var options = new ExceptionHandlingOptions();

        options.Map<Exception>(500);
        options.Map<ApplicationException>(422);

        var exception = new SpecificApplicationException();

        Assert.Equal(422, options.GetStatusCode(exception));
    }

    [Fact]
    public void MoreSpecificProblemDetailsMappingsWinRegardlessOfRegistrationOrder()
    {
        var options = new ProblemDetailsOptions();

        options.MapErrorCode<Exception>("generic_error");
        options.MapErrorCode<ApplicationException>("application_error");
        options.MapTitle<Exception>("Generic error");
        options.MapTitle<ApplicationException>("Application error");

        var exception = new SpecificApplicationException();

        Assert.Equal("application_error", options.GetErrorCode(exception));
        Assert.Equal("Application error", options.GetTitle(exception));
    }

    private sealed class SpecificApplicationException : ApplicationException
    {
    }

    [Fact]
    public void BaseMappingStillAppliesWhenNoMoreSpecificMappingExists()
    {
        var options = new ExceptionHandlingOptions();
        options.Map<Exception>(400);

        Assert.Equal(
            400,
            options.GetStatusCode(new InvalidOperationException("test")));
    }

    [Fact]
    public void CustomizedProblemDetailsTypeMustNotBeEmpty()
    {
        var options = new ProblemDetailsOptions();
        var customization = new ProblemDetailsCustomizationContext
        {
            Exception = new InvalidOperationException(),
            RequestMethod = "GET",
            RequestPath = "/test",
            TraceId = "trace",
            Type = " "
        };

        Assert.Throws<ArgumentException>(
            () => ProblemDetailsOptions.ValidateCustomization(customization));
    }

    [Fact]
    public void CustomizedProblemDetailsExtensionNameMustNotBeEmpty()
    {
        var options = new ProblemDetailsOptions();
        var customization = new ProblemDetailsCustomizationContext
        {
            Exception = new InvalidOperationException(),
            RequestMethod = "GET",
            RequestPath = "/test",
            TraceId = "trace"
        };

        customization.Extensions[""] = "invalid";

        Assert.Throws<ArgumentException>(
            () => options.ValidateCustomization(customization));
    }
}
