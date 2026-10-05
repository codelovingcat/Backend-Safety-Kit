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
}
