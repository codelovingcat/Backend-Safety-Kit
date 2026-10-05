using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class BackendSafetyRegistrationTests
{
    [Fact]
    public void AddBackendSafetyRegistersOptions()
    {
        var services = new ServiceCollection();

        services.AddBackendSafety(options => { });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BackendSafetyOptions>>();

        Assert.NotNull(options.Value);
    }

    [Fact]
    public async Task UseBackendSafetyAllowsRequestToReachEndpoint()
    {
        var services = new ServiceCollection();
        var builder = new ApplicationBuilder(services.BuildServiceProvider());

        var reachedEndpoint = false;

        builder.UseBackendSafety();
        builder.Run(context =>
        {
            reachedEndpoint = true;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        var app = builder.Build();
        var context = new DefaultHttpContext();
        await app(context);

        Assert.True(reachedEndpoint);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }
}
