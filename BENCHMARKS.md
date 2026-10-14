# Runtime Benchmarks

The runtime benchmark project uses BenchmarkDotNet to measure common Backend Safety Kit operations locally. It is a development tool, not a runtime dependency of the published NuGet packages.

## Run the benchmarks

Run the sensitive-data masking scenarios:

```bash
dotnet run --configuration Release --project benchmarks/BackendSafetyKit.Benchmarks -- --filter "*SensitiveDataMaskerBenchmarks*"
```

Run request-pipeline comparisons:

```bash
dotnet run --configuration Release --project benchmarks/BackendSafetyKit.Benchmarks -- --filter "*BackendSafetyPipelineBenchmarks*"
```

Run every benchmark:

```bash
dotnet run --configuration Release --project benchmarks/BackendSafetyKit.Benchmarks
```

BenchmarkDotNet generates reports under `BenchmarkDotNet.Artifacts` in the working directory. Keep generated reports out of source control unless a change explicitly intends to publish a reproducible result.

## Scenarios

### Sensitive data masking

- A single secret string field.
- A small flat dictionary.
- Nested dictionaries.
- A collection containing dictionaries.
- A POCO with nested credentials.

The masker and test payloads are constructed once during global setup. Each benchmark measures the mask operation, not object construction for the benchmark fixture.

### Request pipeline

- A minimal ASP.NET Core pipeline that returns `204 No Content`.
- The same terminal response wrapped with the default `UseBackendSafety()` pipeline.

Each invocation creates a fresh HTTP context and dependency-injection scope in both cases. This comparison includes the overhead of enabled package middleware and should be interpreted as an end-to-end microbenchmark, not as an isolated per-middleware measurement.

## Interpretation

Results depend on the .NET SDK/runtime, operating system, CPU power management, background load, and configuration. Run in Release mode on the same machine when comparing changes. Avoid turning one machine's timings into universal performance claims; use several runs and investigate regressions before deciding whether an optimization is warranted.

The benchmark project is excluded from NuGet packing. BenchmarkDotNet is referenced only by this project and is not added to the runtime dependency graph of the library packages.
