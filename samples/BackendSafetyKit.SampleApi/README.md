# Backend Safety Kit Sample API

Run the sample with:

~~~bash
dotnet run --project samples/BackendSafetyKit.SampleApi
~~~

The sample intentionally demonstrates the smallest setup:

~~~csharp
builder.Services.AddBackendSafety();

var app = builder.Build();

app.UseBackendSafety();
~~~

Feature behavior is added incrementally according to the project roadmap.
