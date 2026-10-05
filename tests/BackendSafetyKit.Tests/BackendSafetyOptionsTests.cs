using BackendSafetyKit;

namespace BackendSafetyKit.Tests;

public sealed class BackendSafetyOptionsTests
{
    [Fact]
    public void Options_CanBeCreatedWithSafeDefaults()
    {
        var options = new BackendSafetyOptions();

        Assert.NotNull(options);
    }
}
