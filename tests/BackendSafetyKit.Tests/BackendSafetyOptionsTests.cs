using BackendSafetyKit;
using Xunit;

namespace BackendSafetyKit.Tests;

public sealed class BackendSafetyOptionsTests
{
    [Fact]
    public void OptionsCanBeCreatedWithSafeDefaults()
    {
        var options = new BackendSafetyOptions();

        Assert.NotNull(options);
    }

    [Fact]
    public void DiagnosticEventCopiesPropertiesAndExposesAReadOnlySnapshot()
    {
        var properties = new Dictionary<string, object?>
        {
            ["http.request.method"] = "GET"
        };

        var diagnosticEvent = new BackendSafetyDiagnosticEvent(
            BackendSafetyDiagnosticEventNames.HttpRequestCompleted,
            DateTimeOffset.UtcNow,
            BackendSafetyDiagnosticSeverity.Information,
            properties);

        properties["http.request.method"] = "POST";

        Assert.Equal("GET", diagnosticEvent.Properties["http.request.method"]);

        var readOnlyProperties = Assert.IsAssignableFrom<IDictionary<string, object?>>(
            diagnosticEvent.Properties);
        Assert.Throws<NotSupportedException>(
            () => readOnlyProperties["http.request.method"] = "DELETE");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DiagnosticEventRejectsBlankNames(string name)
    {
        Assert.Throws<ArgumentException>(
            () => new BackendSafetyDiagnosticEvent(
                name,
                DateTimeOffset.UtcNow,
                BackendSafetyDiagnosticSeverity.Information));
    }

    [Fact]
    public void DiagnosticEventRejectsInvalidSeverity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BackendSafetyDiagnosticEvent(
                "example.event",
                DateTimeOffset.UtcNow,
                (BackendSafetyDiagnosticSeverity)999));
    }
}
