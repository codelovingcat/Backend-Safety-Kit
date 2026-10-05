using BackendSafetyKit.AspNetCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackendSafety();

var app = builder.Build();

app.UseBackendSafety();

app.MapGet("/", () => Results.Ok(new
{
    service = "Backend Safety Kit Sample API",
    status = "running"
}));

app.Run();

public partial class Program;
