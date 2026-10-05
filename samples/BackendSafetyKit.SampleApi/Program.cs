using BackendSafetyKit.AspNetCore.DependencyInjection;
using BackendSafetyKit.AspNetCore.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackendSafety();
builder.Services.AddBackendSafetyHealthChecks();

var app = builder.Build();

app.UseBackendSafety();

app.MapGet("/", () => Results.Ok(new
{
    service = "Backend Safety Kit Sample API",
    status = "running"
}));

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
