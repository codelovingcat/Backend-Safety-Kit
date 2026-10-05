using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using BackendSafetyKit.AspNetCore.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackendSafety(options =>
{
    options.ExceptionHandling.Map<KeyNotFoundException>(StatusCodes.Status404NotFound);
    options.ExceptionHandling.Map<ArgumentException>(StatusCodes.Status400BadRequest);

    options.RequestLogging.AllowedRequestHeaders.Add("X-Api-Key");

    options.RequestTiming.SlowRequestThreshold =
        TimeSpan.FromMilliseconds(20);
    options.RequestTiming.EnableSlowRequestLogging = true;
});

builder.Services.AddBackendSafetyHealthChecks();

var app = builder.Build();

app.UseBackendSafety();

app.MapGet("/", () => Results.Ok(new
{
    service = "Backend Safety Kit Sample API",
    status = "running"
}));

app.MapGet("/api/orders/{id}", (string id) => Results.Ok(new
{
    id,
    status = "ready"
}));

app.MapGet(
    "/api/orders/missing",
    static IResult () => throw new KeyNotFoundException("order-42"));

app.MapGet(
    "/api/validation",
    static IResult () =>
        throw new ArgumentException("validation details must not be exposed"));

app.MapGet("/api/slow", async (CancellationToken cancellationToken) =>
{
    await Task.Delay(50, cancellationToken);

    return Results.Ok(new
    {
        endpoint = "slow",
        status = "completed"
    });
});

app.MapGet("/api/diagnostics/masked", (ISensitiveDataMasker masker) =>
{
    var payload = new
    {
        username = "demo-user",
        password = "sample-password",
        accessToken = "sample-token"
    };

    return Results.Ok(masker.Mask(payload));
});

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
