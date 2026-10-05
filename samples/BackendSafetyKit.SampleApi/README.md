# Backend Safety Kit Sample API

Run the sample with:

```bash
dotnet run --project samples/BackendSafetyKit.SampleApi
```

The sample demonstrates the intended minimal setup plus the initial safety features:

```csharp
builder.Services.AddBackendSafety();
builder.Services.AddBackendSafetyHealthChecks();

var app = builder.Build();

app.UseBackendSafety();
```

Example endpoints:

| Endpoint | Behavior |
| --- | --- |
| `GET /` | Basic sample response |
| `GET /api/orders/42` | Successful request |
| `GET /api/orders/missing` | Mapped `404` ProblemDetails |
| `GET /api/validation` | Mapped `400` ProblemDetails |
| `GET /api/slow` | Slow-request timing diagnostics |
| `GET /api/diagnostics/masked` | Sensitive structured values are redacted |
| `GET /health` | Backend Safety configuration health check |

The sample also allowlists `X-Api-Key` for structured logging so the built-in sensitive-data masker can be observed protecting an otherwise allowlisted sensitive header.

Try a request with a correlation ID:

```bash
curl -i -H "X-Correlation-ID: demo-42" http://localhost:5000/api/orders/42
```

The response contains the effective `X-Correlation-ID` and the secure default `X-Content-Type-Options: nosniff` header.

The sample has no database, cache, cloud service, or other external infrastructure dependency.
